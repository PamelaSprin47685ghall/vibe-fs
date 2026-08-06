namespace Wanxiangshu.Orchestrator

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Host
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact

open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

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

    let private append (deps: OrchestratorProgramDeps) (job: ManagerJob) fact =
        match deps.AppendFact StreamId.Workspace fact with
        | Ok() -> Ok()
        | Error error -> Error(failed job error)

    /// REVIEW-008: one barrier per review round, never reused.
    ///
    /// `round` is part of the id because a post-rebase review can run several times
    /// against the same tree when the target keeps moving, and each of those must be
    /// a fresh barrier with two new tool calls. Deriving the id from the tree hash
    /// instead would make round two look like a replay of round one.
    let private barrierId (job: ManagerJob) (phase: string) (round: int) =
        ReviewBarrierId.create (sprintf "%s:%s:%d" (ManagerJobId.value job.JobId) phase round)

    let private readHead (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        task {
            match! deps.Git.ReadHead job.Worktree.Path with
            | Ok head -> return Ok head
            | Error error -> return Error(failed job (sprintf "Git head lookup failed: %s" error))
        }

    /// One review barrier. `Reverify` returns only once a dual PERFECT is confirmed
    /// for the current tree, so there is nothing to re-check here.
    let private reviewRound (deps: OrchestratorProgramDeps) (job: ManagerJob) (phase: string) (round: int) =
        task {
            match!
                deps.Manager.Reverify job.JobId job.ManagerSessionId job.Worktree.Path (barrierId job phase round)
            with
            | Ok() -> return Ok()
            | Error error -> return Error(OrchestratorVerdict.NeedsReview(job.JobId, error))
        }

    // ── ORCH-006 fact writers ───────────────────────────────────────────────

    let private recordCandidateReady (deps: OrchestratorProgramDeps) (job: ManagerJob) (round: int) =
        task {
            match! readHead deps job with
            | Error verdict -> return Error verdict
            | Ok candidate ->
                return
                    append
                        deps
                        job
                        (AgentFact.CandidateReady
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
        task {
            match! readHead deps job with
            | Error verdict -> return Error verdict
            | Ok rebased ->
                return
                    append
                        deps
                        job
                        (AgentFact.RebasedCandidateReady
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
            (AgentFact.ConflictDetected
                {| ManagerJobId = job.JobId
                   CandidateCommit = candidate
                   TargetHeadSnapshot = targetHead
                   ConflictFiles = files
                   DiagnosticsDigest = HostDigest.sha256Hex (String.Join("\n", files)) |})

    // ── rebase ──────────────────────────────────────────────────────────────

    /// Rebase onto the target head, handing any conflict back to the SAME Manager
    /// in the SAME worktree (ORCH-003).
    ///
    /// Returns the target head it rebased onto, so the caller records the snapshot
    /// the post-rebase witness belongs to. Re-reading it later could observe a
    /// different value, and the witness would then claim a base it never had.
    let private rebaseOnto (deps: OrchestratorProgramDeps) (job: ManagerJob) (targetHead: CommitHash) =
        task {
            match! deps.Git.Rebase job.Worktree.Path job.TargetRef with
            | Ok() -> return Ok()
            | Error rebaseError ->
                match! deps.Git.ConflictedFiles job.Worktree.Path with
                | Error error -> return Error(failed job (sprintf "Conflict-file lookup failed: %s" error))
                | Ok files ->
                    match! readHead deps job with
                    | Error verdict -> return Error verdict
                    | Ok candidate ->
                        match recordConflict deps job candidate targetHead files with
                        | Error verdict -> return Error verdict
                        | Ok() ->
                            let prompt = OrchestratorPrompts.buildConflictResumePrompt files

                            match! deps.Manager.ResumeManager job.JobId job.Worktree.Path prompt with
                            | Error error ->
                                return
                                    Error(
                                        failed
                                            job
                                            (sprintf
                                                "Rebase conflict (%s); manager continuation failed: %s"
                                                rebaseError
                                                error)
                                    )
                            | Ok() ->
                                match! deps.Git.Rebase job.Worktree.Path job.TargetRef with
                                | Ok() -> return Ok()
                                | Error error ->
                                    return Error(failed job (sprintf "Rebase continuation failed: %s" error))
        }

    // ── ORCH-005 short CAS window ───────────────────────────────────────────

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
        task {
            match! deps.Git.GetTargetHead job.TargetRef with
            | Error error -> return Error(failed job (sprintf "Git target head lookup failed: %s" error))
            | Ok current when current <> expectedHead -> return Ok TargetMoved
            | Ok current ->
                let claim =
                    AgentFact.PublishClaimed
                        {| ManagerJobId = job.JobId
                           TargetRef = job.TargetRef
                           ExpectedHead = current |}

                match append deps job claim with
                | Error verdict -> return Error verdict
                | Ok() ->
                    match! deps.Git.FfMerge job.Worktree.Path job.TargetRef current with
                    | Error error when error = OrchestratorConstants.targetRefMovedError -> return Ok TargetMoved
                    | Error error -> return Error(failed job (sprintf "FF merge failed: %s" error))
                    | Ok landed ->
                        let published =
                            AgentFact.Published
                                {| ManagerJobId = job.JobId
                                   CandidateCommit = landed
                                   ResultingTargetHead = landed |}

                        match append deps job published with
                        | Error verdict -> return Error verdict
                        | Ok() ->
                            do! deps.Manager.TerminateChildren job.JobId
                            return Ok(Landed landed)
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

    /// ORCH-005: rebase → fresh dual PERFECT → short-gate ff. On a moved target the
    /// whole round repeats, and the previous post-rebase witness is abandoned
    /// (REVIEW-008) rather than reused.
    let rec private rebaseReviewPublish (deps: OrchestratorProgramDeps) (job: ManagerJob) (round: int) =
        task {
            match! deps.Git.GetTargetHead job.TargetRef with
            | Error error -> return failed job (sprintf "Git target head lookup failed: %s" error)
            | Ok targetHead ->
                match! rebaseOnto deps job targetHead with
                | Error verdict -> return verdict
                | Ok() ->
                    match! reviewRound deps job "post-rebase" round with
                    | Error verdict -> return verdict
                    | Ok() ->
                        match! recordRebasedReady deps job targetHead round with
                        | Error verdict -> return verdict
                        | Ok() ->
                            match! publishUnderGate deps job targetHead with
                            | Error verdict -> return verdict
                            | Ok TargetMoved -> return! rebaseReviewPublish deps job (round + 1)
                            | Ok(Landed commit) ->
                                match! job.Worktree.Release() with
                                | Ok() -> return OrchestratorVerdict.Published(job.JobId, commit)
                                | Error error ->
                                    return
                                        failed
                                            job
                                            (sprintf
                                                "Published %s but cleanup failed: %s"
                                                (CommitHash.value commit)
                                                error)
        }

    // ── ORCH-007 recovery ───────────────────────────────────────────────────

    /// Resume a job from its last durable fact.
    ///
    /// `recoveryAction` decides; this only executes. Adding a second condition here
    /// would put the recovery decision in two places, and ORCH-007's fixed branch
    /// order only holds if there is one.
    let rec private resume (deps: OrchestratorProgramDeps) (job: ManagerJob) (action: JobRecoveryAction) =
        task {
            match action with
            | ResumeManager ->
                match! deps.Manager.AwaitManager job.JobId with
                | Error error -> return failed job (sprintf "Manager run failed: %s" error)
                | Ok() -> return! afterManager deps job
            | RebaseReviewPublish _ -> return! rebaseReviewPublish deps job 0
            | ResumeConflictResolution conflict ->
                let prompt = OrchestratorPrompts.buildConflictResumePrompt conflict.ConflictFiles

                match! deps.Manager.ResumeManager job.JobId job.Worktree.Path prompt with
                | Error error -> return failed job (sprintf "Conflict resolution failed: %s" error)
                | Ok() -> return! rebaseReviewPublish deps job 0
            | AttemptPublish claim ->
                match! publishUnderGate deps job claim.ExpectedHead with
                | Error verdict -> return verdict
                | Ok TargetMoved -> return! rebaseReviewPublish deps job 0
                | Ok(Landed commit) ->
                    match! job.Worktree.Release() with
                    | Ok() -> return OrchestratorVerdict.Published(job.JobId, commit)
                    | Error error ->
                        return
                            failed job (sprintf "Published %s but cleanup failed: %s" (CommitHash.value commit) error)
            | BackfillPublished landed ->
                // ORCH-007 branch 1: the ff already happened and only the fact is
                // missing. Written without re-acquiring the gate — there is no ref
                // mutation left to protect, and taking the lock would block a job that
                // still has real work.
                match
                    append
                        deps
                        job
                        (AgentFact.Published
                            {| ManagerJobId = job.JobId
                               CandidateCommit = landed.RebasedCommit
                               ResultingTargetHead = landed.ResultingTargetHead |})
                with
                | Error verdict -> return verdict
                | Ok() ->
                    do! deps.Manager.TerminateChildren job.JobId

                    match! job.Worktree.Release() with
                    | Ok() -> return OrchestratorVerdict.Published(job.JobId, landed.ResultingTargetHead)
                    | Error error -> return failed job (sprintf "Backfilled Published but cleanup failed: %s" error)
            | RebaseAndReviewAgain -> return! rebaseReviewPublish deps job 0
            | CleanUp ->
                match! job.Worktree.Release() with
                | Ok() -> return OrchestratorVerdict.Empty
                | Error error -> return failed job (sprintf "Terminal job cleanup failed: %s" error)
            | FailClosed reason -> return failed job reason
        }

    /// Pre-rebase review and candidate registration, shared by the fresh run and the
    /// `ResumeManager` recovery path.
    and private afterManager (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        task {
            match! reviewRound deps job "pre-rebase" 0 with
            | Error verdict -> return verdict
            | Ok() ->
                match! recordCandidateReady deps job 0 with
                | Error verdict -> return verdict
                | Ok() -> return! rebaseReviewPublish deps job 0
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
            | Some value ->
                match value.Progress with
                | JobProgress.ManagerStarted -> return None
                | _ ->
                    let! head = deps.Git.GetTargetHead job.TargetRef

                    let currentHead =
                        match head with
                        | Ok commit -> Some commit
                        | Error _ -> None

                    return Some(OrchestratorProjection.recoveryAction currentHead value)
        }

    let private program (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        task {
            let! action = recoveryFor deps job

            match action with
            | Some recoveryAction -> return! resume deps job recoveryAction
            | None ->
                match! deps.Manager.AwaitManager job.JobId with
                | Error error -> return failed job (sprintf "Manager run failed: %s" error)
                | Ok() -> return! afterManager deps job
        }

    let run (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        task {
            try
                return! program deps job
            with
            | :? OperationCanceledException -> return failed job "cancelled"
            | error -> return failed job (sprintf "%A" error)
        }
