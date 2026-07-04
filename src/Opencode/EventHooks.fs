module Wanxiangshu.Opencode.EventHooks

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Shell
open Wanxiangshu.Shell.OpencodeHookInputCodec
open Wanxiangshu.Shell.OpencodeSessionEventCodec

let eventHandler (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore) (scope: Wanxiangshu.Shell.RuntimeScope.RuntimeScope) (input: obj) : JS.Promise<unit> =
    promise {
        match decodeHostEventEnvelope input with
        | Some { EventType = "stream-abort"; Props = props } ->
            let sessionID =
                let s = getSessionID "stream-abort" props
                if s = "" then "loop" else s
            reviewStore.deactivateReview sessionID
            Wanxiangshu.Shell.RunnerBackground.abortRunnerJobCore scope sessionID
        | _ -> ()
    }