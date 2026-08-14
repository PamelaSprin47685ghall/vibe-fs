namespace Wanxiangshu.Mission.Review.Judgement

open Wanxiangshu.Change
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Persistence
open Wanxiangshu.Strength.Replica

open System.Threading.Tasks
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation.Identity

/// Vocabulary: Reviewer continuation sends (rabbit §9.2).
///
/// These verbs own the business promise of "a missing verdict is nudged" /
/// "a pending PERFECT is challenged" exactly once when continuation capability
/// is still open. Physical Host delivery is an injected port.
module ReviewerContinuation =

    /// Ensure a reviewer who has not yet used the verdict tool receives the
    /// missing-verdict nudge exactly once. Closed continuation capability is a
    /// no-op (Finality may have revoked the challenge after a sibling REVISE).
    let ensureVerdictSubmitted
        (port: ReviewerContinuationPort)
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (reviewerKey: string)
        : Task<Result<unit, string>> =
        task {
            if not (ReviewerEvidence.continuationOpen journal reviewerKey) then
                return Ok()
            else
                return! port.NudgeMissingVerdict sessionId providerRun
        }

    /// Ensure a first PERFECT awaiting confirmation receives the challenge
    /// exactly once. Fail closed when the send fails with nothing outstanding —
    /// otherwise the run would wait forever for a confirmation that never left.
    let ensurePerfectConfirmed
        (port: ReviewerContinuationPort)
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (reviewerKey: string)
        : Task<Result<unit, string>> =
        task {
            if not (ReviewerEvidence.continuationOpen journal reviewerKey) then
                return Ok()
            else
                return! port.SendPerfectChallenge sessionId providerRun
        }
