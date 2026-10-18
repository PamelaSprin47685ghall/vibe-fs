namespace Wanxiangshu.Git

open System
open System.Threading.Tasks
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Process

type GitGatewayRunner = string list -> Task<Result<TextProcessExit, ProcessResult>>

/// Work that runs while the cross-process store lock is held: local writer
/// reads, union validation, remote import and the final materialization.
type GitGatewayLocalStage = unit -> Task<Result<StoreSnapshot, ConvergeError>>

[<RequireQualifiedAccess>]
module GitGateway =
    val converge:
        raw: IGitRawStore ->
        commonDir: string ->
        run: GitGatewayRunner ->
        maxRetries: int ->
        remote: string ->
        observedRemote: StoreSnapshot option ->
        withLocalLock: (GitGatewayLocalStage -> Task<Result<StoreSnapshot, ConvergeError>>) ->
        deadline: Deadline ->
        clock: (unit -> DateTimeOffset) ->
            Task<Result<StoreSnapshot, ConvergeError>>

    val createProcessRunner: run: RunProcess -> repoPath: string -> GitGatewayRunner
