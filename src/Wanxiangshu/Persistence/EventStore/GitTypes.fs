namespace Wanxiangshu.Persistence.EventStore

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

[<RequireQualifiedAccess>]
module StoreRef =
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
                remoteTracking remote |> ignore
                Some remote
            with _ ->
                None
        else
            None

[<RequireQualifiedAccess>]
type PublishError =
    | StorageInvalid of StorageInvalid
    | SemanticCut of SemanticCut
    | PublishFailed of reason: string
    | IncompletePayloadClosure
    | AppendSettlementFailed of EventId * AppendError
