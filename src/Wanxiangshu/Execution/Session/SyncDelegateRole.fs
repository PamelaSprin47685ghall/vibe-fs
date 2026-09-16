namespace Wanxiangshu.Execution.Session

[<RequireQualifiedAccess>]
type SyncDelegateRole =
    | Inspector
    | Coder
    | Engineer

module SyncDelegateRole =
    let toAttachmentKind (role: SyncDelegateRole) : AttachmentKind =
        match role with
        | SyncDelegateRole.Inspector -> AttachmentKind.SyncInspector
        | SyncDelegateRole.Coder -> AttachmentKind.SyncCoder
        | SyncDelegateRole.Engineer -> AttachmentKind.SyncInspector
