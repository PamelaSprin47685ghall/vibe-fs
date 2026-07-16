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
            let activeTask = reviewStore.getReviewTask sessionID

            if task = "" then
                do! appendLoopCancelledOrFail directory sessionID
                do! syncReviewFromEventLogDedicated reviewStore directory sessionID

                parts.Add(
                    box
                        {| ``type`` = "text"
                           text = loopCancelledMessage |}
                )
            elif activeTask.IsSome then
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
                let! result =
                    match getClientFromPluginCtx ctx with
                    | Error _ -> Promise.lift Terminated
                    | Ok client -> runReviewerSession childAgentRegistry client reviewStore directory sessionID task

                match result with
                | Accepted _ ->
                    parts.Add(
                        box
                            {| ``type`` = "text"
                               text = preReviewPassedMessage task |}
                    )
                | Terminated ->
                    parts.Add(
                        box
                            {| ``type`` = "text"
                               text = preReviewCouldNotComplete |}
                    )
                | NeedsRevision feedback ->
                    do! appendLoopActivatedOrFail directory sessionID task
                    do! syncReviewFromEventLogDedicated reviewStore directory sessionID

                    let msg =
                        buildLoopMessage
                            task
                            [ withReviewPreReviewFeedbackHeader
                              ""
                              feedback
                              ""
                              "Address the feedback above, then call submit_review with:" ]

                    parts.Add(box {| ``type`` = "text"; text = msg |})

            setHookParts output (box parts)
    }

/// Register /loop and /loop-review command templates in the opencode config.
/// Delegates to OpencodeHookInputCodec.registerLoopReviewCommands.
let registerCommands (cfg: obj) : unit = registerLoopReviewCommands cfg
