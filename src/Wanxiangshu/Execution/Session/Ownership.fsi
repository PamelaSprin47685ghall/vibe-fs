namespace Wanxiangshu.Execution.Session

open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type SessionExecutionClass =
    | Work
    | InternalLeaf

[<RequireQualifiedAccess>]
type AttachmentKind =
    | Companion
    | SyncInspector
    | SyncCoder
    | Bookkeeper of transactionId: string
    | StrengthReplica

[<RequireQualifiedAccess>]
type SessionOwnership =
    | Root
    | Attached of ownerSessionId: SessionId * attachment: AttachmentKind

module SessionExecutionClass =
    val isWork: SessionExecutionClass -> bool
    val isInternalLeaf: SessionExecutionClass -> bool

module SessionOwnership =
    val tryOwner: SessionOwnership -> SessionId option
