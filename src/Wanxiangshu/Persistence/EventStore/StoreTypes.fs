namespace Wanxiangshu.Persistence.EventStore

open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Strength.Persistence

open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Foundation.Identity

/// Git object identity exists only at the remote-sync membrane.
type GitObjectId = private GitObjectId of string

module GitObjectId =
    let create (value: string) = GitObjectId value
    let value (GitObjectId value) = value
    let compare (GitObjectId a) (GitObjectId b) = compare a b

/// Root tree of one remote-sync snapshot. It is not local EventStore authority.
type RootOid = RootOid of GitObjectId

module RootOid =
    let create oid = RootOid oid
    let value (RootOid oid) = oid

type StoreSnapshot = { RootOid: RootOid }

type TreeEntry =
    { Mode: string
      Name: string
      Oid: GitObjectId }

/// Minimal Git object/ref capability used only by WriterStreamSync/GitGateway.
type IGitRawStore =
    abstract WriteBlob: content: byte[] -> Task<GitObjectId>
    abstract WriteTree: entries: TreeEntry list -> Task<GitObjectId>
    abstract ReadObject: oid: GitObjectId -> Task<byte[] option>
    abstract ReadTree: oid: GitObjectId -> Task<TreeEntry list option>
    abstract ReadRef: refName: string -> Task<GitObjectId option>
    abstract CompareAndSwapRef: refName: string * expectedOld: GitObjectId option * newOid: GitObjectId -> Task<bool>

/// Git tree object-format helpers only. No EventStore layout lives here.
[<RequireQualifiedAccess>]
module GitTree =
    [<Literal>]
    let BlobMode = "100644"

    [<Literal>]
    let TreeMode = "40000"

    let normalizeMode mode =
        if mode = "040000" || mode = "40000" then TreeMode else mode

    let isTreeMode mode = normalizeMode mode = TreeMode

    let canonicalOrder (entries: TreeEntry list) =
        entries
        |> List.map (fun entry ->
            { entry with
                Mode = normalizeMode entry.Mode })
        |> List.sortWith (fun a b ->
            let key entry =
                if isTreeMode entry.Mode then
                    entry.Name + "/"
                else
                    entry.Name

            compare (key a) (key b))

[<RequireQualifiedAccess>]
module StoreRef =
    /// Remote publication ref. Local runtime truth is `.git/wanxiang/events/*.ndjson`.
    let canonical = "refs/wanxiang/store"

    let remoteTracking (remote: string) =
        if System.String.IsNullOrWhiteSpace remote then
            invalidArg "remote" "remote name is required"

        if remote.IndexOfAny([| '/'; '\\' |]) >= 0 || remote = "." || remote = ".." then
            invalidArg "remote" "remote name must be a single path segment"

        sprintf "refs/wanxiang/remotes/%s/store" remote

    let tryRemoteFromTracking (refName: string) =
        let marker = "__wanxiang_remote__"
        let template = remoteTracking marker
        let markerAt = template.IndexOf(marker, System.StringComparison.Ordinal)
        let prefix = template.Substring(0, markerAt)
        let suffix = template.Substring(markerAt + marker.Length)

        if
            refName.StartsWith(prefix, System.StringComparison.Ordinal)
            && refName.EndsWith(suffix, System.StringComparison.Ordinal)
            && refName.Length > prefix.Length + suffix.Length
        then
            let remote =
                refName.Substring(prefix.Length, refName.Length - prefix.Length - suffix.Length)

            try
                Some(
                    remoteTracking remote |> ignore
                    remote
                )
            with _ ->
                None
        else
            None

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
