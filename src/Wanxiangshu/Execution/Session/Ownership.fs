namespace Wanxiangshu.Execution.Session

open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Foundation

open Wanxiangshu.Foundation.Identity

/// HOST-008: orthogonal session ownership — ExecutionClass × Ownership.
///
/// Long-lived association is no longer a single `SatelliteKind` axis. Dedicated
/// SyncInspector/SyncCoder are Work+Attached (MAY hold a Companion); Companion /
/// Bookkeeper / StrengthReplica are InternalLeaf+Attached. StrengthReplica is
/// NEVER a `SatelliteKind` case.
/// HOST-008: whether the session is ordinary work or an internal leaf.
[<RequireQualifiedAccess>]
type SessionExecutionClass =
    | Work
    | InternalLeaf

/// HOST-008: why an Attached session is bound to its owner.
[<RequireQualifiedAccess>]
type AttachmentKind =
    | Companion
    | SyncInspector
    | SyncCoder
    | Bookkeeper of transactionId: string
    /// Short-lived decision-local InternalLeaf replica attachment.
    /// Not a SatelliteKind case — Universal AttachmentKind ownership only.
    | StrengthReplica

/// HOST-008: Root work vs Attached ownership under one ownerSessionId.
[<RequireQualifiedAccess>]
type SessionOwnership =
    | Root
    | Attached of ownerSessionId: SessionId * attachment: AttachmentKind

module SessionExecutionClass =

    let isWork =
        function
        | SessionExecutionClass.Work -> true
        | SessionExecutionClass.InternalLeaf -> false

    let isInternalLeaf =
        function
        | SessionExecutionClass.Work -> false
        | SessionExecutionClass.InternalLeaf -> true

module SessionOwnership =

    /// Owner of an Attached session; `None` for Root.
    let tryOwner =
        function
        | SessionOwnership.Root -> None
        | SessionOwnership.Attached(owner, _) -> Some owner

    /// Attachment kind of an Attached session; `None` for Root.
    let attachmentKind =
        function
        | SessionOwnership.Root -> None
        | SessionOwnership.Attached(_, kind) -> Some kind
