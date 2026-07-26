namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.Identity
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

    let gitPort = ProcessGitPort.createWithRunner OrchestratorGit.run

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
                    | Some guard when
                        guard.LastGitTreeHash = Some(GitTreeHash.create tree)
                        && guard.ConsecutivePerfects >= 1
                        ->
                        return Ok true
                    | Some guard when guard.LastGitTreeHash = Some(GitTreeHash.create tree) ->
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

    let orchestratorBranch () : Task<string> =
        task {
            match detectedBranch with
            | Some branch -> return branch
            | None ->
                let! branch = OrchestratorGit.detectBranch OrchestratorGit.run deps.RepoPath
                detectedBranch <- Some branch
                return branch
        }

    let mutable engineInstance: Orchestrator option = None

    let engine () : Task<Orchestrator> =
        task {
            match engineInstance with
            | Some value -> return value
            | None ->
                let! branch = orchestratorBranch ()

                let value =
                    Orchestrator(
                        gitPort,
                        managerPort,
                        deps.RepoPath,
                        branch,
                        ?journal = (deps.Journal |> Option.map OrchestratorJournalPort.fromAgentJournal)
                    )

                engineInstance <- Some value
                return value
        }

    member _.ForkManagerJob(managerId: string, prompt: string) : Task<Result<string, string>> =
        task {
            let! engine = engine ()
            let! result = engine.ForkManager(managerId, prompt)

            match result with
            | Error verdict -> return Error(sprintf "%A" verdict)
            | Ok handle -> return Ok handle.WorktreePath
        }

    member _.JoinPublished() : Task<string> =
        task {
            let! engine = engine ()
            let! verdict = engine.JoinPublished()
            return sprintf "%A" verdict
        }

    member _.Cancel() = runtime.Cancel()
