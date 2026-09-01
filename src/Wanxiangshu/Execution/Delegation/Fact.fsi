namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

module ExecutionFact =
    val inline HandleLinked:
        payload:
            {| Byname: string
               CanonicalRole: Role
               ChildSessionId: SessionId
               Handle: HandleId
               Ownership: HandleOwnership
               ParentSessionId: SessionId
               TargetAgent: string |} ->
            AgentFact

    val inline HandleCompleted:
        payload:
            {| CompletionDigest: BlobDigest option
               CompletionRef: BlobRef option
               Handle: HandleId
               Kind: HandleCompletionKind
               ParentSessionId: SessionId |} ->
            AgentFact

    val inline HandleRetired: payload: {| Handle: HandleId; ParentSessionId: SessionId |} -> AgentFact

    val inline HandleAbandoned:
        payload:
            {| AbandonedAt: System.DateTimeOffset
               Handle: HandleId
               ParentSessionId: SessionId
               Reason: HandleAbandonReason |} ->
            AgentFact

    val inline HandleFalseCompletionRejected:
        payload:
            {| ExpectedCompletionDigest: BlobDigest
               ExpectedCompletionRef: BlobRef
               Handle: HandleId
               ParentSessionId: SessionId
               Reason: FalseCompletionReason |} ->
            AgentFact

    val inline HandleFalseTerminalReported:
        payload:
            {| BadCompletionDigest: BlobDigest
               BadCompletionRef: BlobRef
               Handle: HandleId
               ParentSessionId: SessionId
               Reason: FalseCompletionReason |} ->
            AgentFact

    val inline ParentJoinCorrectionRequested:
        payload:
            {| BadCompletionDigest: BlobDigest
               OriginalHandle: HandleId
               ParentSessionId: SessionId
               ReplacementHandle: HandleId |} ->
            AgentFact

    val inline HostTurnObserved:
        payload: {| ObservedAt: System.DateTimeOffset; ProviderRun: ProviderRunIdentity option; SessionId: SessionId |} -> AgentFact

module DelegationFact =
    val inline DelegatedToolEstimateReplaced: payload: {| ExpectedToolCalls: int; SessionId: SessionId |} -> AgentFact
    val inline DelegatedToolCallObserved: payload: {| SessionId: SessionId; ToolCallId: ToolCallId |} -> AgentFact

    val inline DelegationHandoffCompleted:
        payload: {| ParentEndExclusive: int64; ParentSessionId: SessionId; Route: DelegationHandoffRoute |} -> AgentFact
