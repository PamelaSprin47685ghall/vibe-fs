namespace Wanxiangshu.Next.Journal

open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity

/// Why a journal line was refused during a fold.
///
/// PERSIST-004 requires a corrupt journal to stop startup rather than be
/// absorbed. A benign duplicate is not corruption, so the two are separated
/// here: `FoldRejection` means the line is impossible, and the caller must fail
/// closed.
type FoldRejection = { Fact: string; Reason: string }

/// Pure envelope dispatch. Each bounded projection owns its own fold algorithm;
/// this module only routes facts and decides which refusals are fatal.
module Fold =

    let empty: ProjectionSet =
        { AgentProjections = AgentProjection.empty
          RuntimeId = None }

    let private reject factName reason =
        Error { Fact = factName; Reason = reason }

    /// A dedupe refusal is the fold working as intended: the same failed attempt
    /// or the same tool call arrived twice. The projection stays as it was and
    /// the fold continues.
    ///
    /// A validation refusal is different. FALLBACK-007's modulo-4 check and
    /// REVIEW-003's causal proof can only fail on a line that could not have been
    /// written by a correct writer, so absorbing it would mean replaying a
    /// journal into a state the domain forbids.
    let private fallbackOutcome factName projection result =
        match result with
        | Ok updated -> Ok updated
        | Error AlreadyObserved
        | Error AlreadyExhausted
        | Error DifferentRun -> Ok projection
        | Error InvalidTransition ->
            reject factName "cursor advance violates FALLBACK-007 (offset or count is not the successor)"

    let private verdictOutcome factName projection result =
        match result with
        | Ok updated -> Ok updated
        | Error DuplicateAttempt -> Ok projection
        | Error NotDistinctAttempt ->
            reject factName "confirmed witness violates REVIEW-003 (same provider run or same tool call)"

    let private handleOutcome factName projection result =
        match result with
        | Ok updated -> Ok updated
        // Replaying a completion or a retirement is expected; the tombstone
        // makes both idempotent.
        | Error AlreadyCompleted
        | Error HandleIsRetired -> Ok projection
        | Error UnknownHandle -> reject factName "handle completion or retirement for a handle that was never linked"
        | Error NotCompleted -> reject factName "join retired a handle that had no completion (EXEC-004)"

    // ── session-scoped helpers ──────────────────────────────────────────────

    let private updateSession sessionId apply projection =
        AgentProjection.update sessionId apply projection

    let private updateCompanion sessionId apply projection =
        updateSession
            sessionId
            (fun session ->
                { session with
                    Companion = Some(apply (Option.defaultValue CompanionProjection.empty session.Companion)) })
            projection

    let private updateReviewGuard sessionId apply projection =
        updateSession
            sessionId
            (fun session ->
                { session with
                    ReviewGuard = Some(apply (Option.defaultValue ReviewProjection.empty session.ReviewGuard)) })
            projection

    let private updateRequirements sessionId apply projection =
        updateSession
            sessionId
            (fun session ->
                { session with
                    ReviewRequirements =
                        Some(apply (Option.defaultValue ReviewRequirementProjection.empty session.ReviewRequirements)) })
            projection

    let private updateOrchestrator apply (projection: AgentProjectionSet) =
        { projection with
            Orchestrator = apply projection.Orchestrator }

    /// PROMPT-005: dispatch facts all key on the same session and projection.
    let private updateAuthority sessionId apply projection =
        updateSession
            sessionId
            (fun session ->
                { session with
                    PromptAuthority =
                        Some(apply (Option.defaultValue PromptAuthorityLedger.empty session.PromptAuthority)) })
            projection

    let foldAgentFact (projection: AgentProjectionSet) (fact: AgentFact) : Result<AgentProjectionSet, FoldRejection> =
        match fact with

        // ── prompt dispatch ─────────────────────────────────────────────────

        | AgentFact.PluginPromptClaimed payload ->
            Ok(
                updateAuthority
                    payload.SessionId
                    (fun authority -> PromptAuthorityLedger.foldPromptClaimed authority payload)
                    projection
            )

        | AgentFact.PluginPromptSubmitted payload ->
            Ok(
                updateAuthority
                    payload.SessionId
                    (fun authority -> PromptAuthorityLedger.foldPromptSubmitted authority payload)
                    projection
            )

        | AgentFact.PluginPromptPhysicalAccepted payload ->
            Ok(
                updateAuthority
                    payload.SessionId
                    (fun authority -> PromptAuthorityLedger.foldPromptPhysicalAccepted authority payload)
                    projection
            )

        | AgentFact.PluginPromptAbandoned payload ->
            Ok(
                updateAuthority
                    payload.SessionId
                    (fun authority -> PromptAuthorityLedger.foldPromptAbandoned authority payload)
                    projection
            )

        // ── authority ───────────────────────────────────────────────────────

        | AgentFact.AuthorityRootAccepted payload ->
            // FALLBACK-001: a new Authority Root starts a fresh cursor. Done here
            // rather than by a separate reset fact, because the reset is not an
            // independent event — it IS this fact.
            //
            // REVIEW-007: a HumanRoot also creates a review requirement. An
            // AgentOwnerRoot does not: the agent that forked the work is
            // accountable for it, and requiring review of every internal prompt
            // would make the Guard fire on its own continuations.
            let withAuthority =
                updateSession
                    payload.SessionId
                    (fun session ->
                        { session with
                            PromptAuthority =
                                Some(
                                    PromptAuthorityLedger.foldAuthorityRootAccepted
                                        (Option.defaultValue PromptAuthorityLedger.empty session.PromptAuthority)
                                        payload
                                )
                            Fallback =
                                Some(
                                    FallbackProjection.forAuthority
                                        payload.LogicalRunId
                                        payload.AuthorityRootUserMessageId
                                ) })
                    projection

            if payload.AuthorityKind = "HumanRoot" then
                Ok(
                    updateRequirements
                        payload.SessionId
                        (ReviewRequirementProjection.addRequirement payload.SessionId payload.AuthorityRootUserMessageId)
                        withAuthority
                )
            else
                Ok withAuthority

        // ── fallback ────────────────────────────────────────────────────────

        | AgentFact.FallbackCursorAdvanced payload ->
            let identity =
                { SessionId = payload.SessionId
                  LogicalRunId = payload.LogicalRunId
                  AuthorityRootUserMessageId = payload.AuthorityRootUserMessageId
                  ProviderRun = payload.ProviderRun }

            AgentProjection.tryUpdate
                payload.SessionId
                (fun session ->
                    // An advance for a run with no cursor cannot be validated:
                    // FALLBACK-001 says the cursor is created by the Authority
                    // Root, so its absence means the root fact is missing.
                    match session.Fallback with
                    | None -> Error InvalidTransition
                    | Some current ->
                        FallbackProjection.applyAdvance
                            identity
                            payload.PreviousOffset
                            payload.NextOffset
                            payload.ConsecutiveFailureCount
                            current
                        |> Result.map (fun updated -> { session with Fallback = Some updated }))
                projection
            |> fallbackOutcome "FallbackCursorAdvanced" projection

        | AgentFact.FallbackExhausted payload ->
            Ok(
                updateSession
                    payload.SessionId
                    (fun session ->
                        { session with
                            Fallback = session.Fallback |> Option.map FallbackProjection.applyExhausted })
                    projection
            )

        // ── review ──────────────────────────────────────────────────────────

        | AgentFact.ReviewBarrierStarted payload ->
            Ok(
                updateReviewGuard
                    payload.ReviewerSessionId
                    (ReviewProjection.startBarrier payload.BarrierId payload.GitTreeHash)
                    projection
            )

        | AgentFact.PerfectChallengeIssued payload ->
            let challenge =
                { BarrierId = payload.BarrierId
                  GitTreeHash = payload.GitTreeHash
                  ReviewerSessionId = payload.ReviewerSessionId
                  FirstProviderRun = payload.FirstProviderRun
                  FirstToolCallId = payload.FirstToolCallId
                  ChallengeTextVersion = payload.ChallengeTextVersion
                  ChallengeContentDigest = payload.ChallengeContentDigest }

            Ok(updateReviewGuard payload.ReviewerSessionId (ReviewProjection.applyChallengeIssued challenge) projection)

        | AgentFact.ProviderInputSealed payload ->
            let seal =
                { SessionId = payload.SessionId
                  ProviderRun = payload.ProviderRun
                  PhysicalUserMessageId = payload.PhysicalUserMessageId
                  SealDigest = payload.SealDigest
                  CanonicalVersion = payload.CanonicalVersion
                  IncludedToolResultDigests =
                    payload.IncludedToolResultDigests |> List.map SealDigest.value |> Set.ofList }

            Ok(updateReviewGuard payload.SessionId (ReviewProjection.applySeal seal) projection)

        | AgentFact.ReviewVerdictRecorded payload ->
            let attempt =
                { ReviewBarrierId = payload.BarrierId
                  GitTreeHash = payload.GitTreeHash
                  ReviewerSessionId = payload.ReviewerSessionId
                  ProviderRun = payload.ProviderRun
                  ToolCallId = payload.ToolCallId }

            AgentProjection.tryUpdate
                payload.ReviewerSessionId
                (fun session ->
                    ReviewProjection.applyVerdict
                        attempt
                        payload.Verdict
                        (Option.defaultValue ReviewProjection.empty session.ReviewGuard)
                    |> Result.map (fun updated ->
                        { session with
                            ReviewGuard = Some updated }))
                projection
            |> verdictOutcome "ReviewVerdictRecorded" projection

        | AgentFact.ConfirmedReviewWitness payload ->
            // The witness lands on the reviewer session, where the rest of the
            // review facts live; the requirement clearance lands on the Manager,
            // where REVIEW-007's Guard asks. Two sessions, two updates — the
            // previous version only did the second, so a confirmed dual-PERFECT
            // never became a `Confirmed` witness anywhere and the Guard could not
            // pass no matter how many PERFECT verdicts the reviewer submitted.
            let first =
                { ProviderRun = payload.FirstProviderRun
                  ToolCallId = payload.FirstToolCallId
                  GitTreeHash = payload.GitTreeHash
                  ReviewerSessionId = payload.ReviewerSessionId }

            let second =
                { ProviderRun = payload.SecondProviderRun
                  ToolCallId = payload.SecondToolCallId
                  GitTreeHash = payload.GitTreeHash
                  ReviewerSessionId = payload.ReviewerSessionId }

            AgentProjection.tryUpdate
                payload.ReviewerSessionId
                (fun session ->
                    ReviewProjection.applyConfirmedWitness
                        payload.BarrierId
                        payload.ChallengeResultDigest
                        payload.SecondProviderInputDigest
                        first
                        second
                        (Option.defaultValue ReviewProjection.empty session.ReviewGuard)
                    |> Result.map (fun updated ->
                        { session with
                            ReviewGuard = Some updated }))
                projection
            |> verdictOutcome "ConfirmedReviewWitness" projection
            |> Result.map (
                updateRequirements
                    payload.ManagerSessionId
                    (ReviewRequirementProjection.clearOnConfirmation payload.SecondProviderRun)
            )

        // ── execution handles ───────────────────────────────────────────────

        | AgentFact.HandleLinked payload ->
            AgentProjection.tryUpdate
                payload.ParentSessionId
                (fun session ->
                    HandleProjection.link
                        payload.Handle
                        payload.ChildSessionId
                        payload.TargetAgent
                        payload.CanonicalRole
                        (Option.defaultValue HandleProjection.empty session.Handles)
                    |> Result.map (fun updated -> { session with Handles = Some updated }))
                projection
            |> handleOutcome "HandleLinked" projection

        | AgentFact.HandleCompleted payload ->
            AgentProjection.tryUpdate
                payload.ParentSessionId
                (fun session ->
                    HandleProjection.complete
                        payload.Handle
                        payload.Kind
                        (Option.defaultValue HandleProjection.empty session.Handles)
                    |> Result.map (fun updated -> { session with Handles = Some updated }))
                projection
            |> handleOutcome "HandleCompleted" projection

        | AgentFact.HandleRetired payload ->
            AgentProjection.tryUpdate
                payload.ParentSessionId
                (fun session ->
                    HandleProjection.retire payload.Handle (Option.defaultValue HandleProjection.empty session.Handles)
                    |> Result.map (fun updated -> { session with Handles = Some updated }))
                projection
            |> handleOutcome "HandleRetired" projection

        // ── orchestrator ────────────────────────────────────────────────────

        | AgentFact.ManagerJobCreated payload ->
            Ok(updateOrchestrator (OrchestratorProjection.createJob payload) projection)

        | AgentFact.CandidateReady payload ->
            Ok(
                updateOrchestrator
                    (OrchestratorProjection.recordProgress
                        payload.ManagerJobId
                        (JobProgress.CandidateReady
                            {| CandidateCommit = payload.CandidateCommit
                               PreRebaseReviewBarrierId = payload.PreRebaseReviewBarrierId |}))
                    projection
            )

        | AgentFact.ConflictDetected payload ->
            Ok(
                updateOrchestrator
                    (OrchestratorProjection.recordProgress
                        payload.ManagerJobId
                        (JobProgress.ConflictPending
                            {| CandidateCommit = payload.CandidateCommit
                               TargetHeadSnapshot = payload.TargetHeadSnapshot
                               ConflictFiles = payload.ConflictFiles
                               DiagnosticsDigest = payload.DiagnosticsDigest |}))
                    projection
            )

        | AgentFact.RebasedCandidateReady payload ->
            Ok(
                updateOrchestrator
                    (OrchestratorProjection.recordProgress
                        payload.ManagerJobId
                        (JobProgress.RebasedCandidateReady
                            {| RebasedCommit = payload.RebasedCommit
                               TargetHeadSnapshot = payload.TargetHeadSnapshot
                               PostRebaseReviewBarrierId = payload.PostRebaseReviewBarrierId |}))
                    projection
            )

        | AgentFact.PublishClaimed payload ->
            // ORCH-007 needs the rebased commit to recognise "already published".
            // It comes from the job's current progress rather than the claim
            // fact, because the claim is written inside the CAS window where the
            // rebased candidate is already established.
            let rebasedCommit =
                OrchestratorProjection.tryFind payload.ManagerJobId projection.Orchestrator
                |> Option.bind (fun job ->
                    match job.Progress with
                    | JobProgress.RebasedCandidateReady rebased -> Some rebased.RebasedCommit
                    | _ -> None)

            match rebasedCommit with
            | None -> reject "PublishClaimed" "publish claimed for a job with no rebased candidate (ORCH-004)"
            | Some commit ->
                Ok(
                    updateOrchestrator
                        (OrchestratorProjection.recordProgress
                            payload.ManagerJobId
                            (JobProgress.PublishClaimed
                                {| RebasedCommit = commit
                                   ExpectedHead = payload.ExpectedHead |}))
                        projection
                )

        | AgentFact.Published payload ->
            Ok(
                updateOrchestrator
                    (OrchestratorProjection.recordProgress
                        payload.ManagerJobId
                        (JobProgress.Published
                            {| CandidateCommit = payload.CandidateCommit
                               ResultingTargetHead = payload.ResultingTargetHead |}))
                    projection
            )

        | AgentFact.JobFailed payload ->
            Ok(
                updateOrchestrator
                    (OrchestratorProjection.recordProgress payload.ManagerJobId (JobProgress.Failed payload.Reason))
                    projection
            )

        | AgentFact.JobAbandoned payload ->
            Ok(
                updateOrchestrator
                    (OrchestratorProjection.recordProgress payload.ManagerJobId JobProgress.Abandoned)
                    projection
            )

        // ── companion ───────────────────────────────────────────────────────

        | AgentFact.CompanionBaselineSet payload ->
            Ok(updateCompanion payload.SessionId (CompanionProjection.baseline payload.Projection) projection)

        | AgentFact.CompanionCheckpointReplaced payload ->
            Ok(updateCompanion payload.SessionId (CompanionProjection.checkpoint payload.Content) projection)

        | AgentFact.CompanionAdvanced payload ->
            Ok(
                updateCompanion
                    payload.SessionId
                    (CompanionProjection.recordBlogAdvance payload.Projection payload.Content)
                    projection
            )

        | AgentFact.CompanionEpochSwitched payload ->
            Ok(
                updateCompanion
                    payload.SessionId
                    (CompanionProjection.switchEpoch
                        payload.EpochId
                        payload.FrozenB
                        payload.CutoffMessageIndex
                        payload.CoveredPrefixDigest)
                    projection
            )

        | AgentFact.CompanionReplacementActiveSet payload ->
            Ok(updateCompanion payload.SessionId (CompanionProjection.setReplacement payload.Active) projection)

        | AgentFact.CompanionBloggerLinked payload ->
            Ok(updateCompanion payload.SessionId (CompanionProjection.linkBlogger payload.BloggerSessionId) projection)

        | AgentFact.CompanionBloggerClosed payload ->
            Ok(updateCompanion payload.SessionId CompanionProjection.closeBlogger projection)

        // ── durable effects ─────────────────────────────────────────────────

        | AgentFact.DurableEffectRequested payload ->
            Ok(
                updateSession
                    payload.SessionId
                    (fun session ->
                        { session with
                            Effects =
                                Some(
                                    EffectProjection.request
                                        payload.EffectId
                                        payload.Target
                                        payload.Payload
                                        session.Effects
                                ) })
                    projection
            )

        | AgentFact.DurableEffectAccepted payload ->
            Ok(
                updateSession
                    payload.SessionId
                    (fun session ->
                        { session with
                            Effects = Some(EffectProjection.accept payload.EffectId payload.Result session.Effects) })
                    projection
            )

    let foldEnvelope (projection: ProjectionSet) (envelope: Envelope) : Result<ProjectionSet, FoldRejection> =
        match envelope.Fact with
        | Runtime(RuntimeStarted runtime) ->
            // PROMPT-011 `RecoveryAttemptBudget`: a plugin start means every claim
            // still pending at this point has survived one more recovery attempt.
            //
            // Counted here rather than written by the recovery routine. A fact saying
            // "I attempted recovery" would itself be written during recovery, so a
            // crash before that write would lose the attempt and the budget could
            // never expire — which is the unbounded-pending state the clause bounds.
            //
            // Replay is exact: envelopes fold in order, so a claim is only counted by
            // the starts that came after it.
            Ok
                { projection with
                    RuntimeId = Some runtime.RuntimeId
                    AgentProjections =
                        { projection.AgentProjections with
                            Sessions =
                                projection.AgentProjections.Sessions
                                |> Map.map (fun _ session ->
                                    { session with
                                        PromptAuthority =
                                            session.PromptAuthority |> Option.map PromptAuthority.countRecoveryAttempt }) } }
        | Agent fact ->
            foldAgentFact projection.AgentProjections fact
            |> Result.map (fun agents ->
                { projection with
                    AgentProjections = agents })

    /// Fold a journal. PERSIST-004: the first impossible line stops the fold and
    /// reports which fact and why, rather than producing a partially replayed
    /// state that no writer could have produced.
    let apply (projection: ProjectionSet) (envelopes: Envelope list) : Result<ProjectionSet, FoldRejection> =
        envelopes
        |> List.fold
            (fun state envelope -> state |> Result.bind (fun current -> foldEnvelope current envelope))
            (Ok projection)
