namespace Wanxiangshu.Context.Prefix

[<RequireQualifiedAccess>]
type PhaseWindowDecision =
    | NoPhases
    | KeepFrom of cutoffExclusive: int

[<RequireQualifiedAccess>]
module PhaseWindow =
    val validateK: k: int -> Result<unit, string>

    /// The desired cutoff for the given ordered phase turn starts. Commit order;
    /// equal boundaries are legal because two commits in one turn share their `Bi`.
    val desiredCutoff: k: int -> phaseTurnStarts: int list -> PhaseWindowDecision

    /// The desired cutoff clamped to what committed coverage proves, never below an
    /// already-committed cutoff.
    val actualCutoff:
        decision: PhaseWindowDecision ->
        committedCutoffExclusive: int option ->
        coveredCutoffExclusive: int option ->
            PhaseWindowDecision
