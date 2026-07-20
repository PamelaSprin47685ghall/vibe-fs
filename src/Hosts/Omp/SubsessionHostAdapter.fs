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
open Wanxiangshu.Hosts.Omp.OmpSubsessionHostHelper

type private OmpSessionState =
    { ActiveTurnId: TurnId
      mutable AbortSent: bool }

/// OMP serial prompt API: resolve means prompt entered the ordered stream
/// (host-guaranteed barrier). Receipt is OrderedTurnMarkerObserved.
///
/// Contract: current turn error/idle events NEVER arrive before session.prompt resolves.

type OmpSubsessionHost(session: obj, agent: string, pi: obj, workspaceRoot: string) =
    let mutable sessionStates =
        Map.empty<string, OmpSessionState>

    do
        if not (Dyn.isNullish pi) then
            try
                pi?on (
                    "event",
                    box (fun (event: obj) (ctx: obj) ->
                        let evtType = Dyn.str event "type"
                        if evtType = "session.idle" then
                            let sidOpt = getSessionIdFromContext ctx
                            match sidOpt with
                            | Some sid ->
                                sessionStates <- Map.remove sid sessionStates
                            | None -> ()
                    )
                )
            with _ ->
                ()

    interface ISubsessionHost with
        member _.Dispatch(sessionId, turn) =
            promise {
                let ws = workspaceFor workspaceRoot
                let sid = SessionId.value sessionId
                let tid = TurnId.value turn.TurnId

                let waiter = HostReceiptWaiterRegistry.create ws sid tid

                if not waiter.Completed then
                    let state =
                        { ActiveTurnId = turn.TurnId
                          AbortSent = false }

                    sessionStates <- Map.add sid state sessionStates

                    SubsessionDispatch.dispatch session agent sessionId turn
                    |> Promise.map (handleDispatchResult ws sid tid)
                    |> Promise.catch (handleDispatchException ws sid tid)
                    |> Promise.start

                let! result = waiter.Promise

                return result
            }

        member _.Abort(sessionId, turnId) =
            promise {
                let sid = SessionId.value sessionId

                let isOwner =
                    match Wanxiangshu.Runtime.SubsessionActorRegistry.SubsessionActorRegistry.TryGet workspaceRoot sid with
                    | Some actor -> actor.GetCurrentTurn() = Some turnId
                    | None -> true

                match Map.tryFind sid sessionStates with
                | Some state when state.ActiveTurnId = turnId && isOwner ->
                    if state.AbortSent then
                        return ConfirmedStopped
                    else
                        state.AbortSent <- true

                        let arg = box {| sessionId = sid |}
                        let mutable sawApi = false

                        let sessionAbortP =
                            try
                                let abortFn = Dyn.get session "abort"
                                if not (Dyn.isNullish abortFn) then
                                    sawApi <- true
                                    unbox<JS.Promise<obj>> (session?abort ())
                                    |> Promise.map (fun _ -> true)
                                    |> Promise.catch (fun _ -> false)
                                else
                                    Promise.lift false
                            with _ ->
                                Promise.lift false

                        let piAbortP =
                            try
                                let sessionApi = Dyn.get pi "session"
                                if not (Dyn.isNullish sessionApi) then
                                    sawApi <- true
                                    unbox<JS.Promise<obj>> (sessionApi?sessionAbort (arg))
                                    |> Promise.map (fun _ -> true)
                                    |> Promise.catch (fun _ -> false)
                                else
                                    Promise.lift false
                            with _ ->
                                Promise.lift false

                        let! results = Promise.all [| sessionAbortP; piAbortP |]

                        if Array.exists id results then return RequestAcceptedAwaitIdle
                        elif sawApi then return AbortUnavailable
                        else return AbortUnavailable
                | _ -> return ConfirmedStopped
            }

        member this.CancelPendingDispatch(turnId) =
            let ws = workspaceFor workspaceRoot
            HostReceiptWaiterRegistry.cancelByTurn ws (TurnId.value turnId)
            sessionStates
            |> Map.tryFindKey (fun _ state -> state.ActiveTurnId = turnId)
            |> Option.iter (fun sid ->
                let sessionId = SessionId.create sid
                (this :> ISubsessionHost).Abort(sessionId, turnId) |> ignore)

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
