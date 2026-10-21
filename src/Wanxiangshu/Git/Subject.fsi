namespace Wanxiangshu.Git

open System.Threading.Tasks

module GitSubject =
    [<Literal>]
    val Executable: string = "git"

    val execIn: directory: string -> arguments: string array -> Task<string>
    val revParseGitCommonDir: workspace: string -> Task<string>
    val diffHeadBinary: directory: string -> Task<string>
    val lsFilesUntracked: directory: string -> Task<string>
    val lsFilesUntrackedZ: directory: string -> Task<string>
    val statusPorcelainV2Z: directory: string -> Task<string>
    val lsFilesStageZ: directory: string -> Task<string>
    val hashObjectNoFilters: directory: string -> path: string -> Task<string>
    val revParseHeadTree: directory: string -> Task<string>
    val tryRevParseHeadTree: directory: string -> Task<string option>

/// How Git's own output is read (worktree listings, branch lists, rebase state
/// paths). The law has one owner, `wanxiangli/git-porcelain` (CUT-23 ①); this
/// module only adapts it to F# values.
module GitPorcelain =
    /// `git worktree list --porcelain`: (path, full branch ref) per record.
    val worktrees: output: string -> (string * string option) list
    /// Primary working tree of `git worktree list --porcelain -z`; Error "bare" | "unidentified".
    val primaryWorktree: outputZ: string -> Result<string, string>
    /// `git branch --list` without the `*` / `+` checkout markers.
    val branches: output: string -> string list
    val shortBranch: reference: string -> string
    val isManagerBranch: reference: string -> bool
    val managerBranchOf: jobId: string -> string
    /// Rebase state directories to probe from `rev-parse --git-path rebase-merge|rebase-apply`.
    val rebaseStatePaths: merge: (int * string) -> apply: (int * string) -> string list
    /// Worktree cleanup: paths of manager-branch worktrees whose short branch no active job owns.
    val staleWorktrees: entries: (string * string option) list -> owned: string list -> string list
    /// The listed manager branches whose short name no active job owns, as listed.
    val staleBranches: branches: string list -> owned: string list -> string list
