namespace Wanxiangshu.Persistence.EventStore

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Process

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

type GitRawOutput =
    { Stdout: byte[]
      Stderr: byte[]
      Complete: bool }

type GitRawObservation =
    { End: EndEvidence
      StopRequested: StopRequest option
      Output: GitRawOutput
      Fault: string option }

[<RequireQualifiedAccess>]
type GitRawResult =
    | NotStarted of NoExecution
    | Observed of GitRawObservation

type GitStorageFailure =
    { Operation: string
      Reason: string
      Result: GitRawResult }

module GitStorageFailure =
    let private stderrDiagnostic bytes =
        let prefix = bytes |> Array.truncate 4096 |> System.Text.Encoding.UTF8.GetString
        let marker = if bytes.Length > 4096 then " (truncated)" else ""

        if prefix = "" then
            ""
        else
            sprintf "\nstderr%s: %s" marker prefix

    let describe failure =
        let facts =
            match failure.Result with
            | GitRawResult.NotStarted reason -> sprintf "not started: %A" reason
            | GitRawResult.Observed observation ->
                sprintf
                    "end=%A; stop=%A; output_complete=%b; fault=%A"
                    observation.End
                    observation.StopRequested
                    observation.Output.Complete
                    observation.Fault

        let diagnostic =
            match failure.Result with
            | GitRawResult.Observed observation -> stderrDiagnostic observation.Output.Stderr
            | _ -> ""

        sprintf "git %s: %s (%s)%s" failure.Operation failure.Reason facts diagnostic

/// Minimal Git object/ref capability used only by WriterStreamSync/GitGateway.
type IGitRawStore =
    abstract WriteBlob: content: byte[] -> Task<Result<GitObjectId, GitStorageFailure>>
    abstract WriteTree: entries: TreeEntry list -> Task<Result<GitObjectId, GitStorageFailure>>
    abstract ReadObject: oid: GitObjectId -> Task<Result<byte[], GitStorageFailure>>
    abstract ReadTree: oid: GitObjectId -> Task<Result<TreeEntry list, GitStorageFailure>>
    abstract ReadRef: refName: string -> Task<Result<GitObjectId option, GitStorageFailure>>

[<RequireQualifiedAccess>]
module GitTree =
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

            Array.compareWith
                compare
                (System.Text.Encoding.UTF8.GetBytes(key a))
                (System.Text.Encoding.UTF8.GetBytes(key b)))

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

[<RequireQualifiedAccess>]
type ConvergeError =
    | StorageInvalid of StorageInvalid
    | GitStorage of GitStorageFailure
    | Transport of reason: string
    | ConvergeCasRejected
    | ConvergeRetryExhausted
    | ConvergeBudgetExhausted
