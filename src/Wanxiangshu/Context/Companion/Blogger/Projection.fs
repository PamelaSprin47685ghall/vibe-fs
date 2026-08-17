namespace Wanxiangshu.Context.Companion.Blogger

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// COMPANION-005: what a frame is. Entry and Squash are interchangeable inputs to
/// a later squash (CTX-012 cascade). There is no Seed: the parent LWR is a child's
/// input context, never a frame of the child's own work log (COMPANION-003).
///
/// The kind is recorded rather than derived because a squash of `[Entry; Entry]`
/// is indistinguishable from a squash of `[Entry; Entry]` once written, and
/// diagnostics need to say which history a frame came from.
[<RequireQualifiedAccess>]
type BlogFrameKind =
    | Entry
    | Squash

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
    /// A squash claimed a frame count outside `1 .. current`.
    | CoveredFrameCountOutOfRange of claimed: int * available: int

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

    /// CTX-012: how many of the oldest frames the next squash takes.
    ///
    /// `ceil(m / 2)`, and `m = 1` is not skipped — a single frame can still be
    /// large and redundant enough for a rewrite to shorten it materially.
    let squashWidth (state: BlogProjectionState) =
        let m = frameCount state
        if m = 0 then 0 else (m + 1) / 2

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
                    CoveredThroughSequence = nextIngestSequence }

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
                            coverableFrameCount
                                previousCutoff
                                nextCutoff
                                nextFrames
                                state.Coverage.CoverableFrameCount } }

    /// PERSIST-010 `BlogObservationsSquashed`. Replaces the oldest `count` frames with
    /// one, advances the frame epoch, and leaves coverage untouched.
    ///
    /// `count = frameCount` is allowed: collapsing every frame into one is a
    /// legitimate cascade step. What is refused is a count larger than what exists,
    /// or zero.
    let applySquash
        (previousEpoch: FrameEpochId)
        (nextEpoch: FrameEpochId)
        (count: int)
        (frame: BlogFrame)
        (state: BlogProjectionState)
        : Result<BlogProjectionState, BlogFoldRejection> =
        let available = frameCount state

        if previousEpoch <> state.FrameEpochId then
            Error(BlogFoldRejection.StaleFrameEpoch(state.FrameEpochId, previousEpoch))
        elif nextEpoch <> FrameEpochId.next previousEpoch then
            Error BlogFoldRejection.NonSequentialFrameEpoch
        elif count < 1 || count > available then
            Error(BlogFoldRejection.CoveredFrameCountOutOfRange(count, available))
        else
            // The coverable boundary is a frame INDEX, so collapsing frames below it
            // moves it. The cutoff and the digest do not change — a squash rewrites how
            // B is represented, not which X turns it covers (CTX-012).
            //
            // Three cases, and the middle one is why this is not just a subtraction:
            //
            //   count < coverable   the boundary shrinks by `count - 1`
            //   count >= coverable  the whole covered region became one frame, so the
            //                       boundary is that frame alone. The frame may also
            //                       carry material past the cutoff; that is redundancy
            //                       (those turns are still raw after it), not a false
            //                       claim, because the cutoff and its digest are
            //                       statements about X's prefix, not about B's contents.
            //   coverable = 0       nothing was coverable, and a squash cannot create
            //                       coverage out of frames the cutoff never claimed.
            let coverable = state.Coverage.CoverableFrameCount

            let nextCoverable = if coverable = 0 then 0 else max 1 (coverable - count + 1)

            let oldestFirst = frames state
            let replaced = List.truncate count oldestFirst

            // Squash unions replaced frames' coverage (min from, max through).
            // A merged interval can span two invocations; overlap decides
            // Chronicle membership — do not invent a splitter.
            let storedFrame =
                { frame with
                    CoveredFromSequence = replaced |> List.map (fun item -> item.CoveredFromSequence) |> List.min
                    CoveredThroughSequence = replaced |> List.map (fun item -> item.CoveredThroughSequence) |> List.max }

            Ok
                { state with
                    FrameEpochId = nextEpoch
                    Frames = List.rev (storedFrame :: List.skip count oldestFirst)
                    Coverage =
                        { state.Coverage with
                            CoverableFrameCount = nextCoverable } }

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
