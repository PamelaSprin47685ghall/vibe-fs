module Wanxiangshu.Kernel.HostCapability

[<RequireQualifiedAccess>]
type HostCapability =
    | ToolCatalog
    | ToolExecuteBefore
    | ToolExecuteAfter
    | SystemTransform
    | MessagesTransform
    | CompactingTransform
    | EventHook
    | SlashCommands
    | ToolPolicy
    | ReviewStore
    | SubsessionDispatch
    | SubsessionAbort
    | SubsessionReconcile
    | FallbackContinue
    /// Host exposes a scoped abort that can confirm cancellation.
    | ReliableAbort
    /// Host nudge/prompt returns a verifiable message/run identity receipt.
    | LogicalMessageReceipt

let toString (c: HostCapability) : string =
    match c with
    | HostCapability.ToolCatalog -> "toolCatalog"
    | HostCapability.ToolExecuteBefore -> "toolExecuteBefore"
    | HostCapability.ToolExecuteAfter -> "toolExecuteAfter"
    | HostCapability.SystemTransform -> "systemTransform"
    | HostCapability.MessagesTransform -> "messagesTransform"
    | HostCapability.CompactingTransform -> "compactingTransform"
    | HostCapability.EventHook -> "eventHook"
    | HostCapability.SlashCommands -> "slashCommands"
    | HostCapability.ToolPolicy -> "toolPolicy"
    | HostCapability.ReviewStore -> "reviewStore"
    | HostCapability.SubsessionDispatch -> "subsessionDispatch"
    | HostCapability.SubsessionAbort -> "subsessionAbort"
    | HostCapability.SubsessionReconcile -> "subsessionReconcile"
    | HostCapability.FallbackContinue -> "fallbackContinue"
    | HostCapability.ReliableAbort -> "reliableAbort"
    | HostCapability.LogicalMessageReceipt -> "logicalMessageReceipt"

let allFull: Set<HostCapability> =
    Set
        [ HostCapability.ToolCatalog
          HostCapability.ToolExecuteBefore
          HostCapability.ToolExecuteAfter
          HostCapability.SystemTransform
          HostCapability.MessagesTransform
          HostCapability.CompactingTransform
          HostCapability.EventHook
          HostCapability.SlashCommands
          HostCapability.ToolPolicy
          HostCapability.ReviewStore
          HostCapability.SubsessionDispatch
          HostCapability.SubsessionAbort
          HostCapability.SubsessionReconcile
          HostCapability.FallbackContinue
          HostCapability.ReliableAbort
          HostCapability.LogicalMessageReceipt ]

/// Mux host adapter is capability-degraded: no subsession control plane,
/// no reliable abort, and no guaranteed message-id receipt on nudge resolve.
let muxDefault: Set<HostCapability> =
    Set
        [ HostCapability.ToolCatalog
          HostCapability.ToolExecuteBefore
          HostCapability.ToolExecuteAfter
          HostCapability.SystemTransform
          HostCapability.MessagesTransform
          HostCapability.CompactingTransform
          HostCapability.EventHook
          HostCapability.SlashCommands
          HostCapability.ToolPolicy
          HostCapability.ReviewStore
          HostCapability.FallbackContinue ]

let toStringArray (caps: Set<HostCapability>) : string array =
    caps |> Set.toArray |> Array.map toString
