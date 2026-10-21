namespace Wanxiangshu.Persistence.EventStore

open System.Threading.Tasks

[<RequireQualifiedAccess>]
module RetentionSurface =
    val readLastCompleteLine: path: string -> string
    val retainedWriterIdsAt: stateRoot: string -> nowMs: float -> string[]
    val writerEventIds: stateRoot: string -> obj array
    val reorganizeWithSegmentCap: stateRoot: string -> capBytes: int -> Task<obj>
