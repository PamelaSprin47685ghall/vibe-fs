namespace Wanxiangshu.Persistence.EventStore

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

type GitObjectId

module GitObjectId =
    val create: value: string -> GitObjectId
    val value: oid: GitObjectId -> string
    val compare: a: GitObjectId -> b: GitObjectId -> int

type RootOid = RootOid of GitObjectId

module RootOid =
    val create: oid: GitObjectId -> RootOid
    val value: root: RootOid -> GitObjectId

type StoreSnapshot = { RootOid: RootOid }

type TreeEntry =
    { Mode: string
      Name: string
      Oid: GitObjectId }

type IGitRawStore =
    abstract WriteBlob: content: byte[] -> Task<GitObjectId>
    abstract WriteTree: entries: TreeEntry list -> Task<GitObjectId>
    abstract ReadObject: oid: GitObjectId -> Task<byte[] option>
    abstract ReadTree: oid: GitObjectId -> Task<TreeEntry list option>
    abstract ReadRef: refName: string -> Task<GitObjectId option>
    abstract CompareAndSwapRef: refName: string * expectedOld: GitObjectId option * newOid: GitObjectId -> Task<bool>

[<RequireQualifiedAccess>]
module GitTree =
    [<Literal>]
    val TreeMode: string = "40000"

    val normalizeMode: mode: string -> string
    val isTreeMode: mode: string -> bool
    val canonicalOrder: entries: TreeEntry list -> TreeEntry list

[<RequireQualifiedAccess>]
module StoreRef =
    val canonical: string

    val remoteTracking: remote: string -> string
    val tryRemoteFromTracking: refName: string -> string option

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
    val empty: AppendReceipt
    val cutFor: eventId: EventId -> receipt: AppendReceipt -> SemanticCut option

[<RequireQualifiedAccess>]
type AppendError =
    | StorageInvalid of StorageInvalid
    | SemanticCut of SemanticCut
    | AppendFailed of reason: string

[<RequireQualifiedAccess>]
type PublishError =
    | StorageInvalid of StorageInvalid
    | SemanticCut of SemanticCut
    | PublishFailed of reason: string
    | IncompletePayloadClosure

[<RequireQualifiedAccess>]
type ConvergeError =
    | StorageInvalid of StorageInvalid
    | Transport of reason: string
    | ConvergeCasRejected
    | ConvergeRetryExhausted
