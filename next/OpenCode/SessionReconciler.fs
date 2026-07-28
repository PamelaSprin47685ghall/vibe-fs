namespace Wanxiangshu.Next.OpenCode

open System
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session

/// Backward-compatible wrapper around `ReconcileSupervisor.Supervisor` + `TurnBinding.Store`.
/// New code should use `ReconcileSupervisor.Supervisor` directly.
type SessionReconciler(snapshot: ISessionSnapshotPort, onTurn: ReconciledTurn -> unit, ?onDeleted: SessionId -> unit) =

    let binding = TurnBinding.Store()

    let supervisor =
        ReconcileSupervisor.Supervisor(snapshot, binding, onTurn, ?onDeleted = onDeleted)

    member _.BindActiveRun(binding: ActiveRunBinding) = supervisor.BindActiveRun(binding)

    member _.BindUserMessage(sessionId: SessionId, userMessageId: MessageId, ?agentRole: AgentRole) =
        supervisor.BindUserMessage(sessionId, userMessageId, ?agentRole = agentRole)

    member _.BindContinuationUserMessage(sessionId: SessionId, userMessageId: MessageId) =
        supervisor.BindContinuationUserMessage(sessionId, userMessageId)

    member _.TryPhysicalUserMessage(sessionId: SessionId) =
        supervisor.TryPhysicalUserMessage(sessionId)

    member _.ClearSession(sessionId: SessionId) = supervisor.ClearSession(sessionId)

    /// Kick a reconcile for the session.
    member _.MarkDirty(sessionId: SessionId) = supervisor.Kick(sessionId)

    /// Dispatch a coarse host signal.
    member _.HandleSignal(signal: HostSignal) = supervisor.Signal(signal)
