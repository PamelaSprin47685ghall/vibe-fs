namespace Wanxiangshu.OpenCode.Host.PairProgramming

open Wanxiangshu.Foundation.Identity

type PairProgrammingGuideline =
    { Ordinal: int64
      CallId: ToolCallId
      MarkerText: string
      CallGap: TranscriptGap
      ResultGap: TranscriptGap }

type GuidelineProjectionState =
    { Pairs: PairProgrammingGuideline list
      CallIds: Set<string>
      Placements: Set<string>
      VisibleFromOrdinal: int64 }

[<RequireQualifiedAccess>]
type GuidelineFoldRejection =
    | NonSequentialOrdinal of expected: int64 * actual: int64
    | DuplicateCallId of callId: string
    | DuplicatePlacement of callGap: TranscriptGap * resultGap: TranscriptGap

module GuidelineProjection =
    val empty: GuidelineProjectionState
    val pairs: GuidelineProjectionState -> PairProgrammingGuideline list
    val visiblePairs: GuidelineProjectionState -> PairProgrammingGuideline list
    val nextOrdinal: GuidelineProjectionState -> int64
    val applyReanchor: GuidelineProjectionState -> GuidelineProjectionState
    val restoreVisibilityFloor: int64 -> GuidelineProjectionState -> GuidelineProjectionState
    val apply:
        ordinal: int64 ->
        callId: ToolCallId ->
        markerText: string ->
        callGap: TranscriptGap ->
        resultGap: TranscriptGap ->
        state: GuidelineProjectionState ->
        Result<GuidelineProjectionState, GuidelineFoldRejection>
