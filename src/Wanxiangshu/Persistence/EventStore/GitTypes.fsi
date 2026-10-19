namespace Wanxiangshu.Persistence.EventStore

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

[<RequireQualifiedAccess>]
module StoreRef =
    val canonical: string
    val remoteTracking: remote: string -> string
    val tryRemoteFromTracking: refName: string -> string option

[<RequireQualifiedAccess>]
type PublishError =
    | StorageInvalid of StorageInvalid
    | SemanticCut of SemanticCut
    | PublishFailed of reason: string
    | IncompletePayloadClosure
    | AppendSettlementFailed of EventId * AppendError
