namespace Wanxiangshu.Next.Orchestrator

open System
open System.Collections.Generic
open System.IO
open System.Threading.Tasks
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel.Fact

/// Runtime owner for ManagerJob resources. Every job runs the sequential
/// OrchestratorProgram; the mailbox contains only final post-FF verdicts.
type Orchestrator
    (
        git: GitPort,
        manager: ManagerPort,
        repoPath: string,
        targetBranch: string,
        ?journal: OrchestratorJournalPort,
        ?authority: GitAuthorityPort,
        ?lockRepoPath: string
    ) =

    let mailbox = VerdictMailbox()
    let recovered = Queue<OrchestratorVerdict>()
    let recoveredGate = obj ()
    let journalPort = journal
    let _authorityPort = authority

    let gatePath =
        IntegrationGate.lockPath (defaultArg lockRepoPath repoPath) targetBranch

    let appendFact stream fact =
        match journalPort with
        | None -> Ok()
        | Some port ->
            match port.AppendFact stream fact with
            | Ok _ -> Ok()
            | Error error -> Error error

    let snapshot () =
        journalPort |> Option.map (fun port -> port.Snapshot()) |> Option.defaultValue Fold.empty

    let programDeps: OrchestratorProgramDeps =
        { Git = git
          Manager = manager
          AppendFact = appendFact
          Snapshot = snapshot
          TargetBranch = targetBranch
          GatePath = gatePath }

    let startPublication (job: ManagerJob) =
        mailbox.StartJob()

        task {
            let! verdict = OrchestratorProgram.run programDeps job
            mailbox.Publish verdict
        }
        |> ignore

    let forkManagerCore
        (managerId: string)
        (prompt: string)
        (worktreePath: string option)
        : Task<Result<OrchestratorHandle, OrchestratorVerdict>> =
        task {
            let! dirty = git.IsDirty repoPath

            if dirty then
                return Error(OrchestratorVerdict.RejectedDirty "Worktree is dirty")
            else
                let path =
                    defaultArg
                        worktreePath
                        (Path.Combine(Path.GetTempPath(), sprintf "wanxiangshu-%s" managerId))

                match! WorktreeResource.Create(git, repoPath, managerId, path) with
                | Error error ->
                    return
                        Error(
                            OrchestratorVerdict.IntegrationFailed(
                                managerId,
                                sprintf "Failed to create worktree: %s" error
                            )
                        )
                | Ok worktree ->
                    let fact =
                        AgentFact.OrchestratorManagerJobCreated
                            {| ManagerId = managerId
                               WorktreePath = path
                               Branch = worktree.Branch
                               Prompt = prompt |}

                    match appendFact StreamId.Workspace fact with
                    | Error error ->
                        let! _ = worktree.Release()

                        return
                            Error(
                                OrchestratorVerdict.IntegrationFailed(
                                    managerId,
                                    sprintf "Failed to persist manager job: %s" error
                                )
                            )
                    | Ok() ->
                        let job = ManagerJob.Start(manager, managerId, prompt, worktree)
                        startPublication job
                        return Ok job.Handle
        }

    member _.ForkManager(managerId: string, prompt: string, ?worktreePath: string) =
        forkManagerCore managerId prompt worktreePath

    member _.RecoverPublished(managerId: string, commitHash: string) =
        lock recoveredGate (fun () ->
            recovered.Enqueue(OrchestratorVerdict.Published(managerId, commitHash)))

    member _.RecoverManagerJob(managerId: string, worktreePath: string, prompt: string, managerCompleted: bool) =
        let worktree = WorktreeResource.Adopt(git, managerId, worktreePath)
        let job = ManagerJob.Recover(manager, managerId, prompt, worktree, managerCompleted)
        startPublication job

    member _.JoinPublished() =
        task {
            let recoveredVerdict =
                lock recoveredGate (fun () ->
                    if recovered.Count > 0 then
                        Some(recovered.Dequeue())
                    else
                        None)

            match recoveredVerdict with
            | Some verdict -> return verdict
            | None ->
                match! mailbox.TryJoin() with
                | Some verdict -> return verdict
                | None -> return OrchestratorVerdict.Empty
        }
