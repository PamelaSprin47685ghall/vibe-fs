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

/// Durable execution facts owned by the delegation boundary.
type ExecutionFactCases =
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
    | DelegationHandoffAdvanced of
        {| ParentSessionId: SessionId
           DelegateSessionId: SessionId
           ParentEndExclusive: int64 |}
