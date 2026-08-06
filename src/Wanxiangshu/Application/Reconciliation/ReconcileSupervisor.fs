namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session
open Wanxiangshu.Journal

/// Compatibility surface for existing Host and focused tests. ReconcileInterpreter
/// owns queueing, program interpretation, and every reconcile effect.
module ReconcileSupervisor =

    type Supervisor
        (
            snapshot: ISessionSnapshotPort,
            binding: TurnBinding.Store,
            onTurn: ReconciledTurn -> Task,
            ?onDeleted: SessionId -> unit,
            ?projection: (SessionId -> AgentProjectionSet option),
            ?onSnapshot: SessionId -> SessionMessage list -> Task,
            ?backoffDelaysMs: int array,
            ?maxBudgetMs: int
        ) =

        let interpreter =
            ReconcileInterpreter.Interpreter(
                snapshot,
                binding,
                onTurn,
                ?onDeleted = onDeleted,
                ?projection = projection,
                ?onSnapshot = onSnapshot,
                ?backoffDelaysMs = backoffDelaysMs,
                ?maxBudgetMs = maxBudgetMs
            )

        member _.Kick(sessionId: SessionId) = interpreter.Kick(sessionId)
        member _.Signal(signal: HostSignal) = interpreter.Signal(signal)

        member _.BindUserMessage(sessionId: SessionId, physical: PhysicalUserMessageId, ?agentRole: AgentRole) =
            interpreter.BindUserMessage(sessionId, physical, ?agentRole = agentRole)

        member _.BindContinuationUserMessage(sessionId: SessionId, physical: PhysicalUserMessageId) =
            interpreter.BindContinuationUserMessage(sessionId, physical)

        member _.BindActiveRun(value: ActiveRunBinding) = interpreter.BindActiveRun(value)

        member _.TryPhysicalUserMessage(sessionId: SessionId) =
            interpreter.TryPhysicalUserMessage(sessionId)

        member _.RootBindings = interpreter.RootBindings
        member _.ClearSession(sessionId: SessionId) = interpreter.ClearSession(sessionId)
