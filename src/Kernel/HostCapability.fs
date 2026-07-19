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
    | BacklogSession
    | SubsessionDispatch
    | SubsessionAbort
    | SubsessionReconcile
    | FallbackContinue

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
    | HostCapability.BacklogSession -> "backlogSession"
    | HostCapability.SubsessionDispatch -> "subsessionDispatch"
    | HostCapability.SubsessionAbort -> "subsessionAbort"
    | HostCapability.SubsessionReconcile -> "subsessionReconcile"
    | HostCapability.FallbackContinue -> "fallbackContinue"

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
          HostCapability.BacklogSession
          HostCapability.SubsessionDispatch
          HostCapability.SubsessionAbort
          HostCapability.SubsessionReconcile
          HostCapability.FallbackContinue ]

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
          HostCapability.BacklogSession
          HostCapability.FallbackContinue ]

let toStringArray (caps: Set<HostCapability>) : string array =
    caps |> Set.toArray |> Array.map toString
