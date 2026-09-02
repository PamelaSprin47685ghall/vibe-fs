namespace Wanxiangshu.Persistence.EventStore

open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type StorageInvalid =
    | IdentityCollision of EventId
    | NonCanonical of reason: string
    | MalformedEnvelope of reason: string
    | MissingParent of EventId
    | CyclicParents
    | MissingPayload of PayloadRef
    | UnknownEventType of eventType: string

[<RequireQualifiedAccess>]
type DomainConflict = ConcurrentHeads of streamId: EventStreamId * heads: EventId list

type SemanticCut =
    { Rule: string
      FailedEventId: EventId
      Reason: string
      CutEventId: EventId }

type AppendReceipt = { Cuts: SemanticCut list }

[<RequireQualifiedAccess>]
module AppendReceipt =
    let empty = { Cuts = [] }

    let cutFor (eventId: EventId) (receipt: AppendReceipt) =
        receipt.Cuts |> List.tryFind (fun cut -> cut.FailedEventId = eventId)

[<RequireQualifiedAccess>]
type AppendError =
    | StorageInvalid of StorageInvalid
    | SemanticCut of SemanticCut
    | AppendFailed of reason: string
