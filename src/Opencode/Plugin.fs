module VibeFs.Opencode.Plugin

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Opencode.Tools
open VibeFs.Opencode.Hooks
open VibeFs.Opencode.NudgeHook
open VibeFs.Opencode.Session
open VibeFs.Opencode.AgentConfig

[<Emit("{}")>]
let private emptyObj () : obj = jsNative
[<Emit("$0[$1] = $2")>]
let private setKey (o: obj) (k: string) (v: obj) : unit = jsNative
[<Emit("Object.assign($0, $1)")>]
let private assignInto (target: obj) (source: obj) : obj = jsNative
[<Emit("$0.length = 0")>]
let private clearArray (arr: obj) : unit = jsNative
[<Emit("$0.push($1)")>]
let private pushPart (arr: obj) (part: obj) : unit = jsNative

[<Emit("Date.now()")>]
let private dateNow () : int = jsNative

let private loopFooter =
    "- report: a detailed description of what you did and why\n"
    + "- affectedFiles: list of every file you modified or created\n\n"
    + "A reviewer will examine your submission. If accepted, you are done. If rejected, you will receive specific feedback to address."

let private buildLoopMessage (task: string) (intro: string) : string =
    $"Task (loop): {task}\n\n{intro}\n{loopFooter}"

/// Handle /loop and /loop-review slash commands.
let private commandExecuteBefore (ctx: obj) (reviewStore: VibeFs.Kernel.ReviewRuntime.ReviewStore)
    (input: obj) (output: obj) : JS.Promise<unit> =
    async {
        let command = Dyn.str input "command"
        if command = "loop" || command = "loop-review" then
            let sessionID = Dyn.str input "sessionID"
            let task = (Dyn.str input "arguments").Trim()
            let parts = Dyn.get output "parts"
            if not (Dyn.isNullish parts) then clearArray parts
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
                let! result = runReviewerSession (Dyn.get ctx "client") reviewStore directory sessionID task |> Async.AwaitPromise
                match result with
                | ReviewSession.Accepted ->
                    pushPart parts (box {| ``type`` = "text"; text = $"Pre-review passed. Task \"{task}\" already meets all criteria — no changes needed." |})
                | ReviewSession.Terminated ->
                    pushPart parts (box {| ``type`` = "text"; text = "Pre-review could not complete." |})
                | ReviewSession.Rejected feedback ->
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

/// The opencode plugin entry point: `async (ctx) => Plugin`.
[<ExportDefault>]
let plugin (ctx: obj) : JS.Promise<obj> =
    async {
        let reviewStore = VibeFs.Kernel.ReviewRuntime.createReviewStore ()
        let nudgeHook = createNudgeHook ctx reviewStore
        let directory = Dyn.str ctx "directory"
        let tools = createTools ctx reviewStore
        let mcps = box {| ``type`` = "local"; command = VibeFs.Kernel.McpConfig.getStealthBrowserMcpLocalConfig().command |}
        let mcpMap = box {| ``stealth-browser-mcp`` = mcps |}
        let result = emptyObj ()
        setKey result "name" (box "kunwei")
        setKey result "mcp" mcpMap
        setKey result "tool" tools
        setKey result "config" (box (fun (cfg: obj) ->
            (async {
                let next = applyAgentConfig cfg mcpMap
                registerCommands cfg
                return assignInto cfg next
            } |> Async.StartAsPromise)))
        setKey result "chat.message" (box (fun (input: obj) (output: obj) -> chatMessage input output))
        setKey result "tool.definition" (box (fun (input: obj) (output: obj) -> toolDefinition input output))
        setKey result "tool.execute.before" (box (fun (input: obj) (output: obj) -> toolExecuteBefore input output))
        setKey result "tool.execute.after" (box (fun (input: obj) (output: obj) -> toolExecuteAfter input output))
        setKey result "experimental.chat.messages.transform" (box (fun (_input: obj) (output: obj) -> messagesTransform directory output))
        setKey result "command.execute.before" (box (fun (input: obj) (output: obj) -> commandExecuteBefore ctx reviewStore input output))
        setKey result "event" (box (fun (input: obj) ->
            async {
                do! Hooks.eventHandler reviewStore input |> Async.AwaitPromise
                do! nudgeHook.handleEvent input |> Async.AwaitPromise
            } |> Async.StartAsPromise))
        return result
    }
    |> Async.StartAsPromise
