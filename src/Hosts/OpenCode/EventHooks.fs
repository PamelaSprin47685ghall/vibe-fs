module Wanxiangshu.Hosts.Opencode.EventHooks

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Runtime.OpencodeSessionEventCodec
open Wanxiangshu.Runtime.EventLogRuntime
open Wanxiangshu.Runtime.ToolRuntimeContext

let eventHandler
    (reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore)
    (scope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
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
            Wanxiangshu.Runtime.RunnerBackground.abortRunnerJobCore scope sessionID
        | _ -> ()
    }
