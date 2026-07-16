module Wanxiangshu.Hosts.Opencode.Fallback.Coordinator

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Runtime.OpencodeHostEvent
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.GateTransitions

module Dyn = Wanxiangshu.Runtime.Dyn

type FallbackCoordinator
    (fallbackHandler: (obj -> JS.Promise<FallbackHookResult>) option, fallbackRuntime: FallbackRuntimeStore) =

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
        | Some { EventType = "session.status"
                 Props = props } ->
            let statusObj = Dyn.get props "status"
            let status = resolveStatusValue statusObj
            let sid = getSessionID "session.status" props

            if sid <> "" && status = "busy" then
                fallbackRuntime.SetBusyCount sid (fallbackRuntime.GetBusyCount sid + 1)
            elif sid <> "" && status = "idle" then
                fallbackRuntime.SetBusyCount sid 0
        | _ -> ()
