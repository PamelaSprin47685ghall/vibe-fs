namespace Wanxiangshu.Context.Companion.Blogger

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation

[<RequireQualifiedAccess>]
type BlogFrameKind = | Entry

type BlogFrame =
    {
        Kind: BlogFrameKind
        Digest: BlobDigest
        TextRef: BlobRef
        CoveredFromSequence: int64
        CoveredThroughSequence: int64
        /// context-compression-029: the complete-turn boundary this frame's material
        /// summarises.
        CutoffExclusive: int
    }

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

module BlogProjection =
    val empty: BlogProjectionState
    val frameCount: state: BlogProjectionState -> int
    val frames: state: BlogProjectionState -> BlogFrame list
    val coverableFrames: state: BlogProjectionState -> BlogFrame list
    val framesThroughCutoff: cutoffExclusive: int -> state: BlogProjectionState -> BlogFrame list

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

    val applyReanchor: state: BlogProjectionState -> BlogProjectionState
    val hasCoverage: state: BlogProjectionState -> bool
