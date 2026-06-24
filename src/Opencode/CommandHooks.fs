module VibeFs.Opencode.CommandHooks

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Shell

open VibeFs.Kernel.LoopMessages
open VibeFs.Kernel.ReviewPrompts
open VibeFs.Kernel.ReviewSession
open VibeFs.Opencode.AgentConfig
open VibeFs.Opencode.NudgeEventCodec
open VibeFs.Opencode.ReviewerLoop
open VibeFs.Opencode.KnowledgeGraphRuntime
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Kernel.Domain
open VibeFs.Shell.ToolRuntimeContext
open VibeFs.Shell.OpencodeHookInputCodec
open VibeFs.Shell.Dyn

let private abortOrDeleteEvents =
    set [ "stream-abort"; "session.delete"; "session.close"; "session.remove"; "session.deleted" ]

let cleanUpJobContextIfAbortedOrDeleted (knowledgeGraphRuntime: KnowledgeGraphRuntime) (input: obj) : unit =
    match decodeHostEventEnvelope input with
    | Some { EventType = eventType; Props = props } when Set.contains eventType abortOrDeleteEvents ->
        let sessionID = getSessionID eventType props
        if sessionID <> "" then knowledgeGraphRuntime.DeleteJob(sessionID)
    | _ -> ()

/// Handle /loop and /loop-review slash commands.
let commandExecuteBefore (childAgentRegistry: ChildAgentRegistry) (ctx: obj) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore)
    (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let command = commandNameFromHookInput input
        if command = "loop" || command = "loop-review" then
            let sessionID = sessionIdFromHookInput input ""
            let task = (commandArgumentsFromHookInput input).Trim()
            let parts = ResizeArray<obj>()
            if task = "" then
                reviewStore.deactivateReview sessionID
                parts.Add(box {| ``type`` = "text"; text = loopCancelledMessage |})
            elif reviewStore.isReviewActive sessionID then
                parts.Add(box {| ``type`` = "text"; text = "With-Review Mode is already active. Submit your work via submit_review." |})
            elif command = "loop" then
                reviewStore.activateReview(sessionID, task, Domain.nowMs ())
                let msg = buildLoopMessage task [ "With-Review Mode is active. Complete the task above, then call submit_review with:" ]
                parts.Add(box {| ``type`` = "text"; text = msg |})
            else
                let directory = pluginDirectoryFromCtx ctx
                let! result = runReviewerSession childAgentRegistry (Dyn.get ctx "client") reviewStore directory sessionID task
                match result with
                | Accepted ->
                    parts.Add(box {| ``type`` = "text"; text = $"Pre-review passed. Task \"{task}\" already meets all criteria — no changes needed." |})
                | Terminated ->
                    parts.Add(box {| ``type`` = "text"; text = "Pre-review could not complete." |})
                | Rejected feedback ->
                    reviewStore.activateReview(sessionID, task, Domain.nowMs ())
                    let msg = buildLoopMessage task [ "=== Pre-review Feedback ==="; ""; feedback; ""; "Address the feedback above, then call submit_review with:" ]
                    parts.Add(box {| ``type`` = "text"; text = msg |})
            setKey output "parts" (box parts)
    }

/// Register /loop and /loop-review command templates in the opencode config.
let registerCommands (cfg: obj) : unit =
    let cmd = Dyn.get cfg "command"
    let cmdObj = if Dyn.isNullish cmd then emptyObj () else cmd
    if Dyn.isNullish (Dyn.get cmdObj "loop") then
        setKey cmdObj "loop" (box {| template = withReviewCommandTemplate; description = "Enable With-Review Mode — the next submission must pass through a reviewer before being accepted" |})
    if Dyn.isNullish (Dyn.get cmdObj "loop-review") then
        setKey cmdObj "loop-review" (box {| template = withReviewPrecheckCommandTemplate; description = "Enable With-Review Mode with pre-review — the task is pre-reviewed immediately, and reviewer feedback is prepended to your prompt before any work begins" |})
    setKey cfg "command" cmdObj