module VibeFs.Opencode.Hooks

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.OpencodeHooks
open VibeFs.Kernel.ToolPolicy
open VibeFs.Kernel.TreeSitterKernel
open VibeFs.Opencode.ChildAgent
open VibeFs.Opencode.HookSchema
open VibeFs.Shell.TreeSitterShell

let private emptyObj () : obj = createObj []

let private setKey (o: obj) (k: string) (v: obj) : unit = o?(k) <- v
let private setOutput (o: obj) (v: string) : unit = o?output <- v
let private resolvedUnit : JS.Promise<unit> = async { return () } |> Async.StartAsPromise

let private objectKeys (o: obj) : string array =
    JS.Constructors.Object.keys(o) |> Seq.toArray

let private replaceArrayInPlace (target: obj array) (source: obj array) : unit =
    if obj.ReferenceEquals(target, source) then ()
    else
        let targetObj = box target
        targetObj?length <- 0
        for item in source do
            targetObj?push(item) |> ignore

let private jsTypeof (o: obj) : string = Dyn.jsType o

let private resolveAgent (registry: ChildAgentRegistry) (input: obj) : string =
    let explicit = Dyn.str input "agent"
    if explicit <> "" then explicit
    else
        match registry.LookupChildAgent(Dyn.str input "sessionID") with
        | Some a -> a
        | None -> "manager"

/// Filter tools at runtime by `canUse`.  Denied tools are forced false;
/// allowed tools keep their existing value so users may opt out.
let private resolveChatTools (agent: string) (existingTools: obj) : obj =
    let next = createObj []
    if not (Dyn.isNullish existingTools) then
        for key in objectKeys existingTools do
            if canUse agent key then
                setKey next key (Dyn.get existingTools key)
            else
                setKey next key (box false)
    next

/// chat.message: enforce per-agent tool boundaries at runtime.
let chatMessage (registry: ChildAgentRegistry) (nudgeHook: VibeFs.Opencode.NudgeHook.NudgeHook) (input: obj) (output: obj) : JS.Promise<unit> =
    async {
        let agent = resolveAgent registry input
        let sessionID = Dyn.str input "sessionID"
        do! nudgeHook.handleChatMessage(sessionID, agent, Dyn.get output "parts") |> Async.AwaitPromise
        let message = Dyn.get output "message"
        if not (Dyn.isNullish message) then
            let tools = Dyn.get message "tools"
            if not (Dyn.isNullish tools) then
                setKey message "tools" (resolveChatTools agent tools)
    } |> Async.StartAsPromise

/// tool.execute.after: syntax-check file edits and delegate to the nudge hook.
let toolExecuteAfter (directory: string) (nudgeHook: VibeFs.Opencode.NudgeHook.NudgeHook) (input: obj) (output: obj) : JS.Promise<unit> =
    async {
        let tool = Dyn.str input "tool"
        if isFileEditTool tool then
            let out = Dyn.get output "output"
            if not (Dyn.isNullish out) && Dyn.typeIs out "string" then
                let s = string out
                if not (hasSyntaxCheckMarker s) then
                    let args = Dyn.get input "args"
                    let paths = extractFilePaths args
                    let! diagnostics =
                        paths
                        |> List.map (fun path -> readAndCheckSyntax path directory false |> Async.AwaitPromise)
                        |> Async.Parallel
                    let formatted =
                        let lines = ResizeArray<string>()
                        for diagnostic in diagnostics do
                            match diagnostic with
                            | Some text -> lines.Add text
                            | None -> ()
                        String.concat "\n" lines
                    if formatted <> "" then setOutput output (s + "\n\n" + formatted)
        do! nudgeHook.handleToolExecuteAfter input output |> Async.AwaitPromise
    } |> Async.StartAsPromise

open VibeFs.Kernel.Dedup

let private applyReadDedup (messages: obj array) : unit =
    if Dyn.isNullish messages || not (Dyn.isArray messages) then ()
    else
        let seenByPath = emptyObj ()

        for i = 0 to messages.Length - 1 do
            let message = messages.[i]
            if not (Dyn.isNullish message) then
                let parts = Dyn.get message "parts"
                if not (Dyn.isNullish parts) && Dyn.isArray parts then
                    let partsArr = parts :?> obj array

                    for j = 0 to partsArr.Length - 1 do
                        let part = partsArr.[j]
                        if not (Dyn.isNullish part)
                           && Dyn.str part "type" = "tool"
                           && Dyn.str part "tool" = "read" then
                            let state = Dyn.get part "state"
                            if not (Dyn.isNullish state) then
                                let output = Dyn.get state "output"
                                if not (Dyn.isNullish output) && Dyn.typeIs output "string" then
                                    let currentOutput = string output
                                    let pathKey =
                                        match extractFilePaths (Dyn.get state "input") with
                                        | path :: _ -> path
                                        | [] -> ""
                                    let payload = { path = pathKey; content = currentOutput }
                                    let pathState =
                                        let existing = Dyn.get seenByPath pathKey
                                        if Dyn.isNullish existing then { seenContents = [] }
                                        else unbox<DedupState> existing
                                    let verdict, nextState = processDedup pathState payload
                                    setKey seenByPath pathKey (box nextState)
                                    match verdict with
                                    | AlreadySeen -> setOutput state dedupMarker
                                    | NewContent _ -> ()

let messagesTransform (registry: ChildAgentRegistry) (directory: string) (input: obj) (output: obj) : JS.Promise<unit> =
    async {
        let messages = Dyn.get output "messages"
        if not (Dyn.isNullish messages) && Dyn.isArray messages then
            let messagesArr = messages :?> obj array
            let agent = resolveAgent registry input
            if not (defaultExcludedAgents |> List.contains agent) then
                let! capsFiles = VibeFs.Shell.CapsShell.findCapsFiles directory |> Async.AwaitPromise
                let next =
                    VibeFs.Kernel.CapsFormat.buildCapsMessages
                        VibeFs.Shell.Crypto.sha256HexTruncated
                        messagesArr
                        directory
                        defaultExcludedAgents
                        capsFiles
                replaceArrayInPlace messagesArr next
            applyReadDedup messagesArr
    } |> Async.StartAsPromise

/// tool.definition: hide the internal `_ui` parameter from coder/reader schemas.
let toolDefinition (input: obj) (output: obj) : JS.Promise<unit> =
    async {
        let toolID = Dyn.str input "toolID"
        if toolID = "coder" || toolID = "reader" then
            let parameters = Dyn.get output "parameters"
            if not (Dyn.isNullish parameters) then setKey output "parameters" (withUiParameterStripped parameters)
    } |> Async.StartAsPromise

/// tool.execute.before: populate `_ui` from joined intents so the UI labels the call.
let toolExecuteBefore (input: obj) (output: obj) : JS.Promise<unit> =
    async {
        let args = Dyn.get output "args"
        if Dyn.isNullish args then ()
        else
            let tool = Dyn.str input "tool"
            setUiLabel args tool
    } |> Async.StartAsPromise

/// event: deactivate review on stream-abort.
let eventHandler (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) (input: obj) : JS.Promise<unit> =
    async {
        let event = Dyn.get input "event"
        if Dyn.str event "type" = "stream-abort" then
            let props = Dyn.get event "properties"
            let sessionID =
                if Dyn.isNullish props then "loop"
                else
                    let s = Dyn.str props "sessionID"
                    if s = "" then "loop" else s
            reviewStore.deactivateReview sessionID
    } |> Async.StartAsPromise

let noop (_a: obj) (_b: obj) : JS.Promise<unit> = resolvedUnit
let noopEvent (_a: obj) : JS.Promise<unit> = resolvedUnit
