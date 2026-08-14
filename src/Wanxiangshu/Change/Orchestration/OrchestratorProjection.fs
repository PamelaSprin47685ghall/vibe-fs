namespace Wanxiangshu.Change.Orchestration

open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// What actually happened to a ManagerJob, most recent fact only.
///
/// `RequireQualifiedAccess` is not style. `Published`, `Failed` and `Abandoned`
/// also name cases of `TerminalOutcome`, and an unqualified `Failed reason` in a
/// session-completion path resolved to THIS union — the compiler reported
/// "expected TerminalOutcome but here has JobProgress" in three unrelated files.
/// A bare case name meaning two things across two domains is the double model
/// ARCH-001 forbids, and qualification is how the reader tells them apart.
///
/// ORCH-006 forbids a shape where the recovery action is ambiguous. The old
/// projection held five independent optional fields (PreRebaseReviewCommit,
/// RebasedCommit, ConflictFiles, PostRebaseReviewCommit, PublishClaimHead), so
/// recovery had to guess an ordering from which combination happened to be set —
/// and "candidate registered" could mean either "waiting for review" or "ready
/// to publish".
///
/// This is NOT a program counter (ARCH-001). Every case carries physical
/// evidence that exists in the world: a commit, a target head snapshot, a review
/// barrier, a set of conflicted files. None of them says where the program
/// should go next; ORCH-007 derives that, and it derives it by matching one
/// value instead of ranking five.
[<RequireQualifiedAccess>]
type JobProgress =
    /// ManagerJobCreated. Worktree exists, Manager has produced nothing yet.
    | ManagerStarted
    /// CandidateReady. A candidate commit exists with a pre-rebase witness.
    | CandidateReady of
        {| CandidateCommit: CommitHash
           PreRebaseReviewBarrierId: ReviewBarrierId |}
    /// ConflictDetected. ORCH-003: the SAME Manager resolves it, in the same
    /// worktree. Without this case, recovery cannot tell an in-progress conflict
    /// resolution from a job that never produced a candidate.
    | ConflictPending of
        {| CandidateCommit: CommitHash
           TargetHeadSnapshot: CommitHash
           ConflictFiles: string list
           DiagnosticsDigest: string |}
    /// RebasedCandidateReady. Rebased onto a known head and re-reviewed.
    | RebasedCandidateReady of
        {| RebasedCommit: CommitHash
           TargetHeadSnapshot: CommitHash
           PostRebaseReviewBarrierId: ReviewBarrierId |}
    /// PublishClaimed. Written inside the short CAS window (ORCH-005), so the
    /// ref mutation may or may not have happened.
    | PublishClaimed of
        {| RebasedCommit: CommitHash
           ExpectedHead: CommitHash |}
    /// Published. Terminal success.
    | Published of
        {| CandidateCommit: CommitHash
           ResultingTargetHead: CommitHash |}
    /// Terminal failure.
    | Failed of reason: string
    /// Terminal, deliberate.
    | Abandoned

/// One ManagerJob: one worktree, one Manager, for its whole life (ORCH-003).
type ManagerJobProjection =
    {
        ManagerJobId: ManagerJobId
        ManagerSessionId: SessionId
        /// ORCH-003: persisted so recovery restores `fast-manager` or
        /// `deep-manager` rather than degrading to a bare role.
        ManagerAgent: string
        /// EXEC-029: provider-facing stable road name. ManagerAgent remains the
        /// Host machine binding used for recovery and execution.
        Byname: string
        /// ORCH-006: recovery locates the worktree by identity. The path is
        /// diagnostic — it is mutable state, and a moved worktree must not orphan
        /// a job.
        WorktreeIdentity: WorktreeIdentity
        WorktreePath: WorktreePath
        TargetRef: TargetRef
        /// ORCH-008: frozen by `git symbolic-ref` at fork time. GetTargetHead
        /// failure must fail closed, never fall back to HEAD.
        TargetBranchFrozen: string
        Progress: JobProgress
    }

/// PERSIST-009 worktree create: Requested → Created markers, keyed by the
/// deterministic `WorktreeIdentity` (`manager/<job>`). Not a program counter —
/// physical evidence of an external side effect's claim window.
[<RequireQualifiedAccess>]
type WorktreeEffectStatus =
    | Requested of
        {| ManagerJobId: ManagerJobId
           WorktreePath: WorktreePath |}
    | Created of
        {| ManagerJobId: ManagerJobId
           WorktreePath: WorktreePath |}

/// PERSIST-008: keyed lookup, no history scan. Terminal jobs stay in the map so
/// re-folding a Published fact is recognised as a duplicate rather than
/// resurrecting a fresh entry; `activeJobs` filters them out.
///
/// `WorktreeEffects` is the PERSIST-009 claim window for git worktree add:
/// Requested-without-Created is "not happened" for retry; Created proves the
/// branch exists. Reconcile remains OrchestratorSweep + `git worktree list
/// --porcelain` (owned set = activeJobs identities).
type OrchestratorProjection =
    { Jobs: Map<ManagerJobId, ManagerJobProjection>
      WorktreeEffects: Map<WorktreeIdentity, WorktreeEffectStatus> }

/// ORCH-007: exactly one action per job, derived from its last fact.
type JobRecoveryAction =
    /// Resume the Manager from the existing worktree.
    | ResumeManager
    /// Enter rebaseReviewPublishLoop with this candidate.
    | RebaseReviewPublish of CommitHash
    /// Hand the conflict back to the same Manager in the same worktree.
    | ResumeConflictResolution of
        {| CandidateCommit: CommitHash
           ConflictFiles: string list |}
    /// Acquire the short gate and re-read the head before ff-only.
    | AttemptPublish of
        {| RebasedCommit: CommitHash
           ExpectedHead: CommitHash |}
    /// ORCH-007 branch 1: the ff already happened, only the fact is missing.
    | BackfillPublished of
        {| RebasedCommit: CommitHash
           ResultingTargetHead: CommitHash |}
    /// ORCH-007 branch 3: the claim expired. Discard it AND the post-rebase
    /// witness (REVIEW-008), then rebase and review again.
    | RebaseAndReviewAgain
    /// Clean up the worktree; nothing is owed.
    | CleanUp
    /// GetTargetHead failed. ORCH-008: fail closed rather than guess.
    | FailClosed of reason: string

module OrchestratorProjection =

    let empty =
        { Jobs = Map.empty
          WorktreeEffects = Map.empty }

    let tryFind (jobId: ManagerJobId) (projection: OrchestratorProjection) = Map.tryFind jobId projection.Jobs

    let tryFindByByname (byname: string) (projection: OrchestratorProjection) =
        if System.String.IsNullOrWhiteSpace byname then
            None
        else
            let wanted = byname.Trim()

            projection.Jobs
            |> Map.tryPick (fun _ job ->
                if System.String.Equals(job.Byname, wanted, System.StringComparison.OrdinalIgnoreCase) then
                    Some job
                else
                    None)

    let tryWorktreeEffect (identity: WorktreeIdentity) (projection: OrchestratorProjection) =
        Map.tryFind identity projection.WorktreeEffects

    /// PERSIST-009: claim before `git worktree add`. Idempotent — a second
    /// request for the same identity keeps the first marker (including Created).
    /// Accept→Requested regression is refused: once Created, request is a no-op.
    let requestWorktree
        (identity: WorktreeIdentity)
        (path: WorktreePath)
        (jobId: ManagerJobId)
        (projection: OrchestratorProjection)
        =
        match Map.tryFind identity projection.WorktreeEffects with
        | Some _ -> projection
        | None ->
            { projection with
                WorktreeEffects =
                    Map.add
                        identity
                        (WorktreeEffectStatus.Requested
                            {| ManagerJobId = jobId
                               WorktreePath = path |})
                        projection.WorktreeEffects }

    /// PERSIST-009: physical create succeeded. Idempotent on duplicate accept.
    let acceptWorktree
        (identity: WorktreeIdentity)
        (path: WorktreePath)
        (jobId: ManagerJobId)
        (projection: OrchestratorProjection)
        =
        let created =
            WorktreeEffectStatus.Created
                {| ManagerJobId = jobId
                   WorktreePath = path |}

        match Map.tryFind identity projection.WorktreeEffects with
        | Some(WorktreeEffectStatus.Created _) -> projection
        | _ ->
            { projection with
                WorktreeEffects = Map.add identity created projection.WorktreeEffects }

    /// The job a Manager session is running, for callers that hold only the
    /// session id.
    ///
    /// REVIEW-006 requires `ManagerJobId` and `WorktreeIdentity` inside every
    /// confirmed witness, and the reviewer path reaches the Manager by session.
    /// Scanning `Jobs` is bounded by concurrently active jobs, not by history
    /// length, so PERSIST-008 holds; keying by session as well would be a second
    /// index to keep consistent with this one.
    let tryFindByManagerSession (managerSessionId: SessionId) (projection: OrchestratorProjection) =
        projection.Jobs
        |> Map.tryPick (fun _ job ->
            if job.ManagerSessionId = managerSessionId then
                Some job
            else
                None)

    let private isTerminal (progress: JobProgress) =
        match progress with
        | JobProgress.Published _
        | JobProgress.Failed _
        | JobProgress.Abandoned -> true
        | JobProgress.ManagerStarted
        | JobProgress.CandidateReady _
        | JobProgress.ConflictPending _
        | JobProgress.RebasedCandidateReady _
        | JobProgress.PublishClaimed _ -> false

    /// ORCH-004: jobs still owed work. Multiple jobs may rebase and review in
    /// parallel; only the ref mutation serialises.
    let activeJobs (projection: OrchestratorProjection) =
        projection.Jobs
        |> Map.toList
        |> List.map snd
        |> List.filter (fun job -> not (isTerminal job.Progress))

    /// ORCH-003: create a job, once.
    ///
    /// Idempotent for a job that already exists. PERSIST-009's durable-effect
    /// protocol retries after `CommitUnknown`, so one journal can legitimately
    /// carry the same `ManagerJobCreated` twice — and an unconditional overwrite
    /// would reset `Progress` to `ManagerStarted`. A replay of a PUBLISHED job
    /// would then hand ORCH-007 a job that looks freshly created, and recovery
    /// would resume a Manager for work that already landed.
    ///
    /// Keeping the existing entry rather than merging fields is the same rule
    /// `recordProgress` follows: the worktree and the Manager are fixed for the
    /// job's whole life, so a second create has nothing new to say.
    let createJob
        (job:
            {| ManagerJobId: ManagerJobId
               ManagerSessionId: SessionId
               ManagerAgent: string
               Byname: string
               WorktreeIdentity: WorktreeIdentity
               WorktreePath: WorktreePath
               TargetRef: TargetRef
               TargetBranchFrozen: string |})
        (projection: OrchestratorProjection)
        =
        if Map.containsKey job.ManagerJobId projection.Jobs then
            projection
        else
            { projection with
                Jobs =
                    Map.add
                        job.ManagerJobId
                        { ManagerJobId = job.ManagerJobId
                          ManagerSessionId = job.ManagerSessionId
                          ManagerAgent = job.ManagerAgent
                          Byname =
                            if System.String.IsNullOrWhiteSpace job.Byname then
                                job.ManagerAgent
                            else
                                job.Byname
                          WorktreeIdentity = job.WorktreeIdentity
                          WorktreePath = job.WorktreePath
                          TargetRef = job.TargetRef
                          TargetBranchFrozen = job.TargetBranchFrozen
                          Progress = JobProgress.ManagerStarted }
                        projection.Jobs }

    /// Record a job's latest progress.
    ///
    /// Named `recordProgress` rather than `advance`: `advance` belongs to the
    /// fallback cursor's modulo-4 step, and one algorithm name owned by two
    /// modules is how a reader starts assuming they are the same operation.
    ///
    /// ORCH-003 fixes the worktree and the Manager for the job's whole life, so
    /// only `Progress` is ever replaced. A terminal job accepts nothing further,
    /// which makes a replayed `Published` idempotent instead of reopening the job.
    let recordProgress (jobId: ManagerJobId) (progress: JobProgress) (projection: OrchestratorProjection) =
        match Map.tryFind jobId projection.Jobs with
        | None -> projection
        | Some job when isTerminal job.Progress -> projection
        | Some job ->
            { projection with
                Jobs = Map.add jobId { job with Progress = progress } projection.Jobs }

    /// ORCH-007. `currentHead` is None when GetTargetHead failed.
    ///
    /// The three `PublishClaimed` branches are evaluated in the clause's fixed
    /// order: already-published first, then unchanged target, then everything
    /// else. Order matters — checking "unchanged" first would re-attempt an ff
    /// that already succeeded.
    let recoveryAction (currentHead: CommitHash option) (job: ManagerJobProjection) : JobRecoveryAction =
        match job.Progress with
        | JobProgress.ManagerStarted -> ResumeManager

        | JobProgress.CandidateReady candidate -> RebaseReviewPublish candidate.CandidateCommit

        | JobProgress.ConflictPending conflict ->
            ResumeConflictResolution
                {| CandidateCommit = conflict.CandidateCommit
                   ConflictFiles = conflict.ConflictFiles |}

        | JobProgress.RebasedCandidateReady rebased ->
            match currentHead with
            | None -> FailClosed "GetTargetHead failed; ORCH-008 forbids falling back to HEAD"
            | Some head when head = rebased.TargetHeadSnapshot ->
                AttemptPublish
                    {| RebasedCommit = rebased.RebasedCommit
                       ExpectedHead = rebased.TargetHeadSnapshot |}
            // The target moved while this job held no lock, which ORCH-005
            // explicitly allows. The post-rebase witness is now for the wrong
            // base (REVIEW-008), so it must not be reused.
            | Some _ -> RebaseAndReviewAgain

        | JobProgress.PublishClaimed claim ->
            match currentHead with
            | None -> FailClosed "GetTargetHead failed; ORCH-008 forbids falling back to HEAD"
            | Some head when head = claim.RebasedCommit ->
                BackfillPublished
                    {| RebasedCommit = claim.RebasedCommit
                       ResultingTargetHead = head |}
            | Some head when head = claim.ExpectedHead ->
                AttemptPublish
                    {| RebasedCommit = claim.RebasedCommit
                       ExpectedHead = claim.ExpectedHead |}
            | Some _ -> RebaseAndReviewAgain

        | JobProgress.Published _
        | JobProgress.Failed _
        | JobProgress.Abandoned -> CleanUp
