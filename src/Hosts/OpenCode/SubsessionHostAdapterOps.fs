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
open Wanxiangshu.Hosts.OpenCode.OpencodeSessionEventCodec
open Wanxiangshu.Hosts.Opencode.SubsessionDispatch
open Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes

/// Build the send-prompt function for a specific subsession turn.
/// The returned function matches the `DispatchIdentity -> JS.Promise<…>`
/// shape expected by `SessionDispatcher.Dispatch`.
let buildSendPrompt
    (client: obj)
    (agent: string)
    (directory: string)
    (sessionId: SessionId)
    (turn: TurnPlan)
    : DispatchIdentity -> JS.Promise<DispatchAcceptance> =
    fun (_: DispatchIdentity) ->
        promise {
            match trySessionApi client with
            | Error msg ->
                let err =
                    { ErrorName = "TransportUnavailable"
                      DomainError = None
                      Message = "opencode_session_api_missing: " + msg
                      StatusCode = None
                      IsRetryable = Some false }

                PendingTurnReceipt.markTransportRejected (TurnId.value turn.TurnId) err
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
                    // ChatHooks resolves the receipt when it sees the
                    // chat.message event.  We only return the acceptance
                    // type here; we do NOT call PendingTurnReceipt.tryResolve.
                    return UserMessageAccepted id
                | _ ->
                    // OpaqueAccepted: no host user-message id to bind.
                    // Resolve the receipt here since ChatHooks will
                    // never be able to.
                    let receipt = OrderedTurnMarkerObserved
                    let _ = PendingTurnReceipt.tryResolve (TurnId.value turn.TurnId) receipt
                    return OpaqueAccepted("opencode:" + TurnId.value turn.TurnId)
        }

/// Build the query-dispatch-status function for a specific subsession turn.
/// The returned function is `unit -> JS.Promise<…>` so the caller can
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

            match dispatcher.ActiveLogicalTurnId with
            | Some active when active = nonce ->
                match trySessionApi client with
                | Ok session ->
                    try
                        let! resp =
                            invoke1 (box {| path = box {| id = SessionId.value sessionId |} |}) "messages" session

                        let data = Wanxiangshu.Runtime.Dyn.get resp "data"

                        if
                            Wanxiangshu.Runtime.Dyn.isNullish data
                            || not (Wanxiangshu.Runtime.Dyn.isArray data)
                        then
                            return StillPending
                        else
                            let msgs = unbox<obj array> data
                            let found = msgs |> Array.exists (SubsessionDispatch.isMessageMatch nonce)

                            if found then
                                let mid =
                                    msgs
                                    |> Array.tryFind (SubsessionDispatch.isMessageMatch nonce)
                                    |> Option.map (fun m -> Wanxiangshu.Runtime.Dyn.str m "id")
                                    |> Option.defaultValue ""

                                return DispatchStatus.Accepted(UserMessageObserved mid)
                            else
                                return StillPending
                    with _ ->
                        return StillPending
                | Error _ -> return StillPending

            // Dispatcher has no active turn — fall back to PendingTurnReceipt.
            | _ ->
                match PendingTurnReceipt.tryGetTransportState nonce with
                | Some PendingTurnReceipt.InFlight -> return StillPending
                | Some(PendingTurnReceipt.RejectedBeforeSend err) -> return TransportRejectedBeforeSend err
                | Some(PendingTurnReceipt.FailedAfterUnknown err) -> return TransportFailedAfterUnknownAcceptance err
                | None -> return Unknown
        }

/// Build the query-session-quiescence function for a specific subsession turn.
let buildQuerySessionQuiescence
    (client: obj)
    (sessionId: SessionId)
    (turnId: TurnId)
    : unit -> JS.Promise<QuiescenceStatus> =
    fun () ->
        promise {
            let nonce = TurnId.value turnId

            match trySessionApi client with
            | Ok session ->
                try
                    let! resp = invoke1 (box {| path = box {| id = SessionId.value sessionId |} |}) "messages" session

                    let data = Wanxiangshu.Runtime.Dyn.get resp "data"

                    if
                        Wanxiangshu.Runtime.Dyn.isNullish data
                        || not (Wanxiangshu.Runtime.Dyn.isArray data)
                    then
                        return StopUnknown
                    else
                        let msgs = unbox<obj array> data
                        let target = msgs |> Array.filter (SubsessionDispatch.isMessageMatch nonce)
                        let activeFound = target |> Array.exists SubsessionDispatch.isMessageActive

                        if activeFound then return StillRunning
                        elif target.Length > 0 then return Stopped
                        else return StopUnknown
                with _ ->
                    return StopUnknown
            | Error _ -> return StopUnknown
        }
