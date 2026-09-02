namespace Wanxiangshu.Persistence.EventStore

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Strength.Persistence

/// Narrow bridge for Strength's persistence observations over the opaque
/// EventStore owner capability. It exposes only the payload, Current, and
/// durability seams needed by the Strength owner surface. The underlying
/// IEventStore remains inside the production boundary; callers pass the
/// EventStoreSurface handle unchanged.
[<RequireQualifiedAccess>]
module EventStoreStrengthSurface =
    val storeOf: value: obj -> IEventStore
    val writePayload: value: obj -> bytes: byte array -> Task<Result<PayloadRef, string>>
    val readPayload: value: obj -> payloadRef: PayloadRef -> Task<Result<byte[] option, string>>
    val current: value: obj -> key: string -> obj option
    val durability: value: obj -> StrengthDurabilityPort
