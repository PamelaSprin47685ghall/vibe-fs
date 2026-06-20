module VibeFs.Opencode.CommandHooks

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.LoopMessages
open VibeFs.Kernel.ReviewSession
open VibeFs.Opencode.AgentConfig
open VibeFs.Opencode.ReviewerLoop
open VibeFs.Opencode.WikiRuntime
open VibeFs.Shell.ChildAgentRegistry

let private getEventAssistantText (event: obj) : string =
    let properties = Dyn.get event "properties"
    VibeFs.Kernel.NudgeState.getPartsText (Dyn.get properties "parts")

let flushDirectWriteTurnIfCompleted (wikiRuntime: WikiRuntime) (input: obj) : unit =
    let event = Dyn.get input "event"
    if Dyn.str event "type" = "message.updated" then
        let properties = Dyn.get event "properties"
        let info = Dyn.get properties "info"
        if VibeFs.Kernel.NudgeState.isCompletedAssistantMessage info then
            let sessionID =
                let fromProps = Dyn.str properties "sessionID"
                if fromProps <> "" then fromProps else Dyn.str info "sessionID"
            if sessionID <> "" then
                wikiRuntime.FlushTurnIfNeeded(sessionID, getEventAssistantText event)

let cleanUpJobContextIfAbortedOrDeleted (wikiRuntime: WikiRuntime) (input: obj) : unit =
    let event = Dyn.get input "event"
    let eventType = Dyn.str event "type"
    if eventType = "stream-abort" || eventType = "session.delete" || eventType = "session.close" || eventType = "session.remove" || eventType = "session.deleted" then
        let rawProps = Dyn.get event "properties"
        let props = if Dyn.isNullish rawProps then event else rawProps
        let sessionID = VibeFs.Kernel.NudgeState.getSessionID eventType props
        if sessionID <> "" then
            wikiRuntime.DeleteJob(sessionID)

let private dateNow () : int64 = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()

/// Handle /loop and /loop-review slash commands.
let commandExecuteBefore (childAgentRegistry: ChildAgentRegistry) (ctx: obj) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore)
    (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let command = Dyn.str input "command"
        if command = "loop" || command = "loop-review" then
            let sessionID = Dyn.str input "sessionID"
            let task = (Dyn.str input "arguments").Trim()
            let parts = ResizeArray<obj>()
            if task = "" then
                reviewStore.deactivateReview sessionID
                parts.Add(box {| ``type`` = "text"; text = cancelledMarker |})
            elif reviewStore.isReviewActive sessionID then
                parts.Add(box {| ``type`` = "text"; text = "With-Review Mode is already active. Submit your work via submit_review." |})
            elif command = "loop" then
                reviewStore.activateReview(sessionID, task, dateNow ())
                let msg = buildLoopMessage task [ "With-Review Mode is active. Complete the task above, then call submit_review with:" ]
                parts.Add(box {| ``type`` = "text"; text = msg |})
            else
                let directory = Dyn.str ctx "directory"
                let! result = runReviewerSession childAgentRegistry (Dyn.get ctx "client") reviewStore directory sessionID task
                match result with
                | Accepted ->
                    parts.Add(box {| ``type`` = "text"; text = $"Pre-review passed. Task \"{task}\" already meets all criteria — no changes needed." |})
                | Terminated ->
                    parts.Add(box {| ``type`` = "text"; text = "Pre-review could not complete." |})
                | Rejected feedback ->
                    reviewStore.activateReview(sessionID, task, dateNow ())
                    let msg = buildLoopMessage task [ "=== Pre-review Feedback ==="; ""; feedback; ""; "Address the feedback above, then call submit_review with:" ]
                    parts.Add(box {| ``type`` = "text"; text = msg |})
            setKey output "parts" (box parts)
    }

/// Register /loop and /loop-review command templates in the opencode config.
let registerCommands (cfg: obj) : unit =
    let cmd = Dyn.get cfg "command"
    let cmdObj = if Dyn.isNullish cmd then emptyObj () else cmd
    if Dyn.isNullish (Dyn.get cmdObj "loop") then
        setKey cmdObj "loop" (box {| template = "Enable With-Review Mode."; description = "Enable With-Review Mode — the next submission must pass through a reviewer before being accepted" |})
    if Dyn.isNullish (Dyn.get cmdObj "loop-review") then
        setKey cmdObj "loop-review" (box {| template = "Enable With-Review Mode with pre-review."; description = "Enable With-Review Mode with pre-review — the task is pre-reviewed immediately, and reviewer feedback is prepended to your prompt before any work begins" |})
    setKey cfg "command" cmdObj
