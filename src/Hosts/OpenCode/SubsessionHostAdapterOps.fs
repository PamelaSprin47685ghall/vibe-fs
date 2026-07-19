module Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterOps

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Dispatch.Identity
open Wanxiangshu.Kernel.Dispatch.Protocol
open Wanxiangshu.Runtime.Dispatch
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.Messaging.OpencodeSessionEventCodec
open Wanxiangshu.Hosts.Opencode.SubsessionDispatch
open Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes

module Dyn = Wanxiangshu.Runtime.Dyn

/// Build the send-prompt function for a specific subsession turn.
/// The returned function matches the `DispatchIdentity -> JS.Promise<...>`
/// shape expected by `SessionDispatcher.Dispatch`.
let buildSendPrompt
    (client: obj)
    (agent: string)
    (_directory: string)
    (sessionId: SessionId)
    (turn: TurnPlan)
    : DispatchIdentity -> JS.Promise<DispatchAcceptance> =
    fun (_: DispatchIdentity) ->
        promise {
            match trySessionApi client with
            | Error msg ->
                // The SessionDispatcher will catch the rejected Promise and
                // resolve the per-dispatch HostReceiptWaiter as TransportUnavailable.
                return! Promise.reject (System.Exception("opencode_session_api_missing:" + msg))
            | Ok session ->
                let body = buildBody agent turn.Model turn.Prompt turn.TurnId

                let arg =
                    box
                        {| path = box {| id = SessionId.value sessionId |}
                           body = body |}

                let! response = invoke1 arg "prompt" session
                let mid = decodeResponseForUserMessageId response

                match mid with
                | Some id when id <> "" ->
                    // The SessionDispatcher's acceptRecord path resolves the
                    // HostReceiptWaiter to `UserMessageObserved id`.
                    return UserMessageAccepted id
                | _ ->
                    // OpaqueAccepted: no host user-message id to bind.
                    // acceptRecord maps this to `OrderedTurnMarkerObserved`.
                    return OpaqueAccepted("opencode:" + TurnId.value turn.TurnId)
        }

let private tryFetchMessages (client: obj) (sid: string) : JS.Promise<obj array option> =
    promise {
        match trySessionApi client with
        | Ok session ->
            try
                let! resp = invoke1 (box {| path = box {| id = sid |} |}) "messages" session
                let data = Dyn.get resp "data"

                if Dyn.isNullish data || not (Dyn.isArray data) then
                    return None
                else
                    return Some(unbox<obj array> data)
            with _ ->
                return None
        | Error _ -> return None
    }

let private findMessageIdByNonce (nonce: string) (msgs: obj array) : string option =
    msgs
    |> Array.tryFind (isMessageMatch nonce)
    |> Option.map (fun m -> Dyn.str m "id")

let private dispatchStatusOfWaiter (w: HostReceiptWaiter) : DispatchStatus =
    match w.TransportState with
    | HostReceiptWaiterTransportState.ReceiptResolved receipt -> DispatchStatus.Accepted receipt
    | HostReceiptWaiterTransportState.BeforeSendRejected err -> TransportRejectedBeforeSend err
    | HostReceiptWaiterTransportState.AfterSendUnknown err -> TransportFailedAfterUnknownAcceptance err
    | HostReceiptWaiterTransportState.ReceiptRejected(HostRejected err) -> TransportRejectedBeforeSend err
    | HostReceiptWaiterTransportState.UserCancelled -> TransportRejectedBeforeSend HostReceiptWaiter.cancelError
    | HostReceiptWaiterTransportState.ReceiptRejected(HostAcceptanceUnknown err) ->
        TransportFailedAfterUnknownAcceptance err
    | HostReceiptWaiterTransportState.InFlight -> StillPending

/// Build the query-dispatch-status function for a specific subsession turn.
/// The returned function is `unit -> JS.Promise<...>` so the caller can
/// hold it as a closure and invoke it at the right moment.
let buildQueryDispatchStatus
    (client: obj)
    (directory: string)
    (sessionId: SessionId)
    (turnId: TurnId)
    : unit -> JS.Promise<DispatchStatus> =
    fun () ->
        promise {
            let dispatcher = getDispatcher directory (SessionId.value sessionId)
            let nonce = TurnId.value turnId
            let ws = workspaceFor directory
            let sid = SessionId.value sessionId

            match dispatcher.ActiveLogicalTurnId with
            | Some active when active = nonce ->
                match! tryFetchMessages client sid with
                | None -> return StillPending
                | Some msgs ->
                    match findMessageIdByNonce nonce msgs with
                    | Some mid -> return DispatchStatus.Accepted(UserMessageObserved mid)
                    | None -> return StillPending
            | _ ->
                match HostReceiptWaiterRegistry.tryFind ws sid nonce with
                | Some w when w.Completed -> return dispatchStatusOfWaiter w
                | Some _ -> return StillPending
                | None -> return Unknown
        }

let private quiescenceOfMessages (nonce: string) (msgs: obj array) : QuiescenceStatus =
    let target = msgs |> Array.filter (isMessageMatch nonce)

    if target |> Array.exists isMessageActive then StillRunning
    elif target.Length > 0 then Stopped
    else StopUnknown

/// Build the query-session-quiescence function for a specific subsession turn.
let buildQuerySessionQuiescence
    (client: obj)
    (sessionId: SessionId)
    (turnId: TurnId)
    : unit -> JS.Promise<QuiescenceStatus> =
    fun () ->
        promise {
            let nonce = TurnId.value turnId
            let sid = SessionId.value sessionId

            match! tryFetchMessages client sid with
            | None -> return StopUnknown
            | Some msgs -> return quiescenceOfMessages nonce msgs
        }
