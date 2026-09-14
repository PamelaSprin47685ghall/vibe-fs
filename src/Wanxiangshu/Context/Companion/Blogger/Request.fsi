namespace Wanxiangshu.Context.Companion.Blogger

open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type BloggerRequestRejection =
    | CoverageDidNotAdvance of previous: int64 * next: int64
    | DeltaDigestMismatch of expected: string * actual: string
    | EpochNotCoherent of field: string * value: int64
    | EmptySquashCoverage
    | SquashCoverageMismatch of declared: int * carried: int

type BloggerMainRequestInput =
    { RequestId: BloggerRequestId
      MainSessionId: SessionId
      BloggerSessionId: SessionId
      Items: BloggerDeltaItem list
      Toml: string
      PreviousIngestedThroughSequence: int64
      NextIngestedThroughSequence: int64
      PreviousCoverableTurnCutoffExclusive: int
      NextCoverableTurnCutoffExclusive: int
      NextCoveredPrefixDigest: string
      FrameEpochId: FrameEpochId
      DeltaDigest: BlobDigest
      ObservedPrefixEpochId: PrefixEpochId }

type BloggerSquashRequestInput =
    { RequestId: BloggerRequestId
      MainSessionId: SessionId
      BloggerSessionId: SessionId
      FrameEpochId: FrameEpochId
      CoveredFrameCount: int
      FrameDigests: BlobDigest list
      ObservedPrefixEpochId: PrefixEpochId }

type BloggerMainRequestContext =
    private
        { StoredRequestId: BloggerRequestId
          StoredMainSessionId: SessionId
          StoredBloggerSessionId: SessionId
          StoredItems: BloggerDeltaItem list
          StoredToml: string
          StoredPreviousIngestedThroughSequence: int64
          StoredNextIngestedThroughSequence: int64
          StoredPreviousCoverableTurnCutoffExclusive: int
          StoredNextCoverableTurnCutoffExclusive: int
          StoredNextCoveredPrefixDigest: string
          StoredFrameEpochId: FrameEpochId
          StoredDeltaDigest: BlobDigest
          StoredObservedPrefixEpochId: PrefixEpochId }

    member RequestId: BloggerRequestId
    member MainSessionId: SessionId
    member BloggerSessionId: SessionId
    member Items: BloggerDeltaItem list
    member Toml: string
    member PreviousIngestedThroughSequence: int64
    member NextIngestedThroughSequence: int64
    member PreviousCoverableTurnCutoffExclusive: int
    member NextCoverableTurnCutoffExclusive: int
    member NextCoveredPrefixDigest: string
    member FrameEpochId: FrameEpochId
    member DeltaDigest: BlobDigest
    member ObservedPrefixEpochId: PrefixEpochId

type BloggerSquashRequestContext =
    private
        { StoredRequestId: BloggerRequestId
          StoredMainSessionId: SessionId
          StoredBloggerSessionId: SessionId
          StoredFrameEpochId: FrameEpochId
          StoredCoveredFrameCount: int
          StoredFrameDigests: BlobDigest list
          StoredObservedPrefixEpochId: PrefixEpochId }

    member RequestId: BloggerRequestId
    member MainSessionId: SessionId
    member BloggerSessionId: SessionId
    member FrameEpochId: FrameEpochId
    member CoveredFrameCount: int
    member FrameDigests: BlobDigest list
    member ObservedPrefixEpochId: PrefixEpochId

[<RequireQualifiedAccess>]
type BloggerRequestContext =
    | Main of BloggerMainRequestContext
    | Squash of BloggerSquashRequestContext

[<RequireQualifiedAccess>]
type BloggerTerminalRequestOwnership =
    | Current
    | Superseded
    | Unproven

type BloggerTerminalParentEvidence =
    { PromptKey: PromptKey
      IsRequestScopedRepair: bool }

[<RequireQualifiedAccess>]
module BloggerRequestOwnership =
    val decide:
        currentRequestId: BloggerRequestId ->
        durableOpenRequestId: BloggerRequestId option ->
        durableOpenPromptKey: PromptKey option ->
        parent: BloggerTerminalParentEvidence option ->
            BloggerTerminalRequestOwnership

[<RequireQualifiedAccess>]
module BloggerRequestContext =
    val mainRequestId:
        mainSessionId: SessionId ->
        bloggerSessionId: SessionId ->
        deltaDigest: BlobDigest ->
        previousSeq: int64 ->
        nextSeq: int64 ->
            BloggerRequestId

    val squashRequestId:
        mainSessionId: SessionId ->
        bloggerSessionId: SessionId ->
        frameEpoch: FrameEpochId ->
        coveredFrameCount: int ->
        frameDigests: BlobDigest list ->
            BloggerRequestId

    val toml: ctx: BloggerRequestContext -> string option
    val isMain: ctx: BloggerRequestContext -> bool
    val requestId: ctx: BloggerRequestContext -> BloggerRequestId
    val observedPrefixEpoch: ctx: BloggerRequestContext -> PrefixEpochId
    val mainSessionId: ctx: BloggerRequestContext -> SessionId
    val bloggerSessionId: ctx: BloggerRequestContext -> SessionId
    val frameEpochId: ctx: BloggerRequestContext -> FrameEpochId

[<RequireQualifiedAccess>]
module BloggerRequestMaterial =
    val createMain: input: BloggerMainRequestInput -> Result<BloggerMainRequestContext, BloggerRequestRejection>

    val createSquash:
        input: BloggerSquashRequestInput -> Result<BloggerSquashRequestContext, BloggerRequestRejection>
