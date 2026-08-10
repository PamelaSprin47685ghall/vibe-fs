namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// Vocabulary: Reviewer continuation sends (rabbit §9.2).
///
/// `HostReviewGuard` remains the transport/claim primitive; these verbs own the
/// business promise of "a missing verdict is nudged" / "a pending PERFECT is
/// challenged" exactly once when continuation capability is still open.
module ReviewerContinuation =

    /// Ensure a reviewer who has not yet used the verdict tool receives the
    /// missing-verdict nudge exactly once. Closed continuation capability is a
    /// no-op (Finality may have revoked the challenge after a sibling REVISE).
    let ensureVerdictSubmitted
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (nudgeSent: HashSet<string>)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (reviewerKey: string)
        : Task<Result<unit, string>> =
        task {
            if not (ReviewerEvidence.continuationOpen journal reviewerKey) then
                return Ok()
            else
                let! _ =
                    HostReviewGuard.nudgeReviewer sessionPort journal nudgeSent sessionId providerRun

                return Ok()
        }

    /// Ensure a first PERFECT awaiting confirmation receives the challenge
    /// exactly once. Fail closed when the send fails with nothing outstanding —
    /// otherwise the run would wait forever for a confirmation that never left.
    let ensurePerfectConfirmed
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (nudgeSent: HashSet<string>)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (reviewerKey: string)
        : Task<Result<unit, string>> =
        task {
            if not (ReviewerEvidence.continuationOpen journal reviewerKey) then
                return Ok()
            else
                let! outcome =
                    HostReviewGuard.requestPerfectConfirmation
                        sessionPort
                        journal
                        nudgeSent
                        sessionId
                        providerRun

                match outcome with
                | HostReviewGuard.GuardNudgeOutcome.Failed reason -> return Error reason
                | _ -> return Ok()
        }
