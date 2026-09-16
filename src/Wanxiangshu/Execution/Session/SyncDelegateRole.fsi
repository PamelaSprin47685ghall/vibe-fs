namespace Wanxiangshu.Execution.Session

[<RequireQualifiedAccess>]
type SyncDelegateRole =
    | Inspector
    | Coder
    | Engineer

module SyncDelegateRole =
    val toAttachmentKind: role: SyncDelegateRole -> AttachmentKind
