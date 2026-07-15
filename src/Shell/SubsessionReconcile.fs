module Wanxiangshu.Shell.SubsessionReconcile

open Fable.Core
open Wanxiangshu.Kernel.EventLog.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Fold
open Wanxiangshu.Kernel.Subsession.Decision
open Wanxiangshu.Shell.EventLogRuntimeStore
open Wanxiangshu.Shell.SubsessionActor
open Wanxiangshu.Shell.SubsessionActorRegistry
open Wanxiangshu.Shell.SubsessionEventStore
open Wanxiangshu.Shell.SubsessionEventWire

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
            let root = workspaceRoot
            let store = getStore root
            let! events = store.ReadAllEvents()
            let proj = projectFromWanEvents events
            let mutable currentProj = proj

            for KeyValue(sessionId, entry) in proj do
                match entry with
                | PersistentlyPoisoned _ -> ()
                | ActiveRun runProj ->
                    let sid = SessionId.value sessionId

                    let host =
                        hostFactory
                        |> Option.map (fun factory -> factory sid)
                        |> Option.defaultValue (ReconcileHost() :> ISubsessionHost)

                    let eventStore = create root
                    let actor = SubsessionActorRegistry.GetOrCreate workspaceRoot sid host eventStore
                    let tid = TurnId.create (RunId.value runProj.RunId + "-reconcile")

                    let! stopStatus = host.ClosePhysicalSession sessionId

                    let poisonEvents: SubsessionEvent list =
                        [ SessionPoisoned(sessionId, SessionStateUnknownAfterRestart)
                          TurnFinished(tid, TurnInfrastructureFailed "session state unknown after restart")
                          RunFinished(
                              runProj.RunId,
                              Failed(InfrastructureFailure "session state unknown after restart")
                          ) ]

                    match actor.GetState(), stopStatus with
                    | _, Stopped ->
                        try
                            do! eventStore.Append(sessionId, [ PhysicalSessionClosed sessionId ])
                        with _ ->
                            ()
                    | _ -> ()

                    match actor.GetState() with
                    | Poisoned _ -> ()
                    | _ ->
                        // NDJSON append is best-effort: if it fails, MarkUnknownAfterRestart
                        // still poisons the actor in-memory. On next restart the same orphan
                        // will be rediscovered and re-reconciled (idempotent).
                        try
                            do! eventStore.Append(sessionId, poisonEvents)
                            currentProj <- List.fold projectEvent currentProj poisonEvents
                        with _ ->
                            ()

                        do! actor.MarkUnknownAfterRestart()

            SubsessionActorRegistry.SetSafetyProjection workspaceRoot currentProj
            return currentProj
    }
