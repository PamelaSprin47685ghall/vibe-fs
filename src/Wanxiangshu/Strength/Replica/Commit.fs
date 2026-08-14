namespace Wanxiangshu.Strength.Replica

open Wanxiangshu.Composition.Turn
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
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection

/// STRENGTH-006/007: adapter-level append result. CommitUnknown means the
/// transport/process cannot prove whether the deterministic EventId reached the
/// authoritative store; it is deliberately not folded into ordinary failure.
[<RequireQualifiedAccess>]
type StrengthAppendOutcome =
    | Committed
    | Rejected
    | CommitUnknown

/// Result of rereading the indexed Strength durable projection after an unknown
/// append outcome. `Matches` means the same immutable fact (digest/refs/target),
/// not merely the same DecisionId.
[<RequireQualifiedAccess>]
type StrengthDurableEvidence =
    | Matches
    | Absent
    | Conflicts
    | Unknown

[<RequireQualifiedAccess>]
type StrengthCommitDecision =
    | Proceed
    | FallBackK0
    | RetryAppend
    | FailClosed

[<RequireQualifiedAccess>]
module StrengthCommit =

    /// Prepared is still pre-intervention. A definite rejection, or an unknown
    /// append later proved absent, may safely collapse this decision to K0.
    let resolvePrepared
        (appendOutcome: StrengthAppendOutcome)
        (durableEvidence: StrengthDurableEvidence)
        : StrengthCommitDecision =
        match appendOutcome with
        | StrengthAppendOutcome.Committed -> StrengthCommitDecision.Proceed
        | StrengthAppendOutcome.Rejected -> StrengthCommitDecision.FallBackK0
        | StrengthAppendOutcome.CommitUnknown ->
            match durableEvidence with
            | StrengthDurableEvidence.Matches -> StrengthCommitDecision.Proceed
            | StrengthDurableEvidence.Absent -> StrengthCommitDecision.FallBackK0
            | StrengthDurableEvidence.Conflicts
            | StrengthDurableEvidence.Unknown -> StrengthCommitDecision.FailClosed

    /// Promotion closes already-real causality: the target provider run has
    /// consumed the Candidate. A definite append rejection therefore cannot
    /// fall open. If CommitUnknown is reread as definitely absent, retrying the
    /// same deterministic fact is safe; unresolved/conflicting state blocks the
    /// next continuation.
    let resolvePromotion
        (appendOutcome: StrengthAppendOutcome)
        (durableEvidence: StrengthDurableEvidence)
        : StrengthCommitDecision =
        match appendOutcome with
        | StrengthAppendOutcome.Committed -> StrengthCommitDecision.Proceed
        | StrengthAppendOutcome.Rejected -> StrengthCommitDecision.FailClosed
        | StrengthAppendOutcome.CommitUnknown ->
            match durableEvidence with
            | StrengthDurableEvidence.Matches -> StrengthCommitDecision.Proceed
            | StrengthDurableEvidence.Absent -> StrengthCommitDecision.RetryAppend
            | StrengthDurableEvidence.Conflicts
            | StrengthDurableEvidence.Unknown -> StrengthCommitDecision.FailClosed
