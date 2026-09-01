namespace Wanxiangshu.Persistence.EventStore

[<RequireQualifiedAccess>]
module EventKWayMerge =
    val merge: streams: (string * EventEnvelope list) list -> Result<EventEnvelope list, StorageInvalid>
    val mergeRetained: streams: (string * EventEnvelope list) list -> Result<EventEnvelope list, StorageInvalid>
