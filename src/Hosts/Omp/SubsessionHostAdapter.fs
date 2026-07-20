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
open Wanxiangshu.Runtime.OmpHostBindings
open Wanxiangshu.Hosts.Omp.OmpSubsessionHostHelper

type private OmpSessionState =
    { ActiveTurnId: TurnId
      mutable AbortSent: bool }

/// OMP subsession host.
/// Contract (fail closed — see OmpHostBindings):
/// - session.prompt resolve is NOT ordered accept; only UserMessageObserved id is Ok.
/// - idle/error MAY arrive before prompt resolve; never assume barrier ordering.
/// - CancelPendingDispatch cancels waiter and issues single-path physical abort.
/// - Abort uses session.abort XOR pi.sessionAbort, never both.

type OmpSubsessionHost(session: obj, agent: string, pi: obj, workspaceRoot: string) =
    let mutable sessionStates = Map.empty<string, OmpSessionState>

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
                            | Some sid -> sessionStates <- Map.remove sid sessionStates
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
                let tid = TurnId.value turnId

                let logStale (reason: string) =
                    JS.console.warn (
                        box
                            {| feature = "subsession"
                               hostVariant = "omp"
                               session = sid
                               turnId = tid
                               event = "stale_abort"
                               reason = reason |}
                    )

                let isOwner =
                    match Wanxiangshu.Runtime.SubsessionActorRegistry.SubsessionActorRegistry.TryGet workspaceRoot sid with
                    | Some actor -> actor.GetCurrentTurn() = Some turnId
                    | None -> true

                match Map.tryFind sid sessionStates with
                | Some state when state.ActiveTurnId = turnId && isOwner ->
                    if state.AbortSent then
                        logStale "abort_already_sent"
                        return ConfirmedStopped
                    else
                        // Mark before host call so a concurrent Abort cannot double-fire.
                        state.AbortSent <- true
                        let! struct (ok, sawApi) = abortOnce session pi sid

                        if ok then return RequestAcceptedAwaitIdle
                        elif sawApi then return AbortUnavailable
                        else return AbortUnavailable
                | Some state when state.ActiveTurnId <> turnId ->
                    logStale "active_turn_mismatch"
                    return ConfirmedStopped
                | Some _ when not isOwner ->
                    logStale "actor_turn_mismatch"
                    return ConfirmedStopped
                | None ->
                    logStale "no_active_turn"
                    return ConfirmedStopped
                | _ ->
                    logStale "ownership_gate"
                    return ConfirmedStopped
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
