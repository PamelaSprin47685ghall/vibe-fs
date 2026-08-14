namespace Wanxiangshu.Composition.Turn

open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session
open Wanxiangshu.Composition.Durable
open Wanxiangshu.OpenCode
open Wanxiangshu.Host

/// Compatibility surface for existing Host and focused tests.
/// Reconciler.Scheduler owns queueing and direct-CE pass execution.
module ReconcileSupervisor =

    type Supervisor
        (
            snapshot: ISessionSnapshotPort,
            binding: TurnBinding.Store,
            onTurn: ReconciledTurn -> Task,
            ?onDeleted: SessionId -> unit,
            ?projection: (SessionId -> AgentProjectionSet option),
            ?onSnapshot: SessionId -> SessionMessage list -> Task,
            ?maxCausalRereads: int,
            ?maxConsecutiveErrors: int,
            ?quiescence: SessionQuiescenceGate
        ) =

        let quiescenceGate = defaultArg quiescence (SessionQuiescenceGate())

        let scheduler =
            Reconciler.Scheduler(
                snapshot,
                binding,
                (fun (context: ReconciledTurnContext) -> onTurn context.Turn),
                ?onDeleted = onDeleted,
                ?projection = projection,
                ?onSnapshot = onSnapshot,
                ?maxCausalRereads = maxCausalRereads,
                ?maxConsecutiveErrors = maxConsecutiveErrors
            )

        member _.Kick(sessionId: SessionId) =
            scheduler.Kick(sessionId, ReconcileProgram.ReconcileWake.RetryWake)

        member _.Signal(signal: HostSignal) =
            match signal with
            | SessionIdle sessionId -> scheduler.SignalIdle(sessionId, quiescenceGate.ObserveIdle sessionId)
            | ProviderRetry _
            | ProviderFailure _
            | SessionDeleted _
            // HOST-002/004: forward operator abort as typed wake; never ProviderFailure.
            | AttemptAborted _ -> scheduler.Signal signal

        member _.BindUserMessage(sessionId: SessionId, physical: PhysicalUserMessageId, ?agentRole: Role) =
            scheduler.BindUserMessage(sessionId, physical, ?agentRole = agentRole)

        member _.BindContinuationUserMessage(sessionId: SessionId, physical: PhysicalUserMessageId) =
            scheduler.BindContinuationUserMessage(sessionId, physical)

        member _.BindActiveRun(value: ActiveRunBinding) = scheduler.BindActiveRun(value)

        member _.TryPhysicalUserMessage(sessionId: SessionId) =
            scheduler.TryPhysicalUserMessage(sessionId)

        member _.RootBindings = scheduler.RootBindings
        member _.ClearSession(sessionId: SessionId) = scheduler.ClearSession(sessionId)
