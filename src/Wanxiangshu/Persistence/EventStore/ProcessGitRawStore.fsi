namespace Wanxiangshu.Persistence.EventStore

open System
open System.Threading.Tasks

type GitRawRequest =
    { Arguments: string list
      Input: byte[] option
      OutputLimit: int }

type GitRawRunner = GitRawRequest -> Task<GitRawResult>

type ProcessGitRawStore =
    new: _repoPath: string * run: GitRawRunner -> ProcessGitRawStore
    interface IGitRawStore

[<RequireQualifiedAccess>]
module ProcessGitRawStore =
    val createRunner: repoPath: string -> hardLimit: TimeSpan -> GitRawRunner
    val createDefaultRunner: repoPath: string -> GitRawRunner
    val createWithRunner: repoPath: string -> run: GitRawRunner -> IGitRawStore
    val create: repoPath: string -> IGitRawStore
