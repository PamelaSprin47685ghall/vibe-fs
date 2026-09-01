namespace Wanxiangshu.Mission.Manager.Life

open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation.Identity

type ReviewMemberRef =
    { ReviewerSessionId: SessionId
      ReviewerOrdinal: int
      BarrierId: ReviewBarrierId
      IsNewReviewer: bool }

type ReviewerStanding =
    { ReviewerOrdinal: int
      Barriers: ReviewBarrierId list
      AgentId: string }

type RejectionEvidence =
    { RejectingReviewer: SessionId
      WorkRecordRef: BlobRef
      WorkRecordDigest: BlobDigest }

type BlessingEvidence =
    { RequestId: FinalityRequestId
      WorkRecordBundleRef: BlobRef
      WorkRecordBundleDigest: BlobDigest }

[<RequireQualifiedAccess>]
type FinalityResolution =
    | Open
    | Rejected of RejectionEvidence
    | Blessed of BlessingEvidence
    | Undecided

type SiblingSteerEvidence =
    { ReviewerSessionId: SessionId
      BarrierId: ReviewBarrierId
      WorkRecordRef: BlobRef
      WorkRecordDigest: BlobDigest }

type FinalityRequestProjection =
    { RequestId: FinalityRequestId
      GitTreeHash: GitTreeHash
      LastWordsRef: BlobRef
      LastWordsDigest: BlobDigest
      ProviderRun: ProviderRunIdentity
      ToolCallId: ToolCallId
      Members: Map<SessionId, ReviewMemberRef>
      SiblingSteers: Map<SessionId, SiblingSteerEvidence>
      Resolution: FinalityResolution }

type LifeProjection =
    { LifeId: ManagerLifeId
      OpeningUserMessageId: PhysicalUserMessageId
      OpeningTextRef: BlobRef
      OpeningTextDigest: BlobDigest
      OpeningCursor: XTraceCursor
      ProtectedPrefixEnd: XTraceCursor option
      ActiveFinality: FinalityRequestProjection option
      EnlistedReviewers: Map<SessionId, ReviewerStanding>
      LastRejectedWorkRecord: BlobRef option
      LastBlessing: BlessingEvidence option
      CompletedTerminal: BlobRef option
      Completed: bool }

type ManagerLifeProjection =
    { CurrentLife: LifeProjection option
      CompletedLives: LifeProjection list }

[<RequireQualifiedAccess>]
type ManagerLifeFoldRejection =
    | LifeUnknown
    | LifeAlreadyOpen
    | FinalityAlreadyActive
    | UnknownRequest

module ManagerLifecycleProjection =
    val empty: ManagerLifeProjection
    val fold:
        state: ManagerLifeProjection ->
        fact: ManagerLifecycleFact ->
        Result<ManagerLifeProjection, ManagerLifeFoldRejection>
    val isOpen: request: FinalityRequestProjection -> bool
    val isLifeArchived: projection: ManagerLifeProjection -> bool
