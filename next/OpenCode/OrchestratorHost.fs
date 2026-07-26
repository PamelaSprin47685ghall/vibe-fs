namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Orchestrator

/// Production wiring: an Orchestrator-role session drives the real ManagerJob
/// publish chain instead of forking a generic child session.
type OrchestratorHostDeps =
    { Sessions: ISessionHostPort
      Journal: AgentJournal option
      ModelConfig: ModelResolver.ModelConfig option
      OnChildCreated: string -> AgentRole -> SessionId -> unit
      RegisterReviewerTree: string -> GitTreePort -> unit
      RepoPath: string
      TargetBranch: string }

type OrchestratorHost(deps: OrchestratorHostDeps, orchestratorId: SessionId) =
    let orchestratorKey = SessionId.value orchestratorId
    let worktrees = Dictionary<string, string>()
    let stash = Dictionary<string, RunCompletion>()

    let gitPort = ProcessGitPort.createWithRepo deps.RepoPath OrchestratorGit.run
    let authorityPort = OrchestratorAuthority.createPort ()

    let onChildCreated (agentId: string) (role: AgentRole) (childId: SessionId) =
        if role = AgentRole.Reviewer then
            match worktrees.TryGetValue agentId with
            | true, path -> deps.RegisterReviewerTree (SessionId.value childId) (GitTree.create path)
            | false, _ -> ()

        deps.OnChildCreated agentId role childId

    let runtime =
        HostForkRuntime(
            orchestratorId,
            deps.Sessions,
            ?journal = deps.Journal,
            onChildCreated = onChildCreated,
            ?modelResolver = deps.ModelConfig,
            directoryFor =
                (fun agentId ->
                    match worktrees.TryGetValue agentId with
                    | true, path -> Some path
                    | false, _ -> None)
        )

    let awaitAgent (agentId: string) : Task<Result<RunCompletion, string>> =
        task {
            match stash.TryGetValue agentId with
            | true, completion ->
                stash.Remove agentId |> ignore
                return Ok completion
            | false, _ ->
                let mutable found: Result<RunCompletion, string> option = None

                while found.IsNone do
                    let! joined = runtime.Join()

                    match joined with
                    | Error err -> found <- Some(Error(sprintf "%A" err))
                    | Ok completion when completion.AgentId = agentId -> found <- Some(Ok completion)
                    | Ok completion -> stash.[completion.AgentId] <- completion

                return found.Value
        }

    let runManager (managerId: string) (worktree: string) (prompt: string) : Task<Result<unit, string>> =
        task {
            if not (worktrees.ContainsKey managerId) then
                worktrees.[managerId] <- worktree

            let! forked = runtime.Fork(managerId, AgentRole.Manager, prompt)

            match forked with
            | Error err -> return Error err
            | Ok _ ->
                let! completion = awaitAgent managerId

                match completion with
                | Error err -> return Error err
                | Ok run ->
                    match run.Outcome with
                    | Error err -> return Error err
                    | Ok _ -> return! OrchestratorGit.finalizeWorktree OrchestratorGit.run managerId worktree
        }

    /// Read the Reviewer guard for this Orchestrator's own journal (in-memory
    /// projection). The Reviewer verdict is written to this same journal by the
    /// verdict tool surface, so the projection is authoritative — no disk scan.
    let reviewState (worktree: string) : Task<Result<bool, string>> =
        task {
            match deps.Journal with
            | None -> return Error "Orchestrator review requires a journal"
            | Some journal ->
                let tree = (GitTree.create worktree).GetTreeHash()
                let snapshot = AgentJournal.snapshot journal

                match Map.tryFind orchestratorId snapshot.AgentProjections.Sessions with
                | Some session ->
                    match session.ReviewGuard with
                    | Some guard when guard.LastGitTreeHash = Some(GitTreeHash.create tree) && guard.IsConfirmed ->
                        // Two distinct-ToolCallId PERFECTs on the same tree.
                        return Ok true
                    | Some guard when
                        guard.LastGitTreeHash = Some(GitTreeHash.create tree)
                        && guard.ConsecutivePerfects >= 1
                        ->
                        // One PERFECT so far; needs a second distinct verdict.
                        return Ok false
                    | Some guard when guard.LastGitTreeHash = Some(GitTreeHash.create tree) ->
                        // A REVISE was the last verdict on this tree.
                        return Error "Reviewer requested revision"
                    | _ -> return Ok false
                | None -> return Ok false
        }

    let runReviewerOnce (managerId: string) (worktree: string) (prompt: string) : Task<Result<unit, string>> =
        task {
            let reviewerId = sprintf "%s-reviewer" managerId
            worktrees.[reviewerId] <- worktree

            let! forked = runtime.Fork(reviewerId, AgentRole.Reviewer, prompt)

            match forked with
            | Error err -> return Error err
            | Ok _ ->
                let! completion = awaitAgent reviewerId

                match completion with
                | Error err -> return Error err
                | Ok run ->
                    match run.Outcome with
                    | Error err -> return Error err
                    | Ok _ -> return Ok()
        }

    let reverify (managerId: string) (worktree: string) : Task<Result<unit, string>> =
        task {
            let prompt =
                "Review the current worktree for correctness. Submit your verdict with the verdict tool."

            let! ran = runReviewerOnce managerId worktree prompt

            match ran with
            | Error err -> return Error err
            | Ok() ->
                let! state = reviewState worktree

                match state with
                | Error err -> return Error err
                | Ok true -> return Ok()
                | Ok false ->
                    let! nudged =
                        runReviewerOnce
                            managerId
                            worktree
                            "You produced no verdict. Submit your verdict with the verdict tool."

                    match nudged with
                    | Error err -> return Error err
                    | Ok() ->
                        let! retry = reviewState worktree

                        match retry with
                        | Error err -> return Error err
                        | Ok true -> return Ok()
                        | Ok false -> return Error "Reviewer produced no verdict"
        }

    let managerPort: ManagerPort =
        { RunManager = runManager
          Reverify = reverify }

    let mutable detectedBranch: string option =
        if String.IsNullOrWhiteSpace deps.TargetBranch then
            None
        else
            Some deps.TargetBranch

    let orchestratorBranch () : Task<Result<string, string>> =
        task {
            match detectedBranch with
            | Some branch -> return Ok branch
            | None ->
                let! branchResult = OrchestratorGit.detectBranch OrchestratorGit.run deps.RepoPath

                match branchResult with
                | Ok branch -> detectedBranch <- Some branch
                | Error _ -> ()

                return branchResult
        }

    let mutable engineInstance: Orchestrator option = None

    let engine () : Task<Result<Orchestrator, string>> =
        task {
            match engineInstance with
            | Some value -> return Ok value
            | None ->
                let! branchResult = orchestratorBranch ()

                match branchResult with
                | Error reason -> return Error reason
                | Ok branch ->
                    do!
                        OrchestratorAuthority.reconcilePublishedFromAuthority
                            deps.Journal
                            authorityPort
                            deps.RepoPath
                            branch

                    let value =
                        Orchestrator(
                            gitPort,
                            managerPort,
                            deps.RepoPath,
                            branch,
                            ?journal = (deps.Journal |> Option.map OrchestratorJournalPort.fromAgentJournal),
                            ?authority = Some authorityPort
                        )

                    engineInstance <- Some value

                    match deps.Journal with
                    | Some journal ->
                        let jobs = (AgentJournal.snapshot journal).AgentProjections.Orchestrator.ManagerJobs

                        for KeyValue(managerId, job) in jobs do
                            let id = ManagerId.value managerId
                            worktrees.[id] <- job.WorktreePath
                            value.RecoverManagerJob(id, job.WorktreePath, job.Prompt, job.CandidateCommit.IsSome)
                    | None -> ()

                    return Ok value
        }

    member _.ForkManagerJob(managerId: string, prompt: string) : Task<Result<string, string>> =
        task {
            let! engineResult = engine ()

            match engineResult with
            | Error reason -> return Error reason
            | Ok engine ->
                let! result = engine.ForkManager(managerId, prompt)

                match result with
                | Error verdict -> return Error(sprintf "%A" verdict)
                | Ok handle -> return Ok handle.WorktreePath
        }

    member _.JoinPublished() : Task<string> =
        task {
            let! engineResult = engine ()

            match engineResult with
            | Error reason -> return sprintf "Orchestrator init failed: %s" reason
            | Ok engine ->
                let! verdict = engine.JoinPublished()
                return sprintf "%A" verdict
        }

    member _.Cancel() = runtime.Cancel()
