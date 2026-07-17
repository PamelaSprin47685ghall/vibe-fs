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

/// Execute /loop (activate, cancel, or show active) given the shared setup
/// already performed (directory resolved, scope initialised).
let private handleLoopCommand
    (childAgentRegistry: ChildAgentRegistry)
    (ctx: obj)
    (reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore)
    (directory: string)
    (sessionID: string)
    (task: string)
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
        else
            do! appendLoopActivatedOrFail directory sessionID task
            do! syncReviewFromEventLogDedicated reviewStore directory sessionID

            let msg =
                buildLoopMessage
                    task
                    [ "With-Review Mode is active. Complete the task above, then call submit_review with:" ]

            parts.Add(box {| ``type`` = "text"; text = msg |})
    }

/// Handle /loop slash command.
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

        if command = "loop" then
            let sessionID = sessionIdFromHookInput input ""
            let task = (commandArgumentsFromHookInput input).Trim()
            let parts = ResizeArray<obj>()
            let directory = pluginDirectoryFromCtx ctx
            scope.TriggerInit(directory)
            do! scope.WaitInit()

            do! handleLoopCommand childAgentRegistry ctx reviewStore directory sessionID task parts

            setHookParts output (box parts)
    }

/// Register /loop command template in the opencode config.
let registerCommands (cfg: obj) : unit = registerLoopCommand cfg
