namespace Wanxiangshu.Composition.Turn

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
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
open Wanxiangshu.OpenCode

/// DSL-state-combination: domain — this is a single reconciled provider-turn
/// observation; optional metadata fields preserve evidence absence and never
/// encode a next-step cursor.
type ReconciledTurn =
    {
        SessionId: SessionId
        /// Physical user message that caused this provider run.
        PhysicalUserMessageId: PhysicalUserMessageId
        /// Semantic authority root; continuations never replace this identity.
        AuthorityRootUserMessageId: AuthorityRootUserMessageId
        /// HOST-010/HOST-011: one assistant message is one provider request is
        /// one turn, so the run identity IS the assistant message id. Naming the
        /// field `AssistantMessageId` invited a second identity for the same
        /// thing — and FALLBACK-003 deduplicates failed attempts by this value.
        ProviderRun: ProviderRunIdentity
        Role: Role option
        /// The worktree this run executed in, when it has one. A Manager child runs
        /// in its own worktree; a top-level session has none.
        Directory: string option
        Parts: MessagePart array
        Finish: string option
        ErrorName: string option
        Model: OpencodeModel option
        /// Publishable provider-turn classification (HOST-004). Never TurnUnknown.
        Outcome: ReconcileProgram.TurnOutcome
        /// Reconciliation-private finish=None observation. When Some, evidence and
        /// missing-final-report repair consult this — not Outcome.
        Observation: ReconcileProgram.SnapshotObservation option
    }

/// A reconciled fact is delivered once. A later fresh idle observation may revisit
/// the same fact only to evaluate idle-derived work; it must not replay terminal
/// plumbing, judgment, or other first-delivery effects (HOST-004).
[<RequireQualifiedAccess>]
type ReconciledTurnDelivery =
    | Observation
    | IdleRevisit

/// The reconciled turn plus the quiescence evidence of the pass that delivered it.
/// Only an IdleWake carries `Some permit`; retry / failure wakes carry None. The
/// side-effect boundary re-checks the permit immediately before any physical
/// idle-derived send.
type ReconciledTurnContext =
    { Turn: ReconciledTurn
      Quiescence: QuiescencePermit option
      Delivery: ReconciledTurnDelivery }

/// DSL-state-combination: physical — this process-local binding snapshots
/// observed run/message identities and optional host metadata; it is not durable
/// workflow state or a continuation program counter.
type ActiveRunBinding =
    {
        SessionId: SessionId
        RunId: string option
        AuthorityRootUserMessageId: AuthorityRootUserMessageId option
        /// Latest physical user message for the active logical run.
        PhysicalUserMessageId: PhysicalUserMessageId option
        ContinuationMessageIds: Set<string>
        Role: Role option
        Directory: string option
    }
