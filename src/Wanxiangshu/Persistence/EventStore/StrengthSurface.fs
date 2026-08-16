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

    let internal storeOf (value: obj) : IEventStore =
        (unbox<EventStoreHandle> value).Store

    let internal writePayload (value: obj) (bytes: byte array) : Task<Result<PayloadRef, string>> =
        (storeOf value).WritePayload bytes

    let internal readPayload (value: obj) (payloadRef: PayloadRef) : Task<Result<byte[] option, string>> =
        (storeOf value).ReadPayload payloadRef

    let internal current (value: obj) (key: string) : obj option =
        (storeOf value).TryCurrent key

    let internal durability (value: obj) : StrengthDurabilityPort =
        StrengthDurability.create (storeOf value)
