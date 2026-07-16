module Wanxiangshu.Shell.SubsessionActor

open Fable.Core
open Wanxiangshu.Kernel.Reactive
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.ResourcePlan
open Wanxiangshu.Shell.PromiseQueue
open Wanxiangshu.Shell.ResourceScope
open Wanxiangshu.Shell.CommandProcessor
open Wanxiangshu.Shell.EffectSupervisor

// ── Actor ──

/// Composes CommandProcessor (serial commit pipeline) + EffectSupervisor
/// (host effect dispatch) + ResourceScope (RAII timer management).
///
/// Architecture (REF.md Phase 4):
///
///   SubsessionActor (thin wiring layer)
///     ├── CommandProcessor — owns state, queue, pendingReplies,
///     │                     10-step commit pipeline
///     ├── EffectSupervisor — fire-and-forget host effect dispatch,
///     │                     timer expiry re-entry
///     └── ResourceScope    — RAII JS timer management
///
type SubsessionActor
    (
        sessionId: SessionId,
        host: ISubsessionHost,
        eventStore: ISubsessionEventStore,
        ?onDispose: unit -> unit,
        ?initialState: SubsessionState,
        ?reactivePort: IReactivePort
    ) as this =

    // ── ResourceScope: timer expiry → post deadline-command to self ──
    let resourceScope =
        let cb (id: ResourceId) =
            match id with
            | TurnDeadlineId tid -> this.Post(TurnDeadlineExpired(TurnId.create tid)) |> ignore
            | AbortDeadlineId tid -> this.Post(AbortDeadlineExpired(TurnId.create tid)) |> ignore
            | ReconciliationDeadlineId tid -> this.Post(ReconciliationDeadlineExpired(TurnId.create tid)) |> ignore

        ResourceScope(cb)

    // ── Reconcile resources from actor state after each commit ──
    let reconcileResources (state: SubsessionState) : unit =
        let nowMs = int64 (JS.Date.now ())
        let specs = projectResources nowMs state
        resourceScope.Reconcile specs

    // ── CommandProcessor: serial commit pipeline ──
    let processor =
        CommandProcessor(
            sessionId,
            host,
            eventStore,
            reconcileResources,
            ?reactivePort = reactivePort,
            ?initialState = initialState
        )

    // Wire cleanup callback: DisposeActor → clear resource timers + call onDispose
    do
        processor.AddCleanupCallback(fun () ->
            resourceScope.ClearAll()
            onDispose |> Option.iter (fun f -> f ()))

    // ── EffectSupervisor: host effect dispatch ──
    let supervisor =
        EffectSupervisor(sessionId, host, (fun cmd -> this.Post cmd |> ignore), ?reactivePort = reactivePort)

    // ── Public API ──

    member _.SessionId = sessionId

    member _.GetState() : SubsessionState = processor.GetState()

    member _.IsPoisoned: bool = processor.IsPoisoned

    member _.IsDisposed: bool = processor.IsDisposed

    member _.GetCurrentTurn() : TurnId option = processor.GetCurrentTurn()

    /// Startup reconcile: unfinished NDJSON run → poison so StartRun is rejected.
    member _.MarkUnknownAfterRestart() : JS.Promise<unit> =
        promise {
            do! processor.MarkUnknownAfterRestart()
            resourceScope.ClearAll()
        }

    /// Post a fact. Commits on the queue, then launches host effects detached.
    member _.Post(cmd: Command) : JS.Promise<unit> =
        promise {
            let! hostEffects, _ = processor.Post cmd
            supervisor.LaunchAll hostEffects
        }

    /// Atomic BeginRun: register Deferred + decide StartRun + append + commit
    /// in ONE queue item so CancelRequested cannot insert between them.
    member this.BeginRun(request: StartRunRequest) : JS.Promise<RunResult> =
        promise {
            let! resultPromise, hostEffects = processor.BeginRun request
            supervisor.LaunchAll hostEffects
            return! resultPromise
        }

    /// Back-compat alias for BeginRun (atomic).
    member this.StartRun(request: StartRunRequest) : JS.Promise<RunResult> = this.BeginRun request
