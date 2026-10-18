namespace Wanxiangshu.Git

open System.Threading.Tasks
open Wanxiangshu.Change
open Wanxiangshu.Process

module GitOperations =
    val createWithRepo: repoPath: string -> runner: TextProcessRunner -> GitPort
    val continueRebase: runner: TextProcessRunner -> dir: string -> Task<Result<unit, string>>
    val stageAll: runner: TextProcessRunner -> dir: string -> Task<Result<unit, string>>

    val candidateCommit:
        runner: TextProcessRunner -> dir: string -> msg: string -> Task<Result<unit, string>>

    val createWithRunner: runner: TextProcessRunner -> GitPort
