namespace Wanxiangshu.Composition.Turn

open System.Threading.Tasks
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
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
        member _.StopAndDrain() = scheduler.StopAndDrain()
