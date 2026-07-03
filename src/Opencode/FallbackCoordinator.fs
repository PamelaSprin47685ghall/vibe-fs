module Wanxiangshu.Opencode.FallbackCoordinator

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OpencodeHookInputCodec
open Wanxiangshu.Shell.OpencodeSessionEventCodecCommon
open Wanxiangshu.Shell.FallbackRuntimeState

module Dyn = Wanxiangshu.Shell.Dyn

type FallbackCoordinator
    ( fallbackHandler   : (obj -> JS.Promise<FallbackHookResult>) option
    , fallbackRuntime   : FallbackRuntimeState
    ) =

    member _.TryConsumeEvent(input: obj) : JS.Promise<bool> =
        match fallbackHandler with
        | Some handler ->
            promise {
                let! r = handler input
                return r.Consumed
            }
        | None -> Promise.lift false

    member _.UpdateBusyCount(eventEnvelope: HostEventEnvelope option) : unit =
        match eventEnvelope with
        | Some { EventType = "session.status"; Props = props } ->
            let statusObj = Dyn.get props "status"
            let status =
                let fromStatus = Dyn.str statusObj "status"
                if fromStatus <> "" then fromStatus else Dyn.str statusObj "type"
            let sid = getSessionID "session.status" props
            if sid <> "" && status = "busy" then
                fallbackRuntime.SetBusyCount sid (fallbackRuntime.GetBusyCount sid + 1)
            elif sid <> "" && status = "idle" then
                fallbackRuntime.SetBusyCount sid 0
        | _ -> ()
