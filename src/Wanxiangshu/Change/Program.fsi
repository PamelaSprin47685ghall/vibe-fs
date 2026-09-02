namespace Wanxiangshu.Change

open System.Threading.Tasks

/// ORCH-004/005/006/007: worktree → review → rebase → fresh review → short-CAS
/// ff-only publish.
module OrchestratorProgram =
    val run: deps: OrchestratorProgramDeps -> job: ManagerJob -> Task<OrchestratorVerdict>
