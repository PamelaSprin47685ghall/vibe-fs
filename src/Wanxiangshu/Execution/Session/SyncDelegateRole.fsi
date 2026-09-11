namespace Wanxiangshu.Execution.Session

[<RequireQualifiedAccess>]
type SyncDelegateRole =
    | Inspector
    | Coder

module SyncDelegateRole =
    val toAttachmentKind: role: SyncDelegateRole -> AttachmentKind
