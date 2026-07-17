module Wanxiangshu.Hosts.Opencode.CommandHooks

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime

open Wanxiangshu.Runtime.LoopMessages
open Wanxiangshu.Kernel.ToolCopy
open Wanxiangshu.Kernel.ReviewSession
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Hosts.Opencode.AgentConfig
open Wanxiangshu.Hosts.Opencode.ReviewerLoop
open Wanxiangshu.Runtime.Clock
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.OpencodeSessionEventCodec
open Wanxiangshu.Runtime.EventLogRuntime
open Wanxiangshu.Runtime.RuntimeScope

/// Run the reviewer session for a /loop-review command and return the raw
/// ReviewResult (side-effects for NeedsRevision are applied by the caller).
let private handleLoopReviewCommand
    (childAgentRegistry: ChildAgentRegistry)
    (reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore)
    (directory: string)
    (sessionID: string)
    (task: string)
    (ctx: obj)
    : JS.Promise<ReviewResult> =
    promise {
        let! result =
            match getClientFromPluginCtx ctx with
            | Error _ -> Promise.lift Terminated
            | Ok client -> runReviewerSession childAgentRegistry client reviewStore directory sessionID task

        return result
    }

/// Build the response text part for a given ReviewResult.
let private formatReviewResultMessage (task: string) (result: ReviewResult) : obj =
    match result with
    | Accepted _ ->
        box
            {| ``type`` = "text"
               text = preReviewPassedMessage task |}
    | Terminated ->
        box
            {| ``type`` = "text"
               text = preReviewCouldNotComplete |}
    | NeedsRevision feedback ->
        let msg =
            buildLoopMessage
                task
                [ withReviewPreReviewFeedbackHeader
                  ""
                  feedback
                  ""
                  "Address the feedback above, then call submit_review with:" ]

        box {| ``type`` = "text"; text = msg |}

/// Execute /loop (activate, cancel, or show active) given the shared setup
/// already performed (directory resolved, scope initialised).
let private handleLoopCommand
    (childAgentRegistry: ChildAgentRegistry)
    (ctx: obj)
    (reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore)
    (directory: string)
    (sessionID: string)
    (task: string)
    (command: string)
    (parts: ResizeArray<obj>)
    : JS.Promise<unit> =
    promise {
        if task = "" then
            do! appendLoopCancelledOrFail directory sessionID
            do! syncReviewFromEventLogDedicated reviewStore directory sessionID

            parts.Add(
                box
                    {| ``type`` = "text"
                       text = loopCancelledMessage |}
            )
        elif (reviewStore.getReviewTask sessionID).IsSome then
            parts.Add(
                box
                    {| ``type`` = "text"
                       text = reviewAlreadyActiveMessage |}
            )
        elif command = "loop" then
            do! appendLoopActivatedOrFail directory sessionID task
            do! syncReviewFromEventLogDedicated reviewStore directory sessionID

            let msg =
                buildLoopMessage
                    task
                    [ "With-Review Mode is active. Complete the task above, then call submit_review with:" ]

            parts.Add(box {| ``type`` = "text"; text = msg |})
        else
            // command = "loop-review" when task is non-empty and no active review
            let! result = handleLoopReviewCommand childAgentRegistry reviewStore directory sessionID task ctx

            // NeedsRevision re-activates the loop so the worker can address feedback.
            match result with
            | NeedsRevision _ ->
                do! appendLoopActivatedOrFail directory sessionID task
                do! syncReviewFromEventLogDedicated reviewStore directory sessionID
            | Accepted _ -> ()
            | Terminated -> ()

            parts.Add(formatReviewResultMessage task result)
    }

/// Handle /loop and /loop-review slash commands.
let commandExecuteBefore
    (childAgentRegistry: ChildAgentRegistry)
    (ctx: obj)
    (reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore)
    (scope: RuntimeScope)
    (input: obj)
    (output: obj)
    : JS.Promise<unit> =
    promise {
        let command = commandNameFromHookInput input

        if command = "loop" || command = "loop-review" then
            let sessionID = sessionIdFromHookInput input ""
            let task = (commandArgumentsFromHookInput input).Trim()
            let parts = ResizeArray<obj>()
            let directory = pluginDirectoryFromCtx ctx
            scope.TriggerInit(directory)
            do! scope.WaitInit()

            do! handleLoopCommand childAgentRegistry ctx reviewStore directory sessionID task command parts

            setHookParts output (box parts)
    }

/// Register /loop and /loop-review command templates in the opencode config.
/// Delegates to OpencodeHookInputCodec.registerLoopReviewCommands.
let registerCommands (cfg: obj) : unit = registerLoopReviewCommands cfg
