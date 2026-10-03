namespace Wanxiangshu.Execution.Delegation

open System
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// Who owns a forked child handle.
[<RequireQualifiedAccess>]
type HandleOwnership =
    | DurableParentHandle
    | HostOwnedHidden

[<RequireQualifiedAccess>]
type HandleCompletionKind =
    | Terminal
    | SendFailure
    | Cancelled

[<RequireQualifiedAccess>]
type HandleAbandonReason =
    | ParentCancelled
    | DeadlineExceeded
    | HostSessionGone

[<RequireQualifiedAccess>]
type FalseCompletionReason = | LegacyAbortWasObservation

type HandleWorkId =
    { Handle: HandleId
      ChildSessionId: SessionId
      AuthorityRoot: AuthorityRootUserMessageId }

/// Durable execution facts owned by the delegation boundary.
type ExecutionFactCases =
    | HandleWorkCompleted of
        {| ParentSessionId: SessionId
           Work: HandleWorkId
           Kind: HandleCompletionKind
           CompletionRef: BlobRef option
           CompletionDigest: BlobDigest option |}
    | HandleWorkConsumed of
        {| ParentSessionId: SessionId
           Work: HandleWorkId
           ConsumptionId: string
           Kind: HandleCompletionKind
           CompletionRef: BlobRef option
           CompletionDigest: BlobDigest option |}
    | HandleWorkAbandoned of
        {| ParentSessionId: SessionId
           Work: HandleWorkId
           Reason: HandleAbandonReason |}
    | ChildWorkVoided of
        {| ParentSessionId: SessionId
           Work: HandleWorkId |}
    | HandleLinked of
        {| ParentSessionId: SessionId
           ChildSessionId: SessionId
           Handle: HandleId
           TargetAgent: string
           Byname: string
           CanonicalRole: Role
           Ownership: HandleOwnership |}
    | HandleCompleted of
        {| ParentSessionId: SessionId
           Handle: HandleId
           Kind: HandleCompletionKind
           CompletionRef: BlobRef option
           CompletionDigest: BlobDigest option |}
    | HandleRetired of
        {| ParentSessionId: SessionId
           Handle: HandleId |}
    /// crash-reconciliation-020: a child work run interrupted by a restart is
    /// void — it produced nothing, so it owes nothing. The child's logical run is
    /// closed (a later reuse roots freshly) while the handle itself stays exactly
    /// as it was: no unreported delivery appears in horizon or join.
    | ChildRunVoided of
        {| ParentSessionId: SessionId
           ChildSessionId: SessionId |}
    | HandleAbandoned of
        {| ParentSessionId: SessionId
           Handle: HandleId
           Reason: HandleAbandonReason
           AbandonedAt: DateTimeOffset |}
    | HandleFalseCompletionRejected of
        {| ParentSessionId: SessionId
           Handle: HandleId
           ExpectedCompletionRef: BlobRef
           ExpectedCompletionDigest: BlobDigest
           Reason: FalseCompletionReason |}
    | HandleFalseTerminalReported of
        {| ParentSessionId: SessionId
           Handle: HandleId
           BadCompletionRef: BlobRef
           BadCompletionDigest: BlobDigest
           Reason: FalseCompletionReason |}
    | ParentJoinCorrectionRequested of
        {| ParentSessionId: SessionId
           OriginalHandle: HandleId
           ReplacementHandle: HandleId
           BadCompletionDigest: BlobDigest |}
    | HostTurnObserved of
        {| SessionId: SessionId
           ProviderRun: ProviderRunIdentity option
           ObservedAt: DateTimeOffset |}

/// Durable delegated-tool facts owned by the delegation boundary.
type DelegationFactCases =
    | DelegatedToolEstimateReplaced of
        {| SessionId: SessionId
           ExpectedToolCalls: int |}
    | DelegatedToolCallObserved of
        {| SessionId: SessionId
           ToolCallId: ToolCallId |}
    | DelegationHandoffCompleted of
        {| ParentSessionId: SessionId
           Route: DelegationHandoffRoute
           ParentEndExclusive: int64 |}
