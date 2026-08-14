namespace Wanxiangshu.Orchestrator

open System.Collections.Generic
open System.IO
open System.Threading.Tasks
open Wanxiangshu.Change.Orchestration
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// Runtime owner for ManagerJob resources. Every job runs the sequential
/// direct-CE OrchestratorProgram workflow; the mailbox contains only final post-FF verdicts.
///
/// ORCH-006's `ManagerJobCreated` is the only writer here, and it is written after
/// the Manager fork returns a `SessionId` but before the program awaits it. That
/// ordering is what makes a crash recoverable in either direction: no fact means no
/// job, and a fact means a live Manager whose session is known.
type Orchestrator
    (
        git: GitPort,
        manager: ManagerPort,
        repoPath: string,
        targetRef: TargetRef,
        ?journal: OrchestratorJournalPort,
        ?lockRepoPath: string
    ) =

    let mailbox = VerdictMailbox()
    let journalPort = journal

    let gatePath =
        IntegrationGate.lockPath (defaultArg lockRepoPath repoPath) (TargetRef.value targetRef)

    let appendFact stream fact =
        task {
            match journalPort with
            | None -> return Ok()
            | Some port ->
                match! port.AppendFact stream fact with
                | Ok _ -> return Ok()
                | Error error -> return Error error
        }

    let snapshot () =
        journalPort
        |> Option.map (fun port -> port.Snapshot())
        |> Option.defaultValue Fold.empty

    let programDeps: OrchestratorProgramDeps =
        { Git = git
          Manager = manager
          AppendFact = appendFact
          Snapshot = snapshot
          GatePath = gatePath }

    let startPublication (job: ManagerJob) =
        mailbox.StartJob()

        task {
            let! verdict = OrchestratorProgram.run programDeps job
            mailbox.Publish verdict
        }
        |> ignore

    let defaultWorktreePath (jobId: ManagerJobId) =
        WorktreePath.create (Path.Combine(Path.GetTempPath(), sprintf "wanxiangshu-%s" (ManagerJobId.value jobId)))

    /// ORCH-004: a new ManagerJob.
    ///
    /// `managerAgent` is required and never defaulted. ORCH-003 keeps one Manager per
    /// job for its whole life, and PROMPT-008 forbids rebuilding a managed name from
    /// a role — a `deep-manager` job resumed as `fast-manager` would carry the wrong
    /// FALLBACK-002 A/B pair for the rest of the run.
    let forkManagerCore
        (jobId: ManagerJobId)
        (managerAgent: string)
        (byname: string)
        (prompt: string)
        (expectedToolCalls: int option)
        (worktreePath: WorktreePath option)
        : Task<Result<OrchestratorHandle, OrchestratorVerdict>> =
        task {
            let! dirty = git.IsDirty(WorktreePath.create repoPath)

            if dirty then
                return Error(OrchestratorVerdict.RejectedDirty "Worktree is dirty")
            else
                let path = defaultArg worktreePath (defaultWorktreePath jobId)
                // PERSIST-009: effect identity is deterministic before git runs.
                let identity = WorktreeCommands.identityOf jobId

                let requestFact =
                    OrchestratorFact.WorktreeCreateRequested
                        {| ManagerJobId = jobId
                           WorktreeIdentity = identity
                           WorktreePath = path |}

                match! appendFact StreamId.Workspace requestFact with
                | Error error ->
                    return
                        Error(
                            OrchestratorVerdict.IntegrationFailed(
                                jobId,
                                sprintf "Failed to persist worktree request: %s" error
                            )
                        )
                | Ok() ->
                    match! WorktreeResource.Create(git, jobId, path) with
                    | Error error ->
                        return
                            Error(
                                OrchestratorVerdict.IntegrationFailed(
                                    jobId,
                                    sprintf "Failed to create worktree: %s" error
                                )
                            )
                    | Ok worktree ->
                        let createdFact =
                            OrchestratorFact.WorktreeCreated
                                {| ManagerJobId = jobId
                                   WorktreeIdentity = worktree.Identity
                                   WorktreePath = path |}

                        match! appendFact StreamId.Workspace createdFact with
                        | Error error ->
                            let! _ = worktree.Release()

                            return
                                Error(
                                    OrchestratorVerdict.IntegrationFailed(
                                        jobId,
                                        sprintf "Failed to persist worktree created: %s" error
                                    )
                                )
                        | Ok() ->
                            // The Manager is forked BEFORE the job fact, because ORCH-006
                            // requires the fact to carry its SessionId. A crash here leaves a
                            // Manager with no job, which the next sweep cleans up; the reverse
                            // order would leave a job whose Manager can never be addressed.
                            match!
                                manager.StartManager
                                    { JobId = jobId
                                      ManagerAgent = managerAgent
                                      Worktree = path
                                      Prompt = prompt
                                      ExpectedToolCalls = expectedToolCalls }
                            with
                            | Error error ->
                                let! _ = worktree.Release()

                                return
                                    Error(
                                        OrchestratorVerdict.IntegrationFailed(
                                            jobId,
                                            sprintf "Failed to start manager: %s" error
                                        )
                                    )
                            | Ok managerSessionId ->
                                let fact =
                                    OrchestratorFact.ManagerJobCreated
                                        {| ManagerJobId = jobId
                                           ManagerSessionId = managerSessionId
                                           ManagerAgent = managerAgent
                                           Byname = byname
                                           WorktreeIdentity = worktree.Identity
                                           WorktreePath = path
                                           TargetRef = targetRef
                                           TargetBranchFrozen = TargetRef.value targetRef |}

                                match! appendFact StreamId.Workspace fact with
                                | Error error ->
                                    let! _ = worktree.Release()

                                    return
                                        Error(
                                            OrchestratorVerdict.IntegrationFailed(
                                                jobId,
                                                sprintf "Failed to persist manager job: %s" error
                                            )
                                        )
                                | Ok() ->
                                    if journalPort.IsSome then
                                        worktree.MarkDurable()

                                    let job =
                                        { JobId = jobId
                                          ManagerSessionId = managerSessionId
                                          ManagerAgent = managerAgent
                                          TargetRef = targetRef
                                          Worktree = worktree }

                                    startPublication job
                                    return Ok job.Handle
        }

    member _.ForkManager
        (
            jobId: ManagerJobId,
            managerAgent: string,
            prompt: string,
            ?worktreePath: WorktreePath,
            ?byname: string,
            ?expectedToolCalls: int
        )
        =
        let providerByname =
            match byname with
            | Some value when not (System.String.IsNullOrWhiteSpace value) -> value.Trim()
            | _ -> managerAgent

        forkManagerCore jobId managerAgent providerByname prompt expectedToolCalls worktreePath

    /// ORCH-007: resume a persisted job. The worktree is adopted by its durable
    /// identity, never recreated, and the Manager is the one the fact names.
    member _.RecoverManagerJob(record: ManagerJobProjection) =
        let worktree =
            WorktreeResource.Adopt(git, record.WorktreeIdentity, record.WorktreePath)

        startPublication
            { JobId = record.ManagerJobId
              ManagerSessionId = record.ManagerSessionId
              ManagerAgent = record.ManagerAgent
              TargetRef = record.TargetRef
              Worktree = worktree }

    /// GLORY-068: reuse an active ManagerJob — the SAME worktree and the SAME
    /// Manager session continue with an appended requirement ("十年修得同船渡").
    /// Refuses terminal jobs; a finished job never revives.
    member this.ContinueManager(jobId: ManagerJobId, prompt: string) : Task<Result<WorktreePath, string>> =
        task {
            let projection = snapshot ()

            match OrchestratorProjection.tryFind jobId projection.AgentProjections.Orchestrator with
            | None -> return Error(sprintf "Unknown manager job: %s" (ManagerJobId.value jobId))
            | Some record ->
                match record.Progress with
                | JobProgress.Published _
                | JobProgress.Failed _
                | JobProgress.Abandoned ->
                    return Error(sprintf "Manager job is no longer active: %s" (ManagerJobId.value jobId))
                | JobProgress.ManagerStarted
                | JobProgress.CandidateReady _
                | JobProgress.ConflictPending _
                | JobProgress.RebasedCandidateReady _
                | JobProgress.PublishClaimed _ ->
                    match! manager.ResumeManager record.ManagerJobId record.WorktreePath prompt with
                    | Error error -> return Error error
                    | Ok() -> return Ok record.WorktreePath
        }

    /// Compatibility single-result join (Empty when idle).
    member _.JoinPublished() =
        task {
            match! mailbox.TryJoin() with
            | Some verdict -> return verdict
            | None -> return OrchestratorVerdict.Empty
        }

    /// EXEC-019: bounded FIFO batch with local interrupt (≠ lifecycle Cancel).
    member _.JoinPublishedBatch(maxCount: int, interrupt: Task<JoinInterruptReason>) =
        mailbox.JoinAvailable(maxCount, interrupt)
