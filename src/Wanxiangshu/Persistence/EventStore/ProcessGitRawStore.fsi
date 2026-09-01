namespace Wanxiangshu.Persistence.EventStore

open System.Threading.Tasks

type GitRawRunner = string list * byte[] option -> Task<int * byte[] * string>

type ProcessGitRawStore =
    new: _repoPath: string * run: GitRawRunner -> ProcessGitRawStore
    interface IGitRawStore

[<RequireQualifiedAccess>]
module ProcessGitRawStore =
    val createDefaultRunner: repoPath: string -> GitRawRunner
    val createWithRunner: repoPath: string -> run: GitRawRunner -> IGitRawStore
    val create: repoPath: string -> IGitRawStore
