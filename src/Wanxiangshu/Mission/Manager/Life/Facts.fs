namespace Wanxiangshu.Mission.Manager.Life

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// Durable Manager Life facts owned by the Manager lifecycle boundary.
[<RequireQualifiedAccess>]
type ManagerLifecycleFact =
    | LifeOpened of
        {| SessionId: SessionId
           LifeId: ManagerLifeId
           OpeningUserMessageId: PhysicalUserMessageId
           OpeningTextRef: BlobRef
           OpeningTextDigest: BlobDigest
           OpeningCursorSequence: int64 |}
    | WorkActivated of
        {| SessionId: SessionId
           LifeId: ManagerLifeId
           ActivationPromptKey: PromptKey
           ProtectedPrefixEndSequence: int64 |}
    | FinalityRequested of
        {| SessionId: SessionId
           LifeId: ManagerLifeId
           RequestId: FinalityRequestId
           GitTreeHash: GitTreeHash
           LastWordsRef: BlobRef
           LastWordsDigest: BlobDigest
           ProviderRun: ProviderRunIdentity
           ToolCallId: ToolCallId |}
    | FinalityReviewerEnlisted of
        {| SessionId: SessionId
           LifeId: ManagerLifeId
           RequestId: FinalityRequestId
           ReviewerSessionId: SessionId
           ReviewerOrdinal: int
           BarrierId: ReviewBarrierId
           GitTreeHash: GitTreeHash
           IsNewReviewer: bool |}
    | FinalityRejected of
        {| SessionId: SessionId
           LifeId: ManagerLifeId
           RequestId: FinalityRequestId
           RejectingReviewerSessionId: SessionId
           BarrierId: ReviewBarrierId
           GitTreeHash: GitTreeHash
           WorkRecordRef: BlobRef
           WorkRecordDigest: BlobDigest |}
    | FinalitySiblingSteered of
        {| SessionId: SessionId
           LifeId: ManagerLifeId
           RequestId: FinalityRequestId
           ReviewerSessionId: SessionId
           BarrierId: ReviewBarrierId
           GitTreeHash: GitTreeHash
           WorkRecordRef: BlobRef
           WorkRecordDigest: BlobDigest |}
    | FinalityBlessed of
        {| SessionId: SessionId
           LifeId: ManagerLifeId
           RequestId: FinalityRequestId
           GitTreeHash: GitTreeHash
           WorkRecordBundleRef: BlobRef
           WorkRecordBundleDigest: BlobDigest |}
    | FinalityUndecided of
        {| SessionId: SessionId
           LifeId: ManagerLifeId
           RequestId: FinalityRequestId
           ReviewerSessionId: SessionId
           BarrierId: ReviewBarrierId
           GitTreeHash: GitTreeHash |}
    | LifeCompleted of
        {| SessionId: SessionId
           LifeId: ManagerLifeId
           RequestId: FinalityRequestId
           TerminalRef: BlobRef
           TerminalDigest: BlobDigest |}
