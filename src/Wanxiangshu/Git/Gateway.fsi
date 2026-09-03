namespace Wanxiangshu.Git

open System.Threading.Tasks
open Wanxiangshu.Persistence.EventStore

type GitGatewayRunner = string list -> Task<int * string * string>

[<RequireQualifiedAccess>]
module GitGateway =
    val converge:
        raw: IGitRawStore ->
        commonDir: string ->
        run: GitGatewayRunner ->
        maxRetries: int ->
        remote: string ->
        observedRemote: StoreSnapshot option ->
            Task<Result<StoreSnapshot, ConvergeError>>

    val createDefaultRunner: repoPath: string -> GitGatewayRunner
