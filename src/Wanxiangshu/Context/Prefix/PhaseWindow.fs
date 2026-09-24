namespace Wanxiangshu.Context.Prefix

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

    /// `K` is frozen when the owner opens and must be a positive integer. Zero would
    /// mean "keep nothing raw", which would delete the turn carrying the live canvas.
    let validateK (k: int) : Result<unit, string> =
        if k < 1 then
            Error(sprintf "phase window K must be a positive integer; got %d (context-compression-028)" k)
        else
            Ok()

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

    /// The actual cutoff: the desired one clamped to what the evidence supports.
    ///
    /// `coveredCutoffExclusive` is the furthest cutoff the committed coverage proves.
    /// The actual cutoff never moves past it, and never retreats below
    /// `committedCutoffExclusive` — a committed cutoff is history, not a preference.
    /// Clamp a desired cutoff to the evidence: never past what coverage proves, and
    /// never behind a cutoff already committed.
    ///
    /// Coverage fallen behind the committed cutoff cannot retreat it — a committed
    /// cutoff is history, so the caller keeps what it already has until coverage
    /// catches up.
    let private clampTo (committedCutoffExclusive: int option) (desired: int) (covered: int) =
        let floor = committedCutoffExclusive |> Option.defaultValue 0

        if covered <= floor then
            PhaseWindowDecision.KeepFrom floor
        else
            PhaseWindowDecision.KeepFrom(min desired covered)

    let actualCutoff
        (decision: PhaseWindowDecision)
        (committedCutoffExclusive: int option)
        (coveredCutoffExclusive: int option)
        : PhaseWindowDecision =
        match decision, coveredCutoffExclusive with
        | PhaseWindowDecision.NoPhases, _ -> PhaseWindowDecision.NoPhases
        | _, None -> PhaseWindowDecision.NoPhases
        | PhaseWindowDecision.KeepFrom desired, Some covered -> clampTo committedCutoffExclusive desired covered
