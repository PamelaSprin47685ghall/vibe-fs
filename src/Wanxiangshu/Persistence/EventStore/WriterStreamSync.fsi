namespace Wanxiangshu.Persistence.EventStore

open System.Threading.Tasks

[<RequireQualifiedAccess>]
module WriterStreamSync =
    val retentionMilliseconds: unit -> float
    val isWriterActiveAt: nowMs: float -> lastActivityMs: float -> bool
    val tryCachedLocalSnapshot: commonDir: string -> StoreSnapshot option
    val materializeLocalAt: raw: IGitRawStore -> commonDir: string -> nowMs: float -> Task<StoreSnapshot>
    val materializeLocal: raw: IGitRawStore -> commonDir: string -> Task<StoreSnapshot>

    val syncWriterStreamsAt:
        raw: IGitRawStore ->
        commonDir: string ->
        remote: StoreSnapshot option ->
        nowMs: float ->
            Task<Result<StoreSnapshot, ConvergeError>>

    val syncWriterStreams:
        raw: IGitRawStore ->
        commonDir: string ->
        remote: StoreSnapshot option ->
            Task<Result<StoreSnapshot, ConvergeError>>
