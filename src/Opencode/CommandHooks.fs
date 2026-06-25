module VibeFs.Opencode.CommandHooks

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Shell

open VibeFs.Kernel.LoopMessages
open VibeFs.Kernel.ToolCopy
open VibeFs.Kernel.ReviewSession
open VibeFs.Opencode.AgentConfig
open VibeFs.Opencode.NudgeEventCodec
open VibeFs.Opencode.ReviewerLoop
open VibeFs.Opencode.KnowledgeGraphRuntime
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Kernel.Domain
open VibeFs.Shell.ToolRuntimeContext
open VibeFs.Shell.OpencodeHookInputCodec
open VibeFs.Shell.OpencodeClientCodec

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
                parts.Add(box {| ``type`` = "text"; text = reviewAlreadyActiveMessage |})
            elif command = "loop" then
                reviewStore.activateReview(sessionID, task, Domain.nowMs ())
                let msg = buildLoopMessage task [ "With-Review Mode is active. Complete the task above, then call submit_review with:" ]
                parts.Add(box {| ``type`` = "text"; text = msg |})
            else
                let directory = pluginDirectoryFromCtx ctx
                let! result =
                    match getClientFromPluginCtx ctx with
                    | Error _ -> Promise.lift Terminated
                    | Ok client -> runReviewerSession childAgentRegistry client reviewStore directory sessionID task
                match result with
                | Accepted ->
                    parts.Add(box {| ``type`` = "text"; text = preReviewPassedMessage task |})
                | Terminated ->
                    parts.Add(box {| ``type`` = "text"; text = preReviewCouldNotComplete |})
                | Rejected feedback ->
                    reviewStore.activateReview(sessionID, task, Domain.nowMs ())
                    let msg = buildLoopMessage task [ withReviewPreReviewFeedbackHeader; ""; feedback; ""; "Address the feedback above, then call submit_review with:" ]
                    parts.Add(box {| ``type`` = "text"; text = msg |})
            setHookParts output (box parts)
    }

/// Register /loop and /loop-review command templates in the opencode config.
/// Delegates to OpencodeHookInputCodec.registerLoopReviewCommands.
let registerCommands (cfg: obj) : unit = registerLoopReviewCommands cfg