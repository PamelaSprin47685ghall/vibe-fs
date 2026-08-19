namespace Wanxiangshu.Change

open Wanxiangshu.Git
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Persistence
open Wanxiangshu.Strength.Replica

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Host
open Wanxiangshu.Change
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// ORCH-004/005/006/007: worktree → review → rebase → fresh review → short-CAS
/// ff-only publish.
///
/// Restart re-proves the outstanding obligation from independent durable facts
/// plus current Git reality. No durable latest-stage enum survives the callback.
module OrchestratorProgram =

    /// What one publish attempt resolved to.
    ///
    /// `TargetMoved` is not an error: ORCH-005 answers it by rebasing and
    /// re-reviewing. Folding it into the error channel is how a retry ends up
    /// reusing a post-rebase witness that REVIEW-008 has already invalidated.
    type private PublishAttempt =
        | TargetMoved
        | Landed of CommitHash

    let private failed (job: ManagerJob) details =
        OrchestratorVerdict.IntegrationFailed(job.JobId, details)

    let private asVerdict (result: Result<OrchestratorVerdict, OrchestratorVerdict>) =
        match result with
        | Ok verdict -> verdict
        | Error verdict -> verdict

    let private mapTask (mapper: 'a -> 'b) (operation: Task<'a>) : Task<'b> =
        task {
            let! value = operation
            return mapper value
        }

    let private mapTaskError (mapper: 'error -> 'mapped) (operation: Task<Result<'value, 'error>>) =
        mapTask (Result.mapError mapper) operation

    let private append (deps: OrchestratorProgramDeps) (job: ManagerJob) fact =
        taskResult { do! deps.AppendFact StreamId.Workspace fact |> mapTaskError (failed job) }

    /// REVIEW-008: one barrier per review round, never reused.
    ///
    /// `round` is part of the id because a post-rebase review can run several times
    /// against the same tree when the target keeps moving, and each of those must be
    /// a fresh barrier with two new tool calls. Deriving the id from the tree hash
    /// instead would make round two look like a replay of round one.
    let private barrierId (job: ManagerJob) (phase: string) (round: int) =
        ReviewBarrierId.create (sprintf "%s:%s:%d" (ManagerJobId.value job.JobId) phase round)

    let private readHead (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        taskResult {
            return!
                deps.Git.ReadHead job.Worktree.Path
                |> mapTaskError (fun error -> failed job (sprintf "Git head lookup failed: %s" error))
        }

    /// One review barrier. `Reverify` returns only once a dual PERFECT is confirmed
    /// for the current tree, so there is nothing to re-check here.
    let private reviewRound (deps: OrchestratorProgramDeps) (job: ManagerJob) (phase: string) (round: int) =
        taskResult {
            do!
                deps.Manager.Reverify job.JobId job.ManagerSessionId job.Worktree.Path (barrierId job phase round)
                |> mapTaskError (fun error -> OrchestratorVerdict.NeedsReview(job.JobId, error))
        }

    // ── ORCH-006 fact writers ───────────────────────────────────────────────

    let private recordCandidateReady (deps: OrchestratorProgramDeps) (job: ManagerJob) (round: int) =
        taskResult {
            let! candidate = readHead deps job

            do!
                append
                    deps
                    job
                    (OrchestratorFact.CandidateReady
                        {| ManagerJobId = job.JobId
                           CandidateCommit = candidate
                           PreRebaseReviewBarrierId = barrierId job "pre-rebase" round |})
        }

    let private recordRebasedReady
        (deps: OrchestratorProgramDeps)
        (job: ManagerJob)
        (targetHead: CommitHash)
        (round: int)
        =
        taskResult {
            let! rebased = readHead deps job

            do!
                append
                    deps
                    job
                    (OrchestratorFact.RebasedCandidateReady
                        {| ManagerJobId = job.JobId
                           RebasedCommit = rebased
                           TargetHeadSnapshot = targetHead
                           PostRebaseReviewBarrierId = barrierId job "post-rebase" round |})
        }

    let private recordConflict
        (deps: OrchestratorProgramDeps)
        (job: ManagerJob)
        (candidate: CommitHash)
        (targetHead: CommitHash)
        (files: string list)
        =
        append
            deps
            job
            (OrchestratorFact.ConflictDetected
                {| ManagerJobId = job.JobId
                   CandidateCommit = candidate
                   TargetHeadSnapshot = targetHead
                   ConflictFiles = files
                   DiagnosticsDigest = HostDigest.sha256Hex (String.Join("\n", files)) |})

    // ── rebase ──────────────────────────────────────────────────────────────

    /// Resume the same Manager after conflict files are recorded, then retry rebase.
    let private continueAfterConflict
        (deps: OrchestratorProgramDeps)
        (job: ManagerJob)
        (targetHead: CommitHash)
        (rebaseError: string)
        =
        taskResult {
            let! files =
                deps.Git.ConflictedFiles job.Worktree.Path
                |> mapTaskError (fun error -> failed job (sprintf "Conflict-file lookup failed: %s" error))

            let! candidate = readHead deps job
            do! recordConflict deps job candidate targetHead files
            let prompt = OrchestratorPrompts.buildConflictResumePrompt files

            do!
                deps.Manager.ResumeManager job.JobId job.Worktree.Path prompt
                |> mapTask (
                    Result.mapError (fun error ->
                        failed job (sprintf "Rebase conflict (%s); manager continuation failed: %s" rebaseError error))
                )

            do!
                deps.Git.Rebase job.Worktree.Path job.TargetRef
                |> mapTaskError (fun error -> failed job (sprintf "Rebase continuation failed: %s" error))
        }

    /// Rebase onto the target head, handing any conflict back to the SAME Manager
    /// in the SAME worktree (ORCH-003).
    ///
    /// Returns the target head it rebased onto, so the caller records the snapshot
    /// the post-rebase witness belongs to. Re-reading it later could observe a
    /// different value, and the witness would then claim a base it never had.
    let private rebaseOnto (deps: OrchestratorProgramDeps) (job: ManagerJob) (targetHead: CommitHash) =
        taskResult {
            let! rebaseOutcome = deps.Git.Rebase job.Worktree.Path job.TargetRef |> TaskResultCE.ofTask

            match rebaseOutcome with
            | Ok() -> return ()
            | Error rebaseError -> return! continueAfterConflict deps job targetHead rebaseError
        }

    // ── ORCH-005 short CAS window ───────────────────────────────────────────

    let private completeClaimAndFf (deps: OrchestratorProgramDeps) (job: ManagerJob) (current: CommitHash) =
        taskResult {
            let claim =
                OrchestratorFact.PublishClaimed
                    {| ManagerJobId = job.JobId
                       TargetRef = job.TargetRef
                       ExpectedHead = current |}

            do! append deps job claim
            let! merge = deps.Git.FfMerge job.Worktree.Path job.TargetRef current |> TaskResultCE.ofTask

            match merge with
            | Error error when error = OrchestratorConstants.targetRefMovedError -> return TargetMoved
            | Error error -> return! Error(failed job (sprintf "FF merge failed: %s" error))
            | Ok landed ->
                let published =
                    OrchestratorFact.Published
                        {| ManagerJobId = job.JobId
                           CandidateCommit = landed
                           ResultingTargetHead = landed |}

                do! append deps job published
                do! deps.Manager.TerminateChildren job.JobId |> TaskResultCE.ofTask
                return Landed landed
        }

    /// Claim, verify, ff, publish — all inside the short Integration Gate.
    ///
    /// The gate is acquired HERE, not around the enclosing loop. ORCH-005 says it
    /// protects the ref mutation only; the previous version held it across rebase
    /// and LLM review, which serialised every job's entire review phase behind one
    /// lock.
    ///
    /// The head is re-read inside the gate even though the caller just read it. That
    /// second read is the compare in compare-and-swap: between the caller's read and
    /// the lock being granted, another job may have published.
    let private claimAndFf (deps: OrchestratorProgramDeps) (job: ManagerJob) (expectedHead: CommitHash) =
        taskResult {
            let! current =
                deps.Git.GetTargetHead job.TargetRef
                |> mapTaskError (fun error -> failed job (sprintf "Git target head lookup failed: %s" error))

            match current = expectedHead with
            | false -> return TargetMoved
            | true -> return! completeClaimAndFf deps job current
        }

    /// Hold the gate for exactly the CAS window.
    ///
    /// `claimAndFf` is wrapped so it cannot throw past the release. A leaked publish
    /// lock blocks every other job in the repository until stale detection expires
    /// it, so release must not depend on the happy path being taken.
    let private publishUnderGate (deps: OrchestratorProgramDeps) (job: ManagerJob) (expectedHead: CommitHash) =
        task {
            let! gate = IntegrationGate.acquire deps.GatePath

            let! outcome =
                task {
                    try
                        return! claimAndFf deps job expectedHead
                    with error ->
                        return Error(failed job (sprintf "Publish window failed: %s" error.Message))
                }

            do! gate.Release()
            return outcome
        }

    let private releaseTerminalWorktree (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        task {
            do! deps.Manager.TerminateChildren job.JobId
            return! job.Worktree.Release()
        }

    let private settleLanded (deps: OrchestratorProgramDeps) (job: ManagerJob) (commit: CommitHash) =
        taskResult {
            do!
                releaseTerminalWorktree deps job
                |> mapTask (
                    Result.mapError (fun error ->
                        failed job (sprintf "Published %s but cleanup failed: %s" (CommitHash.value commit) error))
                )

            return OrchestratorVerdict.Published(job.JobId, commit)
        }
        |> mapTask asVerdict

    /// ORCH-005: rebase → fresh dual PERFECT → short-gate ff. On a moved target the
    /// whole round repeats, and the previous post-rebase witness is abandoned
    /// (REVIEW-008) rather than reused.
    let rec private publishEventually (deps: OrchestratorProgramDeps) (job: ManagerJob) (round: int) =
        taskResult {
            let! targetHead =
                deps.Git.GetTargetHead job.TargetRef
                |> mapTaskError (fun error -> failed job (sprintf "Git target head lookup failed: %s" error))

            do! rebaseOnto deps job targetHead
            do! reviewRound deps job "post-rebase" round
            do! recordRebasedReady deps job targetHead round
            let! attempt = publishUnderGate deps job targetHead

            match attempt with
            | TargetMoved ->
                let! verdict = publishEventually deps job (round + 1) |> TaskResultCE.ofTask
                return verdict
            | Landed commit ->
                let! verdict = settleLanded deps job commit |> TaskResultCE.ofTask
                return verdict
        }
        |> mapTask asVerdict

    // ── ORCH-007 reentry from durable facts ─────────────────────────────────

    /// Pre-rebase review and candidate registration, shared by the fresh run and the
    /// `ManagerStarted` reentry path.
    let private afterManager (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        taskResult {
            do! reviewRound deps job "pre-rebase" 0
            do! recordCandidateReady deps job 0
            let! verdict = publishEventually deps job 0 |> TaskResultCE.ofTask
            return verdict
        }
        |> mapTask asVerdict

    /// Await the Manager, then review + register candidate + publish.
    /// One entry for both fresh start and `ManagerStarted` reentry.
    let private awaitAndPublish (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        taskResult {
            do!
                deps.Manager.AwaitManager job.JobId
                |> mapTaskError (fun error -> failed job (sprintf "Manager run failed: %s" error))

            let! verdict = afterManager deps job |> TaskResultCE.ofTask
            return verdict
        }
        |> mapTask asVerdict

    /// ORCH-003/007: hand a rebase conflict back to the SAME Manager in the SAME
    /// worktree, then re-enter the publish loop.
    let private resolveConflict
        (deps: OrchestratorProgramDeps)
        (job: ManagerJob)
        (conflict:
            {| CandidateCommit: CommitHash
               ConflictFiles: string list |})
        =
        taskResult {
            let prompt = OrchestratorPrompts.buildConflictResumePrompt conflict.ConflictFiles

            do!
                deps.Manager.ResumeManager job.JobId job.Worktree.Path prompt
                |> mapTaskError (fun error -> failed job (sprintf "Conflict resolution failed: %s" error))

            let! verdict = publishEventually deps job 0 |> TaskResultCE.ofTask
            return verdict
        }
        |> mapTask asVerdict

    let private settlePublishAttempt (deps: OrchestratorProgramDeps) (job: ManagerJob) (attempt: PublishAttempt) =
        match attempt with
        | TargetMoved -> publishEventually deps job 0
        | Landed commit -> settleLanded deps job commit

    /// ORCH-005: re-enter the short CAS window for a publish claim.
    let private attemptPublish
        (deps: OrchestratorProgramDeps)
        (job: ManagerJob)
        (claim:
            {| RebasedCommit: CommitHash
               ExpectedHead: CommitHash |})
        =
        taskResult {
            let! attempt = publishUnderGate deps job claim.ExpectedHead
            let! verdict = settlePublishAttempt deps job attempt |> TaskResultCE.ofTask
            return verdict
        }
        |> mapTask asVerdict

    /// ORCH-007 branch 1: the ff already happened and only the fact is missing.
    /// Written without re-acquiring the gate — there is no ref mutation left to
    /// protect, and taking the lock would block a job that still has real work.
    let private backfillPublished
        (deps: OrchestratorProgramDeps)
        (job: ManagerJob)
        (landed:
            {| RebasedCommit: CommitHash
               ResultingTargetHead: CommitHash |})
        =
        taskResult {
            do!
                append
                    deps
                    job
                    (OrchestratorFact.Published
                        {| ManagerJobId = job.JobId
                           CandidateCommit = landed.RebasedCommit
                           ResultingTargetHead = landed.ResultingTargetHead |})

            do!
                releaseTerminalWorktree deps job
                |> mapTask (
                    Result.mapError (fun error ->
                        failed job (sprintf "Backfilled Published but cleanup failed: %s" error))
                )

            return OrchestratorVerdict.Published(job.JobId, landed.ResultingTargetHead)
        }
        |> mapTask asVerdict

    /// Terminal job: release the worktree, nothing is owed.
    let private cleanUp (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        taskResult {
            do!
                releaseTerminalWorktree deps job
                |> mapTaskError (fun error -> failed job (sprintf "Terminal job cleanup failed: %s" error))

            return OrchestratorVerdict.Empty
        }
        |> mapTask asVerdict

    /// ORCH-008: read the target head; fail closed on failure rather than
    /// falling back to HEAD.
    let private readTargetHead (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        task {
            let! head = deps.Git.GetTargetHead job.TargetRef
            return Result.toOption head
        }

    /// ORCH-007: re-enter from a rebased candidate. Reads the target head and
    /// classifies the reality to decide the CE effect.
    let private reenterRebasedCandidate
        (deps: OrchestratorProgramDeps)
        (job: ManagerJob)
        (rebased:
            {| RebasedCommit: CommitHash
               TargetHeadSnapshot: CommitHash
               PostRebaseReviewBarrierId: ReviewBarrierId |})
        =
        task {
            let! currentHead = readTargetHead deps job

            match
                OrchestratorProjection.classifyRebasedCandidate
                    currentHead
                    rebased.RebasedCommit
                    rebased.TargetHeadSnapshot
            with
            | RebasedCandidateReality.HeadUnreadable ->
                return failed job "GetTargetHead failed; ORCH-008 forbids falling back to HEAD"
            | RebasedCandidateReality.PublishReady ->
                return!
                    attemptPublish
                        deps
                        job
                        {| RebasedCommit = rebased.RebasedCommit
                           ExpectedHead = rebased.TargetHeadSnapshot |}
            | RebasedCandidateReality.NeedsRebase -> return! publishEventually deps job 0
        }

    /// ORCH-007: re-enter from a publish claim. Reads the target head and
    /// classifies the three CAS branches in fixed order.
    let private reenterPublishClaim
        (deps: OrchestratorProgramDeps)
        (job: ManagerJob)
        (claim:
            {| RebasedCommit: CommitHash
               ExpectedHead: CommitHash |})
        =
        task {
            let! currentHead = readTargetHead deps job

            match OrchestratorProjection.classifyPublishClaim currentHead claim.RebasedCommit claim.ExpectedHead with
            | PublishClaimReality.HeadUnreadable ->
                return failed job "GetTargetHead failed; ORCH-008 forbids falling back to HEAD"
            | PublishClaimReality.AlreadyFastForwarded ->
                let head = Option.get currentHead

                return!
                    backfillPublished
                        deps
                        job
                        {| RebasedCommit = claim.RebasedCommit
                           ResultingTargetHead = head |}
            | PublishClaimReality.PublishReady ->
                return!
                    attemptPublish
                        deps
                        job
                        {| RebasedCommit = claim.RebasedCommit
                           ExpectedHead = claim.ExpectedHead |}
            | PublishClaimReality.ClaimExpired -> return! publishEventually deps job 0
        }

    /// ORCH-007: one entry for fresh start and restart. Later durable evidence
    /// proves that earlier obligations were discharged; head-dependent evidence
    /// is then checked against current Git reality by the re-entry functions.
    let private program (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        task {
            let record =
                OrchestratorProjection.tryFind job.JobId (deps.Snapshot()).AgentProjections.Orchestrator

            let terminal = record |> Option.bind (fun value -> value.Terminal)
            let publishClaim = record |> Option.bind (fun value -> value.PublishClaimed)

            let rebasedCandidate =
                record |> Option.bind (fun value -> value.RebasedCandidateReady)

            let conflict = record |> Option.bind (fun value -> value.ConflictDetected)
            let candidate = record |> Option.bind (fun value -> value.CandidateReady)

            match terminal, publishClaim, rebasedCandidate, conflict, candidate with
            | Some _, _, _, _, _ -> return! cleanUp deps job
            | None, Some claim, _, _, _ -> return! reenterPublishClaim deps job claim
            | None, None, Some rebased, _, _ -> return! reenterRebasedCandidate deps job rebased
            | None, None, None, Some conflict, _ ->
                return!
                    resolveConflict
                        deps
                        job
                        {| CandidateCommit = conflict.CandidateCommit
                           ConflictFiles = conflict.ConflictFiles |}
            | None, None, None, None, Some _ -> return! publishEventually deps job 0
            | None, None, None, None, None -> return! awaitAndPublish deps job
        }

    let run (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        task {
            try
                return! program deps job
            with
            | :? OperationCanceledException -> return failed job "cancelled"
            | error -> return failed job (sprintf "%A" error)
        }
