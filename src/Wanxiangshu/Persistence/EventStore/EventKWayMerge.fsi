namespace Wanxiangshu.Persistence.EventStore

[<RequireQualifiedAccess>]
module EventKWayMerge =
    type MergeObservation =
        { Result: Result<EventEnvelope list, StorageInvalid>
          ReadyComparisons: int64 }

    val merge: streams: (string * EventEnvelope list) list -> Result<EventEnvelope list, StorageInvalid>
    val mergeRetained: streams: (string * EventEnvelope list) list -> Result<EventEnvelope list, StorageInvalid>
    val mergeWithDiagnostics: streams: (string * EventEnvelope list) list -> MergeObservation
