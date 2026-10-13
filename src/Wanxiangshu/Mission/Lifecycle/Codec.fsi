namespace Wanxiangshu.Mission.Lifecycle

open Thoth.Json

[<RequireQualifiedAccess>]
type ReadFault =
    | StorageInvalid of string
    | PairIncomplete of string
    | Rejected of string

[<RequireQualifiedAccess>]
module LifecycleCodec =
    val eventTypeOf: LifecycleEvent -> string
    val encodePayload: LifecycleEvent -> JsonValue
    val decodePayload: eventType: string -> payload: JsonValue -> Result<LifecycleEvent, string>
    val encodeLine: LifecycleEvent -> string
    val readDocument: string -> Result<LifecycleEvent list, ReadFault>
