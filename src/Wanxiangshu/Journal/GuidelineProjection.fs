namespace Wanxiangshu.Journal

open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// HOST-013 durable auto-injected pairs for one transcript (append-only).
///
/// `CallGap` / `ResultGap` anchor the two halves to transcript positions, so a
/// replay restores every historical half at its original gap regardless of what
/// the current transcript looks like (prefix law, ARCH-004).
type PairProgrammingGuideline =
    { Ordinal: int64
      CallId: ToolCallId
      MarkerText: string
      CallGap: TranscriptGap
      ResultGap: TranscriptGap }

type GuidelineProjectionState =
    { /// Stored newest-first so replay cons is O(1). `pairs` restores oldest-first.
      Pairs: PairProgrammingGuideline list
      CallIds: Set<string>
      Placements: Set<string> }

[<RequireQualifiedAccess>]
type GuidelineFoldRejection =
    | NonSequentialOrdinal of expected: int64 * actual: int64
    | DuplicateCallId of callId: string
    /// HOST-013 §8: one placement identity (SessionId + CallGap + ResultGap)
    /// admits at most one pair. The projection is per-session, so the session
    /// part of the identity is implicit here.
    | DuplicatePlacement of callGap: TranscriptGap * resultGap: TranscriptGap

module GuidelineProjection =

    let empty: GuidelineProjectionState =
        { Pairs = []
          CallIds = Set.empty
          Placements = Set.empty }

    let pairs (state: GuidelineProjectionState) : PairProgrammingGuideline list = List.rev state.Pairs

    let private gapKey (gap: TranscriptGap) =
        match gap with
        | TranscriptGap.Start -> "s"
        | TranscriptGap.Before addr -> "b:" + TranscriptMessageAddress.value addr
        | TranscriptGap.After addr -> "a:" + TranscriptMessageAddress.value addr

    let private placementKey callGap resultGap = gapKey callGap + "|" + gapKey resultGap

    let nextOrdinal (state: GuidelineProjectionState) : int64 =
        // Pairs are stored newest-first. Successor is newest.Ordinal + 1.
        match state.Pairs with
        | [] -> 1L
        | newest :: _ -> newest.Ordinal + 1L

    let apply
        (ordinal: int64)
        (callId: ToolCallId)
        (markerText: string)
        (callGap: TranscriptGap)
        (resultGap: TranscriptGap)
        (state: GuidelineProjectionState)
        : Result<GuidelineProjectionState, GuidelineFoldRejection> =
        let expected = nextOrdinal state

        let callKey = ToolCallId.value callId
        let placeKey = placementKey callGap resultGap

        if ordinal <> expected then
            Error(GuidelineFoldRejection.NonSequentialOrdinal(expected, ordinal))
        elif Set.contains callKey state.CallIds then
            Error(GuidelineFoldRejection.DuplicateCallId callKey)
        elif Set.contains placeKey state.Placements then
            Error(GuidelineFoldRejection.DuplicatePlacement(callGap, resultGap))
        else
            Ok
                { Pairs =
                    { Ordinal = ordinal
                      CallId = callId
                      MarkerText = markerText
                      CallGap = callGap
                      ResultGap = resultGap }
                    :: state.Pairs
                  CallIds = Set.add callKey state.CallIds
                  Placements = Set.add placeKey state.Placements }
