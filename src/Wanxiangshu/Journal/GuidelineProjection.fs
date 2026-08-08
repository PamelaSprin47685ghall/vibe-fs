namespace Wanxiangshu.Journal

open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// HOST-013 durable auto-injected pairs for one transcript (append-only).
type PairProgrammingGuideline =
    { Ordinal: int64
      CallId: ToolCallId
      MarkerText: string }

type GuidelineProjectionState =
    { Pairs: PairProgrammingGuideline list }

[<RequireQualifiedAccess>]
type GuidelineFoldRejection =
    | NonSequentialOrdinal of expected: int64 * actual: int64
    | DuplicateCallId of callId: string

module GuidelineProjection =

    let empty: GuidelineProjectionState = { Pairs = [] }

    let pairs (state: GuidelineProjectionState) : PairProgrammingGuideline list = state.Pairs

    let nextOrdinal (state: GuidelineProjectionState) : int64 =
        // Pairs append at the end (oldest → newest). Successor is last.Ordinal + 1.
        match state.Pairs with
        | [] -> 1L
        | pairs -> (List.last pairs).Ordinal + 1L

    let apply
        (ordinal: int64)
        (callId: ToolCallId)
        (markerText: string)
        (state: GuidelineProjectionState)
        : Result<GuidelineProjectionState, GuidelineFoldRejection> =
        let expected = nextOrdinal state

        if ordinal <> expected then
            Error(GuidelineFoldRejection.NonSequentialOrdinal(expected, ordinal))
        elif
            state.Pairs
            |> List.exists (fun pair -> ToolCallId.value pair.CallId = ToolCallId.value callId)
        then
            Error(GuidelineFoldRejection.DuplicateCallId(ToolCallId.value callId))
        else
            Ok
                { Pairs =
                    state.Pairs
                    @ [ { Ordinal = ordinal
                          CallId = callId
                          MarkerText = markerText } ] }
