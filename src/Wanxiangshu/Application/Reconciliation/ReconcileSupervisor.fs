namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session
open Wanxiangshu.Journal

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
            ?maxConsecutiveErrors: int
        ) =

        let scheduler =
            Reconciler.Scheduler(
                snapshot,
                binding,
                onTurn,
                ?onDeleted = onDeleted,
                ?projection = projection,
                ?onSnapshot = onSnapshot,
                ?maxCausalRereads = maxCausalRereads,
                ?maxConsecutiveErrors = maxConsecutiveErrors
            )

        member _.Kick(sessionId: SessionId) = scheduler.Kick(sessionId)
        member _.Signal(signal: HostSignal) = scheduler.Signal(signal)

        member _.BindUserMessage(sessionId: SessionId, physical: PhysicalUserMessageId, ?agentRole: Role) =
            scheduler.BindUserMessage(sessionId, physical, ?agentRole = agentRole)

        member _.BindContinuationUserMessage(sessionId: SessionId, physical: PhysicalUserMessageId) =
            scheduler.BindContinuationUserMessage(sessionId, physical)

        member _.BindActiveRun(value: ActiveRunBinding) = scheduler.BindActiveRun(value)

        member _.TryPhysicalUserMessage(sessionId: SessionId) =
            scheduler.TryPhysicalUserMessage(sessionId)

        member _.RootBindings = scheduler.RootBindings
        member _.ClearSession(sessionId: SessionId) = scheduler.ClearSession(sessionId)
