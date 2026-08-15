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
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// ORCH-004/005/006/007: worktree → review → rebase → fresh review → short-CAS
/// ff-only publish.
///
/// Every branch point reads the durable `JobProgress` (ORCH-006) instead of
/// inspecting which of several optional fields happens to be set. The old shape
/// held five independent options and had to rank them, which is exactly the
/// ambiguous recovery ORCH-006 forbids.
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

    // ── ORCH-007 recovery ───────────────────────────────────────────────────

    /// Pre-rebase review and candidate registration, shared by the fresh run and the
    /// `ResumeManager` recovery path.
    let private afterManager (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        taskResult {
            do! reviewRound deps job "pre-rebase" 0
            do! recordCandidateReady deps job 0
            let! verdict = publishEventually deps job 0 |> TaskResultCE.ofTask
            return verdict
        }
        |> mapTask asVerdict

    let private resumeAwaitManager (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        taskResult {
            do!
                deps.Manager.AwaitManager job.JobId
                |> mapTaskError (fun error -> failed job (sprintf "Manager run failed: %s" error))

            let! verdict = afterManager deps job |> TaskResultCE.ofTask
            return verdict
        }
        |> mapTask asVerdict

    let private resumeConflictResolution
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

    let private resumeAttemptPublish
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

    let private resumeBackfill
        (deps: OrchestratorProgramDeps)
        (job: ManagerJob)
        (landed:
            {| RebasedCommit: CommitHash
               ResultingTargetHead: CommitHash |})
        =
        // ORCH-007 branch 1: the ff already happened and only the fact is
        // missing. Written without re-acquiring the gate — there is no ref
        // mutation left to protect, and taking the lock would block a job that
        // still has real work.
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

    let private resumeCleanUp (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        taskResult {
            do!
                releaseTerminalWorktree deps job
                |> mapTaskError (fun error -> failed job (sprintf "Terminal job cleanup failed: %s" error))

            return OrchestratorVerdict.Empty
        }
        |> mapTask asVerdict

    /// Resume a job from its last durable fact.
    ///
    /// `recoveryAction` decides; this only executes. Adding a second condition here
    /// would put the recovery decision in two places, and ORCH-007's fixed branch
    /// order only holds if there is one.
    let private resumeFromDurableFacts (deps: OrchestratorProgramDeps) (job: ManagerJob) (action: JobRecoveryAction) =
        match action with
        | ResumeManager -> resumeAwaitManager deps job
        | RebaseReviewPublish _ -> publishEventually deps job 0
        | ResumeConflictResolution conflict -> resumeConflictResolution deps job conflict
        | AttemptPublish claim -> resumeAttemptPublish deps job claim
        | BackfillPublished landed -> resumeBackfill deps job landed
        | RebaseAndReviewAgain -> publishEventually deps job 0
        | CleanUp -> resumeCleanUp deps job
        | FailClosed reason -> Task.FromResult(failed job reason)

    /// ORCH-007: recovery decision once durable progress exists past ManagerStarted.
    let private recoveryFromProgress (deps: OrchestratorProgramDeps) (job: ManagerJob) (value: ManagerJobProjection) =
        task {
            match value.Progress with
            | JobProgress.ManagerStarted -> return None
            | _ ->
                let! head = deps.Git.GetTargetHead job.TargetRef
                let currentHead = Result.toOption head
                return Some(OrchestratorProjection.recoveryAction currentHead value)
        }

    /// ORCH-007: the recovery action for a job that already has durable progress.
    ///
    /// `None` for a job whose last fact is `ManagerJobCreated` — that is not a
    /// recovery, it is the ordinary path from the top.
    ///
    /// The target head is read here and handed to `recoveryAction` as an option:
    /// ORCH-008 makes a failed read fail closed, and the projection turns that
    /// `None` into `FailClosed` rather than guessing.
    let private recoveryFor (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        task {
            let record =
                OrchestratorProjection.tryFind job.JobId (deps.Snapshot()).AgentProjections.Orchestrator

            match record with
            | None -> return None
            | Some value -> return! recoveryFromProgress deps job value
        }

    let private startFresh (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        taskResult {
            do!
                deps.Manager.AwaitManager job.JobId
                |> mapTaskError (fun error -> failed job (sprintf "Manager run failed: %s" error))

            let! verdict = afterManager deps job |> TaskResultCE.ofTask
            return verdict
        }
        |> mapTask asVerdict

    let private program (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        task {
            let! action = recoveryFor deps job

            match action with
            | Some recoveryAction -> return! resumeFromDurableFacts deps job recoveryAction
            | None -> return! startFresh deps job
        }

    let run (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        task {
            try
                return! program deps job
            with
            | :? OperationCanceledException -> return failed job "cancelled"
            | error -> return failed job (sprintf "%A" error)
        }
