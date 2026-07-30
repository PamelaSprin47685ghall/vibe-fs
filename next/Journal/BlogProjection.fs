namespace Wanxiangshu.Next.Journal

open Wanxiangshu.Next.Kernel.Identity

/// COMPANION-005: what a frame is. Entry and Squash are interchangeable inputs to
/// a later squash (CTX-012 cascade), and Seed is the parent work record inherited
/// at creation (COMPANION-004).
///
/// The kind is recorded rather than derived because a squash of `[Seed; Entry]` is
/// indistinguishable from a squash of `[Entry; Entry]` once written, and
/// diagnostics need to say which history a frame came from.
[<RequireQualifiedAccess>]
type BlogFrameKind =
    | Entry
    | Squash
    | Seed

type BlogFrame =
    { Kind: BlogFrameKind
      Digest: BlobDigest
      TextRef: BlobRef }

/// CTX-011: where the Companion has consumed to. A message index alone is not
/// enough — one large message may span several 200 KiB chunks, so a chunk
/// boundary can fall inside a turn.
type SemanticCursor = { TurnIndex: int; PartIndex: int }

/// CTX-011: the two positions plus the proof that ties the second one to X.
///
/// `IngestCursor` may sit mid-turn; `CoverableTurnCutoffExclusive` never does.
/// A probe may only use the latter (COMPANION-011), which is why they are separate
/// fields rather than one "progress" number.
type BlogCoverage =
    { IngestCursor: SemanticCursor
      CoverableTurnCutoffExclusive: int
      CoveredPrefixDigest: string }

/// The Companion's durable state: the frame sequence and what it covers.
///
/// `Frames` is oldest-first. PERSIST-008: every question this answers
/// (`frame count`, `current coverage`, `LatestB`) reads this record, never the
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

    let private originCursor = { TurnIndex = 0; PartIndex = 0 }

    let empty =
        { FrameEpochId = FrameEpochId.initial
          Frames = []
          Coverage =
            { IngestCursor = originCursor
              CoverableTurnCutoffExclusive = 0
              CoveredPrefixDigest = "" } }

    let frameCount (state: BlogProjectionState) = List.length state.Frames

    /// COMPANION-004: a new Y inherits the parent's work record as a `Seed` frame.
    ///
    /// A seed covers no X turn of its own — it describes the PARENT's history, not
    /// this session's — so coverage stays at the origin. That is why the first real
    /// entry still starts from cursor 0 despite a frame already being present.
    ///
    /// Only valid on a fresh projection. Seeding a session that already has frames
    /// would insert parent history in the middle of its own, which no clause
    /// describes and `applySquash` would then fold into a frame claiming to
    /// summarise both.
    let withSeed (seed: BlogFrame) (state: BlogProjectionState) : BlogProjectionState =
        if List.isEmpty state.Frames then
            { state with Frames = [ seed ] }
        else
            state

    /// Lexicographic on (turn, part). The part index is only meaningful within a
    /// turn, so comparing it across turns would let a later turn's early part look
    /// like a retreat.
    let private isAfter (next: SemanticCursor) (previous: SemanticCursor) =
        next.TurnIndex > previous.TurnIndex
        || (next.TurnIndex = previous.TurnIndex && next.PartIndex > previous.PartIndex)

    /// CTX-012: how many of the oldest frames the next squash takes.
    ///
    /// `ceil(m / 2)`, and `m = 1` is not skipped — a single frame can still be
    /// large and redundant enough for a rewrite to shorten it materially.
    let squashWidth (state: BlogProjectionState) =
        let m = frameCount state
        if m = 0 then 0 else (m + 1) / 2

    /// PERSIST-010 `BlogEntryCommitted`. Frame append and coverage advance are one
    /// commit, so one function applies both or neither.
    let applyEntry
        (frameEpoch: FrameEpochId)
        (previousIngest: SemanticCursor)
        (nextIngest: SemanticCursor)
        (previousCutoff: int)
        (nextCutoff: int)
        (nextDigest: string)
        (frame: BlogFrame)
        (state: BlogProjectionState)
        : Result<BlogProjectionState, BlogFoldRejection> =
        if frameEpoch <> state.FrameEpochId then
            Error(BlogFoldRejection.StaleFrameEpoch(state.FrameEpochId, frameEpoch))
        elif previousIngest <> state.Coverage.IngestCursor then
            Error BlogFoldRejection.IngestCursorMismatch
        elif not (isAfter nextIngest previousIngest) then
            Error BlogFoldRejection.IngestCursorNotAdvanced
        elif previousCutoff <> state.Coverage.CoverableTurnCutoffExclusive then
            Error BlogFoldRejection.CoverageRetreated
        elif nextCutoff < previousCutoff then
            Error BlogFoldRejection.CoverageRetreated
        else
            Ok
                { state with
                    Frames = state.Frames @ [ frame ]
                    Coverage =
                        { IngestCursor = nextIngest
                          CoverableTurnCutoffExclusive = nextCutoff
                          CoveredPrefixDigest = nextDigest } }

    /// PERSIST-010 `BlogSquashCommitted`. Replaces the oldest `count` frames with
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
            Ok
                { state with
                    FrameEpochId = nextEpoch
                    Frames = frame :: List.skip count state.Frames }

    /// HOST-006 containment: the numbering these positions refer to was voided by a
    /// Host compaction, so coverage returns to the origin.
    ///
    /// Frames survive. B records work that really happened; what compaction voided
    /// is the mapping from B to X turn indices, not the work log itself.
    ///
    /// The frame epoch does NOT advance: no frame changed, so a squash written
    /// against the current epoch is still valid after a reanchor.
    let applyReanchor (state: BlogProjectionState) : BlogProjectionState =
        { state with
            Coverage =
                { IngestCursor = originCursor
                  CoverableTurnCutoffExclusive = 0
                  CoveredPrefixDigest = "" } }

    /// CTX-011: is there a covered prefix a probe could be built from at all.
    ///
    /// `CoverableTurnCutoffExclusive = 0` means nothing complete has been consumed
    /// yet — the initial state and the post-reanchor state are the same answer here,
    /// which is why the reanchor needs no separate flag.
    let hasCoverage (state: BlogProjectionState) =
        state.Coverage.CoverableTurnCutoffExclusive > 0
