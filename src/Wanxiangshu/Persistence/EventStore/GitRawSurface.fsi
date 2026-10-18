namespace Wanxiangshu.Persistence.EventStore

open System.Threading.Tasks

[<RequireQualifiedAccess>]
module GitRawSurface =
    val create: repoPath: string -> obj
    val createWithRunner: repoPath: string -> run: (obj -> Task<obj>) -> obj

    val run:
        repoPath: string ->
        hardLimitMs: float ->
        arguments: string[] ->
        input: byte[] option ->
        outputLimit: int ->
            Task<obj>
