module Wanxiangshu.Hosts.Omp.OmpSubsessionHostHelper

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Dispatch

let workspaceFor (workspaceRoot: string) : Wanxiangshu.Kernel.Primitives.Identity.WorkspaceId =
    if workspaceRoot = "" then
        Wanxiangshu.Kernel.Primitives.Identity.Id.workspaceIdQuick "omp-default"
    else
        Wanxiangshu.Kernel.Primitives.Identity.Id.workspaceIdQuick ("omp:" + workspaceRoot)

let dispatchStatusOfWaiter (w: HostReceiptWaiter) : DispatchStatus =
    match w.TransportState with
    | HostReceiptWaiterTransportState.ReceiptResolved receipt -> DispatchStatus.Accepted receipt
    | HostReceiptWaiterTransportState.BeforeSendRejected err -> TransportRejectedBeforeSend err
    | HostReceiptWaiterTransportState.AfterSendUnknown err -> TransportFailedAfterUnknownAcceptance err
    | HostReceiptWaiterTransportState.ReceiptRejected(HostRejected err) -> TransportRejectedBeforeSend err
    | HostReceiptWaiterTransportState.UserCancelled -> TransportRejectedBeforeSend HostReceiptWaiter.cancelError
    | HostReceiptWaiterTransportState.ReceiptRejected(HostAcceptanceUnknown err) ->
        TransportFailedAfterUnknownAcceptance err
    | HostReceiptWaiterTransportState.InFlight -> StillPending

/// Fetch the raw message collection for an OMP session, trying the
/// `session` API first and falling back to `sessionManager`.
let fetchSessionMessages (pi: obj) (session: obj) (sessionId: SessionId) =
    promise {
        let sessionApi = Dyn.get pi "session"

        if not (Dyn.isNullish sessionApi) then
            let arg = box {| sessionId = SessionId.value sessionId |}
            let! resp = unbox<JS.Promise<obj>> (sessionApi?sessionMessages (arg))
            return Some(Dyn.get resp "data")
        else
            let sm = Dyn.get session "sessionManager"

            if Dyn.isNullish sm then
                return None
            else
                let getEntries = Dyn.get sm "getEntries"

                let raw =
                    if Dyn.typeIs getEntries "function" then
                        Dyn.callMethod0 sm "getEntries"
                    else
                        Dyn.get sm "messages"

                if Dyn.isArray raw then return Some raw else return None
    }

/// Inspect the message array for a turn marker or any user message.
/// When a user message or matched continuation carries an id, return
/// UserMessageObserved instead of fabricating an ordered marker.
let checkMessages (msgs: obj array) (target: string) =
    let mutable accepted = false
    let mutable receipt = None

    for msg in msgs do
        let info = Dyn.get msg "info"

        let found =
            if Dyn.isNullish info then
                false
            else
                let cId1 = Dyn.str info "continuationId"
                let cId2 = Dyn.str info "continuationID"
                cId1 = target || cId2 = target

        let roleTarget =
            if Dyn.str msg "role" <> "" then
                msg
            else
                let m = Dyn.get msg "message"
                if not (Dyn.isNullish m) then m else info

        let isUser =
            if Dyn.isNullish roleTarget then
                false
            else
                (Dyn.str roleTarget "role").ToLowerInvariant() = "user"

        if found then
            accepted <- true
            let msgId = Dyn.str msg "id"
            receipt <- if msgId <> "" then Some msgId else None
        elif isUser then
            accepted <- true
            let msgId = Dyn.str msg "id"
            if receipt.IsNone then receipt <- (if msgId <> "" then Some msgId else None)

    if accepted then
        match receipt with
        | Some id -> DispatchStatus.Accepted(UserMessageObserved id)
        | None -> DispatchStatus.Accepted OrderedTurnMarkerObserved
    else
        DispatchStatus.Unknown

let handleDispatchResult ws sid tid (result: Result<HostStartReceipt, DispatchFailure>) =
    match result with
    | Ok receipt -> HostReceiptWaiterRegistry.tryResolve ws sid tid receipt |> ignore
    | Error fail ->
        HostReceiptWaiterRegistry.tryFind ws sid tid
        |> Option.iter (fun w -> HostReceiptWaiter.reject w fail (ReceiptRejected fail) |> ignore)

let handleDispatchException ws sid tid (ex: System.Exception) =
    let fail =
        DispatchFailure.HostAcceptanceUnknown
            { ErrorName = "DispatchFailed"
              DomainError = None
              Message = ex.Message
              StatusCode = None
              IsRetryable = Some true }

    HostReceiptWaiterRegistry.tryFind ws sid tid
    |> Option.iter (fun w -> HostReceiptWaiter.reject w fail (ReceiptRejected fail) |> ignore)
