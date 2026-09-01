namespace Wanxiangshu.Context.Companion.Blogger

open Wanxiangshu.Foundation.Identity

type BloggerMainRequestContext =
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

type BloggerSquashRequestContext =
    { RequestId: BloggerRequestId
      MainSessionId: SessionId
      BloggerSessionId: SessionId
      FrameEpochId: FrameEpochId
      CoveredFrameCount: int
      FrameDigests: BlobDigest list
      ObservedPrefixEpochId: PrefixEpochId }

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
    val toml: ctx: BloggerRequestContext -> string option
    val isMain: ctx: BloggerRequestContext -> bool
    val requestId: ctx: BloggerRequestContext -> BloggerRequestId
    val observedPrefixEpoch: ctx: BloggerRequestContext -> PrefixEpochId
    val mainSessionId: ctx: BloggerRequestContext -> SessionId
    val bloggerSessionId: ctx: BloggerRequestContext -> SessionId
    val frameEpochId: ctx: BloggerRequestContext -> FrameEpochId
