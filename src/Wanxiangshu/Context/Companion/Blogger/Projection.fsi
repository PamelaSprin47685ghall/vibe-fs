namespace Wanxiangshu.Context.Companion.Blogger

open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type BlogFrameKind =
    | Entry
    | Squash

type BlogFrame =
    { Kind: BlogFrameKind
      Digest: BlobDigest
      TextRef: BlobRef
      CoveredFromSequence: int64
      CoveredThroughSequence: int64 }

type BlogCoverage =
    { IngestedThroughSequence: int64
      CoverableTurnCutoffExclusive: int
      CoveredPrefixDigest: string
      CoverableFrameCount: int }

type BlogProjectionState =
    { FrameEpochId: FrameEpochId
      Frames: BlogFrame list
      Coverage: BlogCoverage }

[<RequireQualifiedAccess>]
type BlogFoldRejection =
    | StaleFrameEpoch of expected: FrameEpochId * actual: FrameEpochId
    | NonSequentialFrameEpoch
    | IngestCursorNotAdvanced
    | IngestCursorMismatch
    | CoverageRetreated
    | CoveredFrameCountOutOfRange of claimed: int * available: int

module BlogProjection =
    val empty: BlogProjectionState
    val frameCount: state: BlogProjectionState -> int
    val frames: state: BlogProjectionState -> BlogFrame list
    val coverableFrames: state: BlogProjectionState -> BlogFrame list
    val squashWidth: state: BlogProjectionState -> int

    val applyEntry:
        frameEpoch: FrameEpochId ->
        previousIngestSequence: int64 ->
        nextIngestSequence: int64 ->
        previousCutoff: int ->
        nextCutoff: int ->
        nextDigest: string ->
        frame: BlogFrame ->
        state: BlogProjectionState ->
            Result<BlogProjectionState, BlogFoldRejection>

    val applySquash:
        previousEpoch: FrameEpochId ->
        nextEpoch: FrameEpochId ->
        count: int ->
        frame: BlogFrame ->
        state: BlogProjectionState ->
            Result<BlogProjectionState, BlogFoldRejection>

    val applyReanchor: state: BlogProjectionState -> BlogProjectionState
    val hasCoverage: state: BlogProjectionState -> bool
