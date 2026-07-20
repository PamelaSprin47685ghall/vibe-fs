module Wanxiangshu.Runtime.SubsessionReconcile

open Fable.Core
open Fable.Core.JsInterop

[<Emit("performance.now()")>]
let private now () : float = jsNative

open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Fold
open Wanxiangshu.Kernel.Subsession.Decision
open Wanxiangshu.Runtime.EventLogRuntimeStore
open Wanxiangshu.Runtime.CommandProcessor
open Wanxiangshu.Runtime.SubsessionPorts
open Wanxiangshu.Runtime.SubsessionActor
open Wanxiangshu.Runtime.SubsessionActorRegistry
open Wanxiangshu.Runtime.SubsessionEventStore
open Wanxiangshu.Runtime.SubsessionEventWire

/// No-op host used only to materialize poisoned actors for unfinished runs.
type private ReconcileHost() =
    interface ISubsessionHost with
        member _.Dispatch(_, _) =
            Promise.lift (
                Error(
                    DispatchFailure.HostRejected
                        { ErrorName = "ReconcileOnly"
                          DomainError = None
                          Message = "reconcile host cannot dispatch"
                          StatusCode = None
                          IsRetryable = Some false }
                )
            )

        member _.Abort(_, _) = Promise.lift AbortUnavailable

        member _.CancelPendingDispatch(_) = ()

        member _.QueryDispatchStatus(_, _) = Promise.lift Unknown

        member _.QuerySessionQuiescence(_, _) = Promise.lift Stopped

        member _.ClosePhysicalSession(_) = Promise.lift StopUnknown

let createReconcileHost () : ISubsessionHost = ReconcileHost() :> ISubsessionHost

let private createPoisonEvents
    (sessionId: SessionId)
    (tid: TurnId)
    (runProj: ActiveRunProjection)
    : SubsessionEvent list =
    [ SessionPoisoned(sessionId, SessionStateUnknownAfterRestart)
      TurnFinished(tid, TurnInfrastructureFailed "session state unknown after restart")
      RunFinished(runProj.RunId, Failed(InfrastructureFailure "session state unknown after restart")) ]

let private reconcileActiveRun
    (workspaceRoot: string)
    (hostFactory: (string -> ISubsessionHost) option)
    (sessionId: SessionId)
    (runProj: ActiveRunProjection)
    (currentProj: SessionSafetyProjection)
    : JS.Promise<SessionSafetyProjection> =
    promise {
        let sid = SessionId.value sessionId

        let host =
            hostFactory
            |> Option.map (fun factory -> factory sid)
            |> Option.defaultValue (ReconcileHost() :> ISubsessionHost)

        let eventStore = create workspaceRoot
        let actor = SubsessionActorRegistry.GetOrCreate workspaceRoot sid host eventStore
        let tid = TurnId.create (RunId.value runProj.RunId + "-reconcile")

        let! stopStatusOpt = PromiseQueue.withTimeout 2000 (host.ClosePhysicalSession sessionId)
        let stopStatus = defaultArg stopStatusOpt StopUnknown

        let poisonEvents = createPoisonEvents sessionId tid runProj

        match actor.GetState(), stopStatus with
        | _, Stopped ->
            try
                do! eventStore.Append(sessionId, [ PhysicalSessionClosed sessionId ])
            with _ ->
                ()
        | _ -> ()

        match actor.GetState() with
        | Poisoned _ -> return currentProj
        | _ ->
            try
                do! eventStore.Append(sessionId, poisonEvents)
                let nextProj = List.fold projectEvent currentProj poisonEvents
                let! markResult = PromiseQueue.withTimeout 2000 (actor.MarkUnknownAfterRestart())
                return nextProj
            with _ ->
                let! markResult = PromiseQueue.withTimeout 2000 (actor.MarkUnknownAfterRestart())
                return currentProj
    }

/// Load NDJSON, find RunStarted without RunFinished, and ensure those physical
/// sessions cannot accept a new StartRun until SessionClosed dispose.
/// Persists SessionPoisoned + TurnFinished + RunFinished so the decision is durable.
let reconcileUnfinishedRuns
    (workspaceRoot: string)
    (hostFactory: (string -> ISubsessionHost) option)
    : JS.Promise<SessionSafetyProjection> =
    promise {
        if System.String.IsNullOrWhiteSpace workspaceRoot then
            return emptyProjection
        else
            let store = getStore workspaceRoot
            let! eventsOpt = PromiseQueue.withTimeout 10000 (store.ReadAllEvents())
            let events = defaultArg eventsOpt []
            let proj = projectFromWanEvents events
            let mutable currentProj = proj
            let deadline = now () + 30000.0

            for KeyValue(sessionId, entry) in proj do
                if now () < deadline then
                    match entry with
                    | PersistentlyPoisoned _ -> ()
                    | ActiveRun runProj ->
                        try
                            let! nextProj = reconcileActiveRun workspaceRoot hostFactory sessionId runProj currentProj
                            currentProj <- nextProj
                        with ex ->
                            ()

            SubsessionActorRegistry.SetSafetyProjection workspaceRoot currentProj
            return currentProj
    }
