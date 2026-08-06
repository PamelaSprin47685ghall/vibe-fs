namespace Wanxiangshu.Domain

open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// ENFORCER-045/050/051: typed material for one Blogger provider request.
///
/// Staged and consumed as a whole. Coverage advance on cycle commit reads this
/// context — never re-derives from the latest XTrace (fail closed if missing).
///
/// C5: RequestId + ObservedPrefixEpochId are frozen at materialization. Commit
/// must use the frozen epoch, not the live PrefixEpoch at tool-return time.
type BloggerMainRequestContext =
    { RequestId: BloggerRequestId
      MainSessionId: SessionId
      BloggerSessionId: SessionId
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
module BloggerRequestContext =

    let toml (ctx: BloggerRequestContext) =
        match ctx with
        | BloggerRequestContext.Main main -> Some main.Toml
        | BloggerRequestContext.Squash _ -> None

    let isMain (ctx: BloggerRequestContext) =
        match ctx with
        | BloggerRequestContext.Main _ -> true
        | BloggerRequestContext.Squash _ -> false

    let requestId (ctx: BloggerRequestContext) =
        match ctx with
        | BloggerRequestContext.Main main -> main.RequestId
        | BloggerRequestContext.Squash squash -> squash.RequestId

    let observedPrefixEpoch (ctx: BloggerRequestContext) =
        match ctx with
        | BloggerRequestContext.Main main -> main.ObservedPrefixEpochId
        | BloggerRequestContext.Squash squash -> squash.ObservedPrefixEpochId

    let mainSessionId (ctx: BloggerRequestContext) =
        match ctx with
        | BloggerRequestContext.Main main -> main.MainSessionId
        | BloggerRequestContext.Squash squash -> squash.MainSessionId

    let bloggerSessionId (ctx: BloggerRequestContext) =
        match ctx with
        | BloggerRequestContext.Main main -> main.BloggerSessionId
        | BloggerRequestContext.Squash squash -> squash.BloggerSessionId

    let frameEpochId (ctx: BloggerRequestContext) =
        match ctx with
        | BloggerRequestContext.Main main -> main.FrameEpochId
        | BloggerRequestContext.Squash squash -> squash.FrameEpochId
