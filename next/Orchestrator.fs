namespace Wanxiangshu.Next.Orchestrator

open System.Collections.Generic
open System.IO
open System.Threading.Tasks
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity

/// Runtime owner for ManagerJob resources. Every job runs the sequential
/// OrchestratorProgram; the mailbox contains only final post-FF verdicts.
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
        match journalPort with
        | None -> Ok()
        | Some port ->
            match port.AppendFact stream fact with
            | Ok _ -> Ok()
            | Error error -> Error error

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
        (prompt: string)
        (worktreePath: WorktreePath option)
        : Task<Result<OrchestratorHandle, OrchestratorVerdict>> =
        task {
            let! dirty = git.IsDirty(WorktreePath.create repoPath)

            if dirty then
                return Error(OrchestratorVerdict.RejectedDirty "Worktree is dirty")
            else
                let path = defaultArg worktreePath (defaultWorktreePath jobId)

                match! WorktreeResource.Create(git, jobId, path) with
                | Error error ->
                    return
                        Error(
                            OrchestratorVerdict.IntegrationFailed(jobId, sprintf "Failed to create worktree: %s" error)
                        )
                | Ok worktree ->
                    // The Manager is forked BEFORE the job fact, because ORCH-006
                    // requires the fact to carry its SessionId. A crash here leaves a
                    // Manager with no job, which the next sweep cleans up; the reverse
                    // order would leave a job whose Manager can never be addressed.
                    match!
                        manager.StartManager
                            { JobId = jobId
                              ManagerAgent = managerAgent
                              Worktree = path
                              Prompt = prompt }
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
                            AgentFact.ManagerJobCreated
                                {| ManagerJobId = jobId
                                   ManagerSessionId = managerSessionId
                                   ManagerAgent = managerAgent
                                   WorktreeIdentity = worktree.Identity
                                   WorktreePath = path
                                   TargetRef = targetRef
                                   TargetBranchFrozen = TargetRef.value targetRef |}

                        match appendFact StreamId.Workspace fact with
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
                            let job =
                                { JobId = jobId
                                  ManagerSessionId = managerSessionId
                                  ManagerAgent = managerAgent
                                  TargetRef = targetRef
                                  Worktree = worktree }

                            startPublication job
                            return Ok job.Handle
        }

    member _.ForkManager(jobId: ManagerJobId, managerAgent: string, prompt: string, ?worktreePath: WorktreePath) =
        forkManagerCore jobId managerAgent prompt worktreePath

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

    member _.JoinPublished() =
        task {
            match! mailbox.TryJoin() with
            | Some verdict -> return verdict
            | None -> return OrchestratorVerdict.Empty
        }
