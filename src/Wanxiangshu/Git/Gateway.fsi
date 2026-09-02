namespace Wanxiangshu.Git

open System.Threading.Tasks
open Wanxiangshu.Persistence.EventStore

type GitGatewayRunner = string list -> Task<int * string * string>

[<RequireQualifiedAccess>]
module GitGateway =
    [<Literal>]
    val SyncActiveEnv: string = "WANXIANG_GIT_SYNC_ACTIVE"

    val discoverRemote:
        run: GitGatewayRunner ->
        remote: string ->
            Task<Result<StoreSnapshot option * GitObjectId option, ConvergeError>>

    val converge:
        raw: IGitRawStore ->
        commonDir: string ->
        run: GitGatewayRunner ->
        maxRetries: int ->
        remote: string ->
        observedRemote: StoreSnapshot option ->
            Task<Result<StoreSnapshot, ConvergeError>>

    val createDefaultRunner: repoPath: string -> GitGatewayRunner
