namespace Wanxiangshu.Context.Prefix

open Wanxiangshu.Foundation.Identity

/// The K-window cutoff decision for one cognitive phase boundary.
///
/// context-compression-028: with committed phases `A1..AN` and `Bi` the start of the
/// complete semantic turn containing `Ai`, the desired cutoff is `Bj` for
/// `j = max(1, N − K + 1)`. `N = 0` yields no cutoff at all — there is nothing to
/// fold yet, so the caller keeps its raw tail.
///
/// This module decides only the DESIRE. Whether the desire is reachable depends on
/// Blogger coverage, which the coordinator owns; pairing them here would let a pure
/// function claim a compression that has no evidence behind it.
[<RequireQualifiedAccess>]
type PhaseWindowDecision =
    /// No phase has committed, so there is no window to fold.
    | NoPhases
    /// The window selects `cutoffExclusive`, the exclusive end of the earliest turn
    /// the window still keeps raw.
    | KeepFrom of cutoffExclusive: int

[<RequireQualifiedAccess>]
module PhaseWindow =

    /// context-compression-028: the window frozen when the owner opens. Positive by
    /// construction, so `validateK` only has to refuse a *supplied* value. Nothing in
    /// the product configures a different one yet — a per-owner override would have to
    /// be recorded as an owner fact before this constant could stop being the answer
    /// for every owner.
    let defaultK = 2

    /// `K` is frozen when the owner opens and must be a positive integer. Zero would
    /// mean "keep nothing raw", which would delete the turn carrying the live canvas.
    let validateK (k: int) : Result<unit, string> =
        if k < 1 then
            Error(sprintf "phase window K must be a positive integer; got %d (context-compression-028)" k)
        else
            Ok()

    type PhaseCommitWindow = { PhaseCallIds: ToolCallId list }

    let emptyWindow: PhaseCommitWindow = { PhaseCallIds = [] }

    /// Admit one committed phase and keep at most `k` entries, oldest first.
    ///
    /// Only the window is retained, never the whole phase history: `desiredCutoff`
    /// reads `Bj` for `j = max(1, N − K + 1)`, and with the list holding the last
    /// `min(N, K)` commits that index is always the oldest retained one. A bounded
    /// window is therefore exactly as much evidence as the formula needs, and phase
    /// commits do not accumulate here as the session grows.
    let appendPhase (k: int) (callId: ToolCallId) (window: PhaseCommitWindow) : PhaseCommitWindow =
        let kept =
            if k < 1 then
                []
            else
                window.PhaseCallIds @ [ callId ] |> List.rev |> List.truncate k |> List.rev

        { PhaseCallIds = kept }

    /// The window's desire: the turn start of the oldest phase still kept raw.
    ///
    /// `turnStartOf` is the canonical XTrace turn of a committed tool call in the
    /// CURRENT generation, injected so this stays pure. A phase whose turn is no
    /// longer addressable proves nothing — the caller must keep its raw tail rather
    /// than fold at a boundary it cannot point to (context-compression-014/029).
    let desiredCutoffOf (turnStartOf: ToolCallId -> int option) (window: PhaseCommitWindow) : PhaseWindowDecision =
        window.PhaseCallIds
        |> List.tryHead
        |> Option.bind turnStartOf
        |> Option.map PhaseWindowDecision.KeepFrom
        |> Option.defaultValue PhaseWindowDecision.NoPhases

    /// The desired cutoff for `phaseTurnStarts`, the ordered start boundary of each
    /// committed phase's own semantic turn.
    ///
    /// The list order is the commit order, and equal boundaries are legal: two
    /// commits inside one turn share `Bi`, so the window keeps the whole turn rather
    /// than trying to split a call from its result.
    let desiredCutoff (k: int) (phaseTurnStarts: int list) : PhaseWindowDecision =
        match phaseTurnStarts with
        | [] -> PhaseWindowDecision.NoPhases
        | starts ->
            let n = List.length starts
            let j = max 1 (n - k + 1)
            let index = min (max j 1) n
            PhaseWindowDecision.KeepFrom(List.item (index - 1) starts)
