namespace Wanxiangshu.Persistence.EventStore

open System.Threading.Tasks

[<RequireQualifiedAccess>]
module RetentionSurface =
    val readLastCompleteLine: path: string -> string
    val retainedWriterIdsAt: commonDir: string -> nowMs: float -> string[]

    val remotePayloadNeedsRead:
        cachedStatIdentity: string ->
        cachedOid: string ->
        currentStatIdentity: string ->
        remoteOid: string ->
        isBlob: bool ->
            bool

    val syncAt: repoPath: string -> commonDir: string -> remoteRoot: string -> nowMs: float -> Task<obj>
    val writerSyncAdapterScenario: input: obj -> Task<obj>
