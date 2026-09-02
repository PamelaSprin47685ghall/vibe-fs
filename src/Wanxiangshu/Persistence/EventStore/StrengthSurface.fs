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

    let storeOf (value: obj) : IEventStore = (unbox<EventStoreHandle> value).Store

    let writePayload (value: obj) (bytes: byte array) : Task<Result<PayloadRef, string>> =
        (storeOf value).WritePayload bytes

    let readPayload (value: obj) (payloadRef: PayloadRef) : Task<Result<byte[] option, string>> =
        (storeOf value).ReadPayload payloadRef

    let current (value: obj) (key: string) : obj option = (storeOf value).TryCurrent key

    let durability (value: obj) : StrengthDurabilityPort =
        StrengthDurability.create (storeOf value)
