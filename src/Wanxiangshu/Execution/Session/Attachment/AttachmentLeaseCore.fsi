namespace Wanxiangshu.Execution.Session.Attachment

open System.Threading.Tasks
open Wanxiangshu.Execution.Session
open Wanxiangshu.Foundation.Identity

/// The ONE lifecycle mechanism for every Attached session
/// (managed-session-lifecycle [001]): each AttachmentKind supplies only parameters,
/// this module performs creation, recovery, registration and release for all of them.
[<RequireQualifiedAccess>]
module AttachmentLeaseCore =

    type Child =
        { SessionId: string
          Agent: string option
          Title: string option }

    type Operations =
        { ListChildren: string -> Task<Result<Child list, string>>
          CreateChild: string -> string -> string option -> Task<Result<string, string>>
          AbortChild: string -> Task<Result<unit, string>>
          Link: string -> string -> string -> Task<Result<unit, string>>
          Close: string -> Task<Result<unit, string>> }

    type Spec =
        { Kind: AttachmentKind
          Agent: string
          Title: string
          Directory: string option
          RestoreHint: string option }

    type Origin =
        | Created
        | Reused
        | Replacement

    type Lease =
        {
            SessionId: string
            Origin: Origin
            /// The agent established for this child. A reuse answers with the agent
            /// bound at create time, never with a later request's spelling.
            Agent: string
        }

    val kindKey: kind: AttachmentKind -> string
    val leaseKey: owner: SessionId -> kind: AttachmentKind -> string

    /// Establish (or re-establish) the kind's lease and publish its association.
    val ensure: operations: Operations -> owner: SessionId -> spec: Spec -> Task<Result<Lease, string>>

    /// Release the kind's lease: the child ends and the association closes.
    val retire: operations: Operations -> owner: SessionId -> Task<Result<unit, string>>

/// The single lifecycle owner's registry: the child each `(owner, AttachmentKind)`
/// holds, plus single-flight so concurrent first requests share one creation.
type AttachmentLeaseRegistry =
    new: unit -> AttachmentLeaseRegistry
    member Bind: owner: SessionId * kind: AttachmentKind * lease: AttachmentLeaseCore.Lease -> unit
    member TryFind: owner: SessionId * kind: AttachmentKind -> AttachmentLeaseCore.Lease option
    member TryFindScoped: owner: SessionId * kind: AttachmentKind -> AttachmentLeaseCore.Lease option
    member TryFindByChild: child: SessionId -> (SessionId * AttachmentKind) list
    member Snapshot: unit -> (SessionId * AttachmentKind) list
    member Remove: owner: SessionId * kind: AttachmentKind -> bool
    member RemoveByChild: child: SessionId -> (SessionId * AttachmentKind) list

    member Ensure:
        owner: SessionId * kind: AttachmentKind * start: (unit -> Task<Result<AttachmentLeaseCore.Lease, string>>) ->
            Task<Result<AttachmentLeaseCore.Lease, string>>

    member FinishEnsure: owner: SessionId * kind: AttachmentKind -> unit
    member Clear: unit -> unit
