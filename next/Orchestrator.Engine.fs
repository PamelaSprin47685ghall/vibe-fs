namespace Wanxiangshu.Next.Orchestrator

open System.Threading.Tasks

module OrchestratorEngine =
    let create git manager repoPath targetBranch =
        Orchestrator(git, manager, repoPath, targetBranch)

    let forkManager (orch: Orchestrator) managerId prompt (worktreePath: string option) =
        orch.ForkManager(managerId, prompt, ?worktreePath = worktreePath)

    let joinPublished (orch: Orchestrator) = orch.JoinPublished()
