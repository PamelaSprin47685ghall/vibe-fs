namespace Wanxiangshu.Context.Companion.Blogger

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation

/// COMPANION-005: what a frame is. There is no Seed: the parent LWR is a child's
/// input context, never a frame of the child's own work log (COMPANION-003).
[<RequireQualifiedAccess>]
type BlogFrameKind = | Entry

type BlogFrame =
    {
        Kind: BlogFrameKind
        Digest: BlobDigest
        TextRef: BlobRef
        /// Exclusive start of this frame's RecordCoverage interval
        /// (`BlogObservationCommitted.PreviousIngestedThroughSequence`).
        CoveredFromSequence: int64
        /// Inclusive end of this frame's RecordCoverage interval
        /// (`BlogObservationCommitted.NextIngestedThroughSequence`).
        CoveredThroughSequence: int64
        /// context-compression-029: the complete-turn boundary this frame's material
        /// summarises. Frames are appended in non-decreasing order of it, so the subset
        /// a cutoff may use is a prefix of the coverable frames — material past the
        /// cutoff would describe turns that are still raw after it.
        CutoffExclusive: int
    }

/// CTX-011: the two positions plus the proof that ties the second one to X.
///
/// `IngestedThroughSequence` is the RecordCoverage advance in XTraceCursor
/// coordinates (COMPANION-003): it may sit mid-turn, and it decides where the LWR
/// gap starts. `CoverableTurnCutoffExclusive` never does — a probe may only use
/// the latter (COMPANION-011), which is why they are separate fields rather than
/// one "progress" number.
///
/// `IngestedThroughSequence` comes from `Domain.BloggerDelta`: the chunker
/// produces the value this projection folds, and one type keeps the producer and
/// the validator talking about the same position.
type BlogCoverage =
    {
        /// RecordCoverage: how much of the XTrace the Companion has consumed.
        /// Durable across Host compaction/reanchor (COMPANION-008); only
        /// BlogObservationCommitted advances it.
        IngestedThroughSequence: int64
        /// PrefixCoverage: the complete-turn boundary the probe may replace up to.
        CoverableTurnCutoffExclusive: int
        CoveredPrefixDigest: string
        /// CTX-011: how many frames existed when the cutoff last advanced.
        ///
        /// Frames and the cutoff do not move together. A chunk that stops mid-turn
        /// appends a frame and leaves the cutoff alone (CTX-013 level two), so the frame
        /// list runs ahead of what the cutoff claims.
        ///
        /// A probe must use only these frames. Using all of them would build a
        /// FrozenRecordPrefix describing turns at or beyond the cutoff — which are still
        /// present as raw messages after it — so the model would see the same turn twice,
        /// once summarised and once verbatim. Not a correctness loss, but the design says
        /// "probe uses CoverableRecordPrefix, not the possibly-ahead full frame list",
        /// and this count is what makes the prefix derivable rather than a second stored
        /// copy.
        CoverableFrameCount: int
    }

/// The Companion's durable state: the frame sequence and what it covers.
///
/// `Frames` is stored newest-first so replay cons is O(1). `BlogProjection.frames`
/// restores oldest-first. PERSIST-008: every question this answers
/// (`frame count`, `current coverage`, `EffectiveFrames`) reads this record, never the
/// journal history, and never the Blogger's physical transcript (PERSIST-010).
type BlogProjectionState =
    { FrameEpochId: FrameEpochId
      Frames: BlogFrame list
      Coverage: BlogCoverage }

/// Why a context-recovery line was refused. Each case is one PERSIST-010 rule, so
/// a rejected envelope names the invariant it broke instead of a generic failure.
[<RequireQualifiedAccess>]
type BlogFoldRejection =
    /// The line was written against a different frame epoch than the one in force.
    /// Stale, or from a concurrent writer that lost.
    | StaleFrameEpoch of expected: FrameEpochId * actual: FrameEpochId
    /// `NextFrameEpochId` was not the successor of `PreviousFrameEpochId`.
    | NonSequentialFrameEpoch
    /// The ingest cursor did not advance. A committed entry that consumed nothing
    /// would let the same delta be blogged forever.
    | IngestCursorNotAdvanced
    /// The previous cursor in the line disagrees with the projection's.
    | IngestCursorMismatch
    /// Coverage moved backwards. CTX-011 forbids retreating within one numbering.
    | CoverageRetreated

// PERSIST-007's "TextDigest = digest(blob content)" is deliberately NOT a case
// here. This module is pure and holds only a `BlobRef`, so it cannot read the body
// to compare. Asserting the rule where it cannot be checked would produce a branch
// no input reaches — and a reader would then believe the fold verifies something it
// does not. The check belongs to the blob read boundary, which has the bytes.

module BlogProjection =

    let private coverableFrameCount
        (previousCutoff: int)
        (nextCutoff: int)
        (nextFrames: BlogFrame list)
        (previousCount: int)
        =
        if nextCutoff > previousCutoff then
            List.length nextFrames
        else
            previousCount

    let empty =
        { FrameEpochId = FrameEpochId.initial
          Frames = []
          Coverage =
            { IngestedThroughSequence = 0L
              CoverableTurnCutoffExclusive = 0
              CoveredPrefixDigest = ""
              CoverableFrameCount = 0 } }

    let frameCount (state: BlogProjectionState) = List.length state.Frames

    /// Oldest-first. The stored field is newest-first.
    let frames (state: BlogProjectionState) = List.rev state.Frames

    /// CTX-011: the frames a probe may build FrozenRecordPrefix from.
    ///
    /// Derived from `CoverableFrameCount` rather than stored as a second blob. The
    /// design draft carried a `CoverableBRef`/`CoverableBDigest` pair; a count is
    /// equivalent because the frame list is append-only within an epoch, and it cannot
    /// drift from the frames the way a separate materialised copy can.
    let coverableFrames (state: BlogProjectionState) =
        frames state |> List.truncate state.Coverage.CoverableFrameCount

    /// context-compression-029: the coverable frames whose own claim ends at or before
    /// `cutoffExclusive` — the verifiable material subset for that boundary.
    ///
    /// A frame that also covers turns at or past the cutoff is excluded rather than
    /// truncated: no part of an already-rendered summary can be withdrawn, so using it
    /// would show the same turn twice, once summarised and once raw. When no frame ends
    /// exactly at the cutoff the caller must not advance (`否则本次不前移`).
    let framesThroughCutoff (cutoffExclusive: int) (state: BlogProjectionState) =
        coverableFrames state
        |> List.takeWhile (fun frame -> frame.CutoffExclusive <= cutoffExclusive)

    /// PERSIST-010 `BlogObservationCommitted`. Frame append and coverage advance are one
    /// commit, so one function applies both or neither.
    ///
    /// `IngestedThroughSequence` advances in XTraceCursor coordinates; the turn/part
    /// pair is only used for the coverable-turn cutoff, which moves exclusively on
    /// complete turn boundaries. The record coverage may advance without the cutoff
    /// (mid-turn chunk) but never backwards.
    let applyEntry
        (frameEpoch: FrameEpochId)
        (previousIngestSequence: int64)
        (nextIngestSequence: int64)
        (previousCutoff: int)
        (nextCutoff: int)
        (nextDigest: string)
        (frame: BlogFrame)
        (state: BlogProjectionState)
        : Result<BlogProjectionState, BlogFoldRejection> =
        if frameEpoch <> state.FrameEpochId then
            Error(BlogFoldRejection.StaleFrameEpoch(state.FrameEpochId, frameEpoch))
        elif previousIngestSequence <> state.Coverage.IngestedThroughSequence then
            Error BlogFoldRejection.IngestCursorMismatch
        elif nextIngestSequence <= previousIngestSequence then
            Error BlogFoldRejection.IngestCursorNotAdvanced
        elif previousCutoff <> state.Coverage.CoverableTurnCutoffExclusive then
            Error BlogFoldRejection.CoverageRetreated
        elif nextCutoff < previousCutoff then
            Error BlogFoldRejection.CoverageRetreated
        else
            let storedFrame =
                { frame with
                    CoveredFromSequence = previousIngestSequence
                    CoveredThroughSequence = nextIngestSequence
                    CutoffExclusive = nextCutoff }

            let nextFrames = storedFrame :: state.Frames

            Ok
                { state with
                    Frames = nextFrames
                    Coverage =
                        { IngestedThroughSequence = nextIngestSequence
                          CoverableTurnCutoffExclusive = nextCutoff
                          CoveredPrefixDigest = nextDigest
                          // The coverable boundary moves only when the cutoff does. A
                          // chunk that stopped mid-turn appended a frame describing
                          // material the cutoff does not yet claim, so counting it would
                          // let a probe summarise a turn that is also still raw.
                          CoverableFrameCount =
                            coverableFrameCount previousCutoff nextCutoff nextFrames state.Coverage.CoverableFrameCount } }

    /// HOST-006 containment: Host compaction voided the Host turn numbering that
    /// PrefixCoverage indexes. Only the prefix mapping returns to the origin.
    ///
    /// Frames survive. `IngestedThroughSequence` (RecordCoverage) is a durable
    /// XTrace cursor and MUST stay put (COMPANION-008 / LWR): zeroing it would
    /// re-feed already-compressed X material into Y and duplicate lifecycle content.
    ///
    /// The frame epoch does NOT advance: no frame changed, so a squash written
    /// against the current epoch is still valid after a reanchor.
    let applyReanchor (state: BlogProjectionState) : BlogProjectionState =
        { state with
            Coverage =
                { state.Coverage with
                    CoverableTurnCutoffExclusive = 0
                    CoveredPrefixDigest = ""
                    // Zero, not "all frames": the frames survive, but none of them is
                    // coverable any more. A frame is coverable because the cutoff claims
                    // the X turns it describes, and compaction voided every such claim.
                    CoverableFrameCount = 0 } }

    /// CTX-011: is there a covered prefix a probe could be built from at all.
    ///
    /// `CoverableTurnCutoffExclusive = 0` means nothing complete has been consumed
    /// yet — the initial state and the post-reanchor state are the same answer here,
    /// which is why the reanchor needs no separate flag.
    let hasCoverage (state: BlogProjectionState) =
        state.Coverage.CoverableTurnCutoffExclusive > 0
