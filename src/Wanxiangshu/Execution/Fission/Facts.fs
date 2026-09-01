namespace Wanxiangshu.Execution.Fission

open Wanxiangshu.Foundation.Identity

/// Durable facts for one logical participant's temporary physical lanes.
[<RequireQualifiedAccess>]
type FissionFactCases =
    | FissionAdmitted of
        {| GroupId: string
           OwnerSessionId: SessionId
           ParentSessionId: SessionId option
           OriginToolCallId: ToolCallId
           LaneCount: int
           LaneSessions: SessionId list
           LanePrompts: string list
           OwnerWorkRecordRef: BlobRef
           OwnerWorkRecordDigest: BlobDigest
           PreFissionCompletionIds: string list |}
    | FissionLaneMaterialized of
        {| GroupId: string
           OwnerSessionId: SessionId
           LaneIndex: int
           LaneSessionId: SessionId
           ProviderRun: ProviderRunIdentity
           WorkRecordRef: BlobRef
           WorkRecordDigest: BlobDigest |}
    | FissionCompletionCaptured of
        {| GroupId: string
           OwnerSessionId: SessionId
           CompletionId: string
           PayloadRef: BlobRef
           PayloadDigest: BlobDigest |}
    | FissionCompletionDelivered of
        {| GroupId: string
           OwnerSessionId: SessionId
           CompletionId: string
           LaneIndex: int |}
    | FissionExternalAffinityBound of
        {| GroupId: string
           OwnerSessionId: SessionId
           ExternalId: string
           LaneIndex: int |}
    | FissionTakeoverClaimed of
        {| GroupId: string
           OwnerSessionId: SessionId
           LaneIndex: int
           LaneSessionId: SessionId
           PromptKey: PromptKey
           AggregateWorkRecordRef: BlobRef
           AggregateWorkRecordDigest: BlobDigest |}
    /// Legacy accepted-physical takeover fact. New writes use
    /// FissionTakeoverClaimed so lane-terminal observation never waits for a
    /// future chat.message just to learn its PhysicalUserMessageId.
    | FissionTakeoverStarted of
        {| GroupId: string
           OwnerSessionId: SessionId
           LaneIndex: int
           LaneSessionId: SessionId
           PhysicalUserMessageId: PhysicalUserMessageId
           AggregateWorkRecordRef: BlobRef
           AggregateWorkRecordDigest: BlobDigest |}
    | FissionConverged of
        {| GroupId: string
           OwnerSessionId: SessionId
           TerminalLaneSessionId: SessionId
           TerminalProviderRun: ProviderRunIdentity
           AggregateWorkRecordRef: BlobRef
           AggregateWorkRecordDigest: BlobDigest |}
    | FissionFailed of
        {| GroupId: string
           OwnerSessionId: SessionId
           Reason: string |}
