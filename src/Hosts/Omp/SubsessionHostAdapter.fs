module Wanxiangshu.Hosts.Omp.SubsessionHostAdapter

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.CommandProcessor
open Wanxiangshu.Runtime.SubsessionPorts
open Wanxiangshu.Runtime.SubsessionActor
open Wanxiangshu.Runtime.SubsessionEventRouter
open Wanxiangshu.Hosts.Omp.Codec
open Wanxiangshu.Hosts.Omp.SubsessionDispatch
open Wanxiangshu.Hosts.Omp.MessagingCodec
open Wanxiangshu.Hosts.Omp.OmpSubsessionHostAdapterPrompts
open Wanxiangshu.Runtime.Dispatch

type private OmpSessionState =
    { ActiveTurnId: TurnId
      mutable AbortSent: bool }

let mutable private sessionStates = Map.empty<string, OmpSessionState>

let private workspaceFor (workspaceRoot: string) : Wanxiangshu.Kernel.Primitives.Identity.WorkspaceId =
    if workspaceRoot = "" then
        Wanxiangshu.Kernel.Primitives.Identity.Id.workspaceIdQuick "omp-default"
    else
        Wanxiangshu.Kernel.Primitives.Identity.Id.workspaceIdQuick ("omp:" + workspaceRoot)

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

/// Fetch the raw message collection for an OMP session, trying the
/// `session` API first and falling back to `sessionManager`.
let private fetchSessionMessages (pi: obj) (session: obj) (sessionId: SessionId) =
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
let private checkMessages (msgs: obj array) (target: string) =
    let mutable found = false
    let mutable anyUser = false

    for msg in msgs do
        let info = Dyn.get msg "info"

        if not (Dyn.isNullish info) then
            let cId1 = Dyn.str info "continuationId"
            let cId2 = Dyn.str info "continuationID"

            if cId1 = target || cId2 = target then
                found <- true

        let roleTarget =
            if Dyn.str msg "role" <> "" then
                msg
            else
                let m = Dyn.get msg "message"
                if not (Dyn.isNullish m) then m else info

        if not (Dyn.isNullish roleTarget) then
            let role = (Dyn.str roleTarget "role").ToLowerInvariant()

            if role = "user" then
                anyUser <- true

    if found || anyUser then
        DispatchStatus.Accepted OrderedTurnMarkerObserved
    else
        DispatchStatus.Unknown

/// OMP serial prompt API: resolve means prompt entered the ordered stream
/// (host-guaranteed barrier). Receipt is OrderedTurnMarkerObserved.
///
/// Contract: current turn error/idle events NEVER arrive before session.prompt resolves.

type OmpSubsessionHost(session: obj, agent: string, pi: obj, workspaceRoot: string) =
    interface ISubsessionHost with
        member _.Dispatch(sessionId, turn) =
            promise {
                let ws = workspaceFor workspaceRoot
                let sid = SessionId.value sessionId
                let tid = TurnId.value turn.TurnId

                let state =
                    { ActiveTurnId = turn.TurnId
                      AbortSent = false }

                sessionStates <- Map.add sid state sessionStates

                let waiter = HostReceiptWaiterRegistry.create ws sid tid

                SubsessionDispatch.dispatch session agent sessionId turn
                |> Promise.map (fun result ->
                    match result with
                    | Ok receipt -> HostReceiptWaiterRegistry.tryResolve ws sid tid receipt |> ignore
                    | Error fail ->
                        HostReceiptWaiterRegistry.tryFind ws sid tid
                        |> Option.iter (fun w -> HostReceiptWaiter.reject w fail (ReceiptRejected fail) |> ignore))
                |> Promise.catch (fun ex ->
                    let fail =
                        DispatchFailure.HostAcceptanceUnknown
                            { ErrorName = "DispatchFailed"
                              DomainError = None
                              Message = ex.Message
                              StatusCode = None
                              IsRetryable = Some true }

                    HostReceiptWaiterRegistry.tryFind ws sid tid
                    |> Option.iter (fun w -> HostReceiptWaiter.reject w fail (ReceiptRejected fail) |> ignore))
                |> Promise.start

                let! result = waiter.Promise

                match result with
                | Ok receipt ->
                    JS.setTimeout
                        (fun () ->
                            promise {
                                let sm = Dyn.get session "sessionManager"

                                if not (Dyn.isNullish sm) then
                                    let text =
                                        match readAssistantText (unbox<ISessionManager> sm) 0 "\n\n" with
                                        | Some t -> t
                                        | None -> ""

                                    if text <> "" then
                                        let evidence =
                                            { CurrentTurnEvidence.empty with
                                                Assistant = AssistantSnapshot("", 0L, text, Some NormalFinish)
                                                Outcome = CompletionRequested text }

                                        do!
                                            SubsessionEventRouter.routeEvidence
                                                workspaceRoot
                                                (SessionId.value sessionId)
                                                evidence
                                            |> Promise.map ignore

                                do!
                                    SubsessionEventRouter.tryIdle workspaceRoot (SessionId.value sessionId)
                                    |> Promise.map ignore
                            }
                            |> Promise.start)
                        50
                    |> ignore
                | Error _ -> ()

                return result
            }

        member _.Abort(sessionId, turnId) =
            promise {
                let sid = SessionId.value sessionId

                match Map.tryFind sid sessionStates with
                | Some state when state.ActiveTurnId = turnId ->
                    if state.AbortSent then
                        return ConfirmedStopped
                    else
                        state.AbortSent <- true

                        let mutable requested = false
                        let mutable sawApi = false

                        try
                            let abortFn = Dyn.get session "abort"

                            if not (Dyn.isNullish abortFn) then
                                sawApi <- true
                                do! unbox<JS.Promise<obj>> (session?abort ()) |> Promise.map ignore
                                requested <- true
                        with _ ->
                            ()

                        try
                            let sessionApi = Dyn.get pi "session"

                            if not (Dyn.isNullish sessionApi) then
                                sawApi <- true
                                let arg = box {| sessionId = SessionId.value sessionId |}
                                do! unbox<JS.Promise<obj>> (sessionApi?sessionAbort (arg)) |> Promise.map ignore
                                requested <- true
                        with _ ->
                            ()

                        if requested then return RequestAcceptedAwaitIdle
                        elif sawApi then return AbortUnavailable
                        else return AbortUnavailable
                | _ -> return ConfirmedStopped
            }

        member _.CancelPendingDispatch(turnId) =
            HostReceiptWaiterRegistry.cancelByTurn (workspaceFor workspaceRoot) (TurnId.value turnId)

        member _.QueryDispatchStatus(sessionId, turnId) =
            promise {
                let ws = workspaceFor workspaceRoot
                let sid = SessionId.value sessionId
                let target = TurnId.value turnId

                match HostReceiptWaiterRegistry.tryFind ws sid target with
                | Some w when w.Completed -> return dispatchStatusOfWaiter w
                | Some _ -> return StillPending
                | None ->
                    try
                        let! dataOpt = fetchSessionMessages pi session sessionId

                        match dataOpt with
                        | Some data when not (Dyn.isNullish data) && Dyn.isArray data ->
                            let msgs = unbox<obj array> data
                            return checkMessages msgs target
                        | _ -> return DispatchStatus.Unknown
                    with _ ->
                        return DispatchStatus.Unknown
            }

        member _.QuerySessionQuiescence(sessionId, _turnId) =
            OmpSubsessionHostAdapterPrompts.querySessionQuiescence session (Dyn.get pi "session") sessionId _turnId

        member _.ClosePhysicalSession(sessionId) =
            promise {
                let ws = workspaceFor workspaceRoot
                let sid = SessionId.value sessionId

                HostReceiptWaiterRegistry.removeSession ws sid
                sessionStates <- Map.remove sid sessionStates

                return! OmpSubsessionHostAdapterPrompts.closePhysicalSession session (Dyn.get pi "session") sessionId
            }

let createHost (session: obj) (agent: string) (pi: obj) (workspaceRoot: string) : ISubsessionHost =
    OmpSubsessionHost(session, agent, pi, workspaceRoot) :> ISubsessionHost
