module Wanxiangshu.Opencode.EventHooks

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Shell
open Wanxiangshu.Shell.OpencodeHookInputCodec
open Wanxiangshu.Shell.OpencodeSessionEventCodec
open Wanxiangshu.Shell.EventLogRuntime
open Wanxiangshu.Shell.ToolRuntimeContext

let eventHandler
    (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore)
    (scope: Wanxiangshu.Shell.RuntimeScope.RuntimeScope)
    (ctx: obj)
    (input: obj)
    : JS.Promise<unit> =
    promise {
        match decodeHostEventEnvelope input with
        | Some { EventType = "stream-abort"
                 Props = props } ->
            let sessionID =
                let s = getSessionID "stream-abort" props
                if s = "" then "loop" else s

            let directory = pluginDirectoryFromCtx ctx
            scope.TriggerInit(directory)
            do! scope.WaitInit()
            do! appendLoopCancelledOrFail directory sessionID
            do! syncReviewFromEventLogDedicated reviewStore directory sessionID
            Wanxiangshu.Shell.RunnerBackground.abortRunnerJobCore scope sessionID
        | _ -> ()
    }
