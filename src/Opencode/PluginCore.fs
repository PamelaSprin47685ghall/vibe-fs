module VibeFs.Opencode.PluginCore

open Fable.Core
open Fable.Core.JsInterop
open Fable.Core.JS
open VibeFs.Kernel
open VibeFs.Kernel.Config
open VibeFs.Kernel.Prompts
open VibeFs.Kernel.ReviewSession
open VibeFs.Opencode.Tools
open VibeFs.Opencode.Hooks
open VibeFs.Opencode.NudgeHook
open VibeFs.Opencode.Session
open VibeFs.Shell.FuzzyFinderShell
open VibeFs.Opencode.Actors
open VibeFs.Opencode.MagicTodo
open VibeFs.Kernel.HostTools

[<Global("process")>]
let private nodeProcess : obj = jsNative

let private envVar (name: string) : string =
    let v = nodeProcess?env?(name)
    if isNull v then "" else string v

let private emptyObj () : obj = createObj []
let private setKey (o: obj) (k: string) (v: obj) : unit = o?(k) <- v
let private assignInto (target: obj) (source: obj) : obj = Dyn.assignInto target source
let private clearArray (arr: obj) : unit = (arr :?> ResizeArray<obj>).Clear()
let private pushPart (arr: obj) (part: obj) : unit = (arr :?> ResizeArray<obj>).Add(part)

let private emptyMcps : obj = [||] :> obj

type private BuiltinAgentSpec =
    { name: string
      defaultMode: string
      systemPrompt: string
      defaultMcps: string array }

let private defaultPrimaryAliases = [ "manager"; "build"; "plan" ]

let private builtinAgentSpecs =
    [ { name = "manager"; defaultMode = "primary"; systemPrompt = Prompts.managerSystemPrompt; defaultMcps = [||] }
      { name = "build"; defaultMode = "primary"; systemPrompt = ""; defaultMcps = [||] }
      { name = "plan"; defaultMode = "primary"; systemPrompt = ""; defaultMcps = [||] }
      { name = "coder"; defaultMode = "subagent"; systemPrompt = ""; defaultMcps = [||] }
      { name = "investigator"; defaultMode = "subagent"; systemPrompt = ""; defaultMcps = [||] }
      { name = "meditator"; defaultMode = "subagent"; systemPrompt = ""; defaultMcps = [||] }
      { name = "reviewer"; defaultMode = "subagent"; systemPrompt = Prompts.reviewInstructions; defaultMcps = [||] }
      { name = "browser"; defaultMode = "subagent"; systemPrompt = ""; defaultMcps = [| "stealth-browser-mcp" |] }
      { name = "executor"; defaultMode = "subagent"; systemPrompt = ""; defaultMcps = [||] } ]

let private tryFindBuiltinAgent name =
    builtinAgentSpecs |> List.tryFind (fun spec -> spec.name = name)

let private mergeObj (a: obj) (b: obj) : obj =
    let result = emptyObj ()
    Dyn.assignInto result a |> ignore
    Dyn.assignInto result b |> ignore
    result

let private toolDefaultsFor (host: Host) (agentName: string) : obj =
    let o = emptyObj ()
    for name in allToolNames host do
        setKey o name (box (canUseForHost host agentName name))
    o

let private permissionDefaultsFor (host: Host) (agentName: string) : obj =
    let o = emptyObj ()
    for name in allToolNames host do
        setKey o name (box (if canUseForHost host agentName name then "allow" else "deny"))
    o

let private withRoleDefaultsFor (host: Host) (name: string) (userAgent: obj) : obj =
    let spec = tryFindBuiltinAgent name
    let userPrompt = Dyn.str userAgent "prompt"
    let prompt =
        if userPrompt <> "" then userPrompt
        else spec |> Option.map (fun value -> value.systemPrompt) |> Option.defaultValue ""
    let userMode = Dyn.str userAgent "mode"
    let mode =
        if userMode <> "" then userMode
        else spec |> Option.map (fun value -> value.defaultMode) |> Option.defaultValue "subagent"
    let primaryDefaultMode = if defaultPrimaryAliases |> List.contains name then "primary" else "subagent"
    let effectiveMode = if mode <> "" then mode else primaryDefaultMode
    let userPerm = Dyn.get userAgent "permission"
    let userTools = Dyn.get userAgent "tools"
    let userMcps = Dyn.get userAgent "mcps"
    let mcps =
        if Dyn.isNullish userMcps then
            spec
            |> Option.map (fun value -> if value.defaultMcps.Length = 0 then emptyMcps else box value.defaultMcps)
            |> Option.defaultValue emptyMcps
        else
            userMcps

    let perm = mergeObj (permissionDefaultsFor host name) userPerm
    let tools = mergeObj (toolDefaultsFor host name) userTools
    let result = mergeObj (emptyObj ()) userAgent
    setKey result "prompt" (box prompt)
    setKey result "mode" (box effectiveMode)
    setKey result "permission" perm
    setKey result "tools" tools
    setKey result "mcps" mcps
    result

let private objectKeys (o: obj) : string array =
    JS.Constructors.Object.keys(o) |> Seq.toArray

let private applyAgentConfigFor (host: Host) (opencodeConfig: obj) (mcps: obj) : obj =
    let userAgent = if Dyn.isNullish (Dyn.get opencodeConfig "agent") then emptyObj () else Dyn.get opencodeConfig "agent"
    let configMcp = Dyn.get opencodeConfig "mcp"
    let mergedMcp = if Dyn.isNullish configMcp then mcps else mergeObj configMcp mcps
    let agents = mergeObj userAgent (emptyObj ())
    for name in builtinAgentSpecs |> List.map (fun spec -> spec.name) do
        if Dyn.isNullish (Dyn.get agents name) then setKey agents name (emptyObj ())
    let finalAgents = emptyObj ()
    for name in objectKeys agents do
        let ua = Dyn.get agents name
        let uaObj = if Dyn.isNullish ua then emptyObj () else ua
        setKey finalAgents name (withRoleDefaultsFor host name uaObj)
    mergeObj opencodeConfig (box {| agent = finalAgents; mcp = mergedMcp |})

let private dateNow () : int64 = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()

let private loopFooter =
    "- report: a detailed description of what you did and why\n"
    + "- affectedFiles: list of every file you modified or created\n\n"
    + "A reviewer will examine your submission. If accepted, you are done. If rejected, you will receive specific feedback to address."

let private buildLoopMessage (task: string) (intro: string) : string =
    $"Task (loop): {task}\n\n{intro}\n{loopFooter}"

let private ensureParts (output: obj) : obj =
    let parts = Dyn.get output "parts"
    if Dyn.isNullish parts then
        let arr = ResizeArray<obj>()
        setKey output "parts" (box arr)
        box arr
    else
        parts

/// Handle /loop and /loop-review slash commands.
let private commandExecuteBefore (childAgentRegistry: ChildAgentRegistry) (ctx: obj) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore)
    (input: obj) (output: obj) : JS.Promise<unit> =
    async {
        let command = Dyn.str input "command"
        if command = "loop" || command = "loop-review" then
            let sessionID = Dyn.str input "sessionID"
            let task = (Dyn.str input "arguments").Trim()
            let parts = ensureParts output
            clearArray parts
            if task = "" then
                reviewStore.deactivateReview sessionID
                let cancelText = if command = "loop-review" then "loop-review mode cancelled." else "loop mode cancelled."
                pushPart parts (box {| ``type`` = "text"; text = cancelText |})
            elif reviewStore.isReviewActive sessionID then
                pushPart parts (box {| ``type`` = "text"; text = "loop mode is already active. Submit your work via submit_review." |})
            elif command = "loop" then
                reviewStore.activateReview(sessionID, task, dateNow ())
                let msg = buildLoopMessage task "loop mode is active. Complete the task above, then call submit_review with:"
                pushPart parts (box {| ``type`` = "text"; text = msg |})
            else
                let directory = Dyn.str ctx "directory"
                let! result = runReviewerSession childAgentRegistry (Dyn.get ctx "client") reviewStore directory sessionID task |> Async.AwaitPromise
                match result with
                | Accepted ->
                    pushPart parts (box {| ``type`` = "text"; text = $"Pre-review passed. Task \"{task}\" already meets all criteria — no changes needed." |})
                | Terminated ->
                    pushPart parts (box {| ``type`` = "text"; text = "Pre-review could not complete." |})
                | Rejected feedback ->
                    reviewStore.activateReview(sessionID, task, dateNow ())
                    let msg = $"Task (loop-review): {task}\n\n=== Pre-review Feedback ===\n\n{feedback}\n\nAddress the feedback above, then call submit_review with:\n{loopFooter}"
                    pushPart parts (box {| ``type`` = "text"; text = msg |})
    } |> Async.StartAsPromise

/// Register /loop and /loop-review command templates in the opencode config.
let private registerCommands (cfg: obj) : unit =
    let cmd = Dyn.get cfg "command"
    let cmdObj = if Dyn.isNullish cmd then emptyObj () else cmd
    if Dyn.isNullish (Dyn.get cmdObj "loop") then
        setKey cmdObj "loop" (box {| template = "Enable loop mode."; description = "Enable loop mode — the next submission must pass through a reviewer before being accepted" |})
    if Dyn.isNullish (Dyn.get cmdObj "loop-review") then
        setKey cmdObj "loop-review" (box {| template = "Enable while-loop mode with pre-review."; description = "Enable while-loop mode — the task is pre-reviewed immediately, and reviewer feedback is prepended to your prompt before any work begins" |})
    setKey cfg "command" cmdObj

let private twoArgHook (f: obj -> obj -> JS.Promise<unit>) = box (System.Func<obj, obj, JS.Promise<unit>>(f))

let pluginFor (host: Host) (ctx: obj) : JS.Promise<obj> =
    async {
        let reviewStore = VibeFs.Shell.ReviewRuntime.createReviewStore ()
        let childAgentRegistry = ChildAgentRegistry.Create()
        let finderCache = FinderCache()
        let nudgeHook = createNudgeHook host ctx reviewStore childAgentRegistry
        let directory = Dyn.str ctx "directory"
        let magicSession = MagicSession host
        let tools = createTools childAgentRegistry finderCache ctx reviewStore
        let mcps = box {| ``type`` = "local"; command = VibeFs.Kernel.Config.getStealthBrowserMcpLocalConfig(envVar "STEALTH_BROWSER_MCP_REF").command |}
        let mcpMap = box {| ``stealth-browser-mcp`` = mcps |}
        let result = emptyObj ()
        setKey result "id" (box "kunwei")
        setKey result "name" (box "kunwei")
        setKey result "mcp" mcpMap
        setKey result "tool" tools
        setKey result "config" (box (fun (cfg: obj) ->
            (async {
                let next = applyAgentConfigFor host cfg mcpMap
                registerCommands cfg
                return assignInto cfg next
            } |> Async.StartAsPromise)))
        setKey result "chat.message" (twoArgHook (fun input output -> chatMessageFor host childAgentRegistry nudgeHook input output))
        setKey result "tool.definition" (twoArgHook (fun input output -> toolDefinitionFor host input output))
        setKey result "tool.execute.before" (twoArgHook (fun input output -> toolExecuteBeforeFor host input output))
        setKey result "tool.execute.after" (twoArgHook (fun input output -> toolExecuteAfterFor host directory nudgeHook input output))
        setKey result "experimental.chat.messages.transform" (twoArgHook (fun input output -> messagesTransform childAgentRegistry directory magicSession input output))
        setKey result "command.execute.before" (twoArgHook (fun input output ->
            async {
                do! nudgeHook.handleCommandExecuteBefore input output |> Async.AwaitPromise
                do! commandExecuteBefore childAgentRegistry ctx reviewStore input output |> Async.AwaitPromise
            } |> Async.StartAsPromise))
        setKey result "event" (box (fun (input: obj) ->
            async {
                do! Hooks.eventHandler reviewStore input |> Async.AwaitPromise
                do! nudgeHook.handleEvent input |> Async.AwaitPromise
            } |> Async.StartAsPromise))
        setKey result "experimental.session.compacting" (twoArgHook (fun input output -> compactingHandlerFor host magicSession input output))
        return result
    }
    |> Async.StartAsPromise
