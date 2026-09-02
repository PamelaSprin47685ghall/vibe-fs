namespace Wanxiangshu.Change.Host

open System.Threading.Tasks
open Wanxiangshu.Process

module OrchestratorGit =
    val run: cmd: Command -> Task<int * string * string>

    val command: dir: string -> args: string list -> Command

    val hasRebaseHead: runner: (Command -> Task<int * string * string>) -> worktree: string -> Task<bool>

    val finalizeWorktree:
        runner: (Command -> Task<int * string * string>) ->
        managerId: string ->
        worktree: string ->
            Task<Result<unit, string>>
