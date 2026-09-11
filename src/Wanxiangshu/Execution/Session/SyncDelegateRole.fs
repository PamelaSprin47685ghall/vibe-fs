namespace Wanxiangshu.Execution.Session

[<RequireQualifiedAccess>]
type SyncDelegateRole =
    | Inspector
    | Coder

module SyncDelegateRole =
    let toAttachmentKind (role: SyncDelegateRole) : AttachmentKind =
        match role with
        | SyncDelegateRole.Inspector -> AttachmentKind.SyncInspector
        | SyncDelegateRole.Coder -> AttachmentKind.SyncCoder
