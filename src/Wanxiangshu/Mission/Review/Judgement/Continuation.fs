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
/// This vocabulary owns the business promise that a missing verdict is nudged
/// once per exact terminal occasion while continuation capability is still open.
/// A fresh terminal re-arms the gate until a judgement closes it. Finality's
/// PERFECT→Challenge→PERFECT ordering belongs exclusively to the direct CE in
/// ReviewBarrierWorkflow, never to continuation repair. Physical Host delivery
/// is an injected port.
module ReviewerContinuation =

    /// Ensure a reviewer who has not yet called `judge` receives the
    /// missing-verdict nudge for this exact terminal. The durable barrier and
    /// terminal ProviderRun together identify the reminder occasion. Closed
    /// continuation capability is a no-op (Finality may have revoked the
    /// challenge after a sibling REVISE).
    let ensureVerdictSubmitted
        (port: ReviewerContinuationPort)
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (terminalProviderRun: ProviderRunIdentity)
        (reviewerKey: string)
        : Task<Result<unit, string>> =
        task {
            if not (ReviewerEvidence.continuationOpen journal reviewerKey) then
                return Ok()
            else
                return! port.NudgeMissingVerdict sessionId terminalProviderRun
        }

    let ensurePerfectConfirmed
        (port: ReviewerContinuationPort)
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (terminalProviderRun: ProviderRunIdentity)
        (reviewerKey: string)
        : Task<Result<unit, string>> =
        ensureVerdictSubmitted port journal sessionId terminalProviderRun reviewerKey
