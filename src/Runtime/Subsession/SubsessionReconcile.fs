module Wanxiangshu.Runtime.SubsessionReconcile

open Fable.Core
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

                    let poisonEvents = createPoisonEvents sessionId tid runProj

                    match actor.GetState() with
                    | Poisoned _ ->
                        match stopStatus with
                        | Stopped ->
                            try
                                do! eventStore.Append(sessionId, [ PhysicalSessionClosed sessionId ])
                            with _ ->
                                ()
                        | _ -> ()
                    | _ ->
                        let eventsToAppend =
                            match stopStatus with
                            | Stopped -> PhysicalSessionClosed sessionId :: poisonEvents
                            | _ -> poisonEvents

                        try
                            do! eventStore.Append(sessionId, eventsToAppend)
                            currentProj <- List.fold projectEvent currentProj eventsToAppend
                        with _ ->
                            ()

                        do! actor.MarkUnknownAfterRestart()

            SubsessionActorRegistry.SetSafetyProjection workspaceRoot currentProj
            return currentProj
    }
