namespace Wanxiangshu.Execution.Fission

open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity

module FissionFact =
    val inline FissionAdmitted:
        payload:
            {| GroupId: string
               OwnerSessionId: SessionId
               ParentSessionId: SessionId option
               OriginToolCallId: ToolCallId
               LaneCount: int
               LaneSessions: SessionId list
               LanePrompts: string list
               OwnerWorkRecordRef: BlobRef
               OwnerWorkRecordDigest: BlobDigest
               PreFissionCompletionIds: string list |} ->
            AgentFact

    val inline FissionLaneMaterialized:
        payload:
            {| GroupId: string
               OwnerSessionId: SessionId
               LaneIndex: int
               LaneSessionId: SessionId
               ProviderRun: ProviderRunIdentity
               WorkRecordRef: BlobRef
               WorkRecordDigest: BlobDigest |} ->
            AgentFact

    val inline FissionCompletionCaptured:
        payload:
            {| GroupId: string
               OwnerSessionId: SessionId
               CompletionId: string
               PayloadRef: BlobRef
               PayloadDigest: BlobDigest |} ->
            AgentFact

    val inline FissionCompletionDelivered:
        payload:
            {| GroupId: string
               OwnerSessionId: SessionId
               CompletionId: string
               LaneIndex: int |} ->
            AgentFact

    val inline FissionExternalAffinityBound:
        payload:
            {| GroupId: string
               OwnerSessionId: SessionId
               ExternalId: string
               LaneIndex: int |} ->
            AgentFact

    val inline FissionTakeoverClaimed:
        payload:
            {| GroupId: string
               OwnerSessionId: SessionId
               LaneIndex: int
               LaneSessionId: SessionId
               PromptKey: PromptKey
               AggregateWorkRecordRef: BlobRef
               AggregateWorkRecordDigest: BlobDigest |} ->
            AgentFact

    val inline FissionTakeoverStarted:
        payload:
            {| GroupId: string
               OwnerSessionId: SessionId
               LaneIndex: int
               LaneSessionId: SessionId
               PhysicalUserMessageId: PhysicalUserMessageId
               AggregateWorkRecordRef: BlobRef
               AggregateWorkRecordDigest: BlobDigest |} ->
            AgentFact

    val inline FissionConverged:
        payload:
            {| GroupId: string
               OwnerSessionId: SessionId
               TerminalLaneSessionId: SessionId
               TerminalProviderRun: ProviderRunIdentity
               AggregateWorkRecordRef: BlobRef
               AggregateWorkRecordDigest: BlobDigest |} ->
            AgentFact

    val inline FissionFailed:
        payload:
            {| GroupId: string
               OwnerSessionId: SessionId
               Reason: string |} ->
            AgentFact
