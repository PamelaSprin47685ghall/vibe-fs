namespace Wanxiangshu.Change.Host

open System.Threading.Tasks
open Wanxiangshu.Process

module OrchestratorGit =
    val createRunner: run: RunProcess -> TextProcessRunner

    val command: dir: string -> args: string list -> Command

    val hasRebaseHead: runner: TextProcessRunner -> worktree: string -> Task<Result<bool, string>>

    val finalizeWorktree:
        runner: TextProcessRunner -> managerId: string -> worktree: string -> Task<Result<unit, string>>
