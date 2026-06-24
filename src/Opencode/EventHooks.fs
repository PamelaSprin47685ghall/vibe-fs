module VibeFs.Opencode.EventHooks

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Shell
open VibeFs.Shell.OpencodeHookInputCodec
open VibeFs.Opencode.NudgeEventCodec

let eventHandler (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) (input: obj) : JS.Promise<unit> =
    promise {
        match decodeHostEventEnvelope input with
        | Some { EventType = "stream-abort"; Props = props } ->
            let sessionID =
                let s = getSessionID "stream-abort" props
                if s = "" then "loop" else s
            reviewStore.deactivateReview sessionID
        | _ -> ()
    }