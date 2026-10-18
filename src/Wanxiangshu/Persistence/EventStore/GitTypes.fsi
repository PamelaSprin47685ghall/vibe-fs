namespace Wanxiangshu.Persistence.EventStore

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Process

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
    val describe: failure: GitStorageFailure -> string

type IGitRawStore =
    abstract WriteBlob: content: byte[] -> Task<Result<GitObjectId, GitStorageFailure>>
    abstract WriteTree: entries: TreeEntry list -> Task<Result<GitObjectId, GitStorageFailure>>
    abstract ReadObject: oid: GitObjectId -> Task<Result<byte[], GitStorageFailure>>
    abstract ReadTree: oid: GitObjectId -> Task<Result<TreeEntry list, GitStorageFailure>>
    abstract ReadRef: refName: string -> Task<Result<GitObjectId option, GitStorageFailure>>

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
