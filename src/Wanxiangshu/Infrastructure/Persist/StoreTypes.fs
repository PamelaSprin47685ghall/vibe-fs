namespace Wanxiangshu.Infrastructure.Persist

open Wanxiangshu.Domain
open Wanxiangshu.Kernel.Identity

/// Persist-owned Git object identity. Domain must not reference this (§45).
type GitObjectId = private GitObjectId of string

module GitObjectId =
    let create (value: string) = GitObjectId value
    let value (GitObjectId v) = v

    let compare (GitObjectId a) (GitObjectId b) = compare a b

/// Root tree OID pointed at by `refs/wanxiang/store` (§6 / §9).
type RootOid = RootOid of GitObjectId

module RootOid =
    let create (oid: GitObjectId) = RootOid oid
    let value (RootOid oid) = oid

/// Frozen store snapshot. RootOid is authoritative — no full EventId set (§10 / 附录 B).
type StoreSnapshot = { RootOid: RootOid }

/// Candidate publication: base snapshot + new events + Persist-side payload blobs.
type AppendCandidate =
    {
        BaseSnapshot: StoreSnapshot
        NewEvents: EventEnvelope list
        NewPayloads: (GitObjectId * byte[]) list
    }

/// Inputs to K-way merge (set of snapshots to union).
type MergeInput = MergeInput of StoreSnapshot list

module MergeInput =
    let ofList (snapshots: StoreSnapshot list) = MergeInput snapshots
    let toList (MergeInput snapshots) = snapshots

/// Git tree entry for IGitRawStore.WriteTree / ReadTree.
type TreeEntry =
    {
        Mode: string
        Name: string
        Oid: GitObjectId
    }

/// Canonical store ref — Persist/Git ownership only (unified-store-gate).
[<RequireQualifiedAccess>]
module StoreRef =
    let canonical = "refs/wanxiang/store"

    /// Remote-tracking store ref for ordinary fetch/pull (§14):
    /// `refs/wanxiang/remotes/<remote>/store`.
    let remoteTracking (remote: string) : string =
        if System.String.IsNullOrWhiteSpace remote then
            invalidArg "remote" "remote name is required"

        if remote.IndexOfAny([| '/'; '\\' |]) >= 0 || remote = "." || remote = ".." then
            invalidArg "remote" "remote name must be a single path segment"

        sprintf "refs/wanxiang/remotes/%s/store" remote

/// StorageInvalid — global fail-closed (§5.3). Not recoverable by projection.
[<RequireQualifiedAccess>]
type StorageInvalid =
    | IdentityCollision of EventId
    | NonCanonical of reason: string
    | MalformedEnvelope of reason: string
    | MissingParent of EventId
    | CyclicParents
    | MissingPayload of PayloadRef
    | UnknownEventType of eventType: string

/// DomainConflict — physically legal concurrent fork; projection conflict state (§5.3).
/// Never escalate to StorageInvalid / global fail-closed.
[<RequireQualifiedAccess>]
type DomainConflict = | ConcurrentHeads of streamId: EventStreamId * heads: EventId list

[<RequireQualifiedAccess>]
type MergeError = | StorageInvalid of StorageInvalid

[<RequireQualifiedAccess>]
type AppendError =
    | StorageInvalid of StorageInvalid
    | AppendCasRejected
    | AppendRetryExhausted

[<RequireQualifiedAccess>]
type PublishError =
    | StorageInvalid of StorageInvalid
    | PublishCasRejected
    | PublishRetryExhausted
    | IncompletePayloadClosure

[<RequireQualifiedAccess>]
type FoldError = | StorageInvalid of StorageInvalid

[<RequireQualifiedAccess>]
type ConvergeError =
    | StorageInvalid of StorageInvalid
    | Transport of reason: string
    | ConvergeCasRejected
    | ConvergeRetryExhausted
