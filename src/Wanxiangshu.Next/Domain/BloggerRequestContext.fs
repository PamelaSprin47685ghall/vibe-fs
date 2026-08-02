namespace Wanxiangshu.Next.Domain

open Wanxiangshu.Next.Kernel.Identity

/// ENFORCER-045/050/051: typed material for one Blogger provider request.
///
/// Staged and consumed as a whole. Coverage advance on cycle commit reads this
/// context — never re-derives from the latest XTrace (fail closed if missing).
type BloggerMainRequestContext =
    { Toml: string
      PreviousIngestedThroughSequence: int64
      NextIngestedThroughSequence: int64
      PreviousCoverableTurnCutoffExclusive: int
      NextCoverableTurnCutoffExclusive: int
      NextCoveredPrefixDigest: string
      FrameEpochId: FrameEpochId
      DeltaDigest: BlobDigest }

type BloggerSquashRequestContext =
    { FrameEpochId: FrameEpochId
      CoveredFrameCount: int
      FrameDigests: BlobDigest list }

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
