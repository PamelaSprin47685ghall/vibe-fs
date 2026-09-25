namespace Wanxiangshu.Context.Prefix

open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type PhaseWindowDecision =
    | NoPhases
    | KeepFrom of cutoffExclusive: int

[<RequireQualifiedAccess>]
module PhaseWindow =
    /// context-compression-028: the window in force when the owner opens.
    val defaultK: int

    val validateK: k: int -> Result<unit, string>

    /// The committed phases the window still keeps raw, oldest first, bounded by `K`.
    type PhaseCommitWindow = { PhaseCallIds: ToolCallId list }

    val emptyWindow: PhaseCommitWindow

    /// Admit one committed phase; keeps at most `k` entries.
    val appendPhase: k: int -> callId: ToolCallId -> window: PhaseCommitWindow -> PhaseCommitWindow

    /// The window's desire: the turn start of the oldest phase still kept raw, or
    /// `NoPhases` when nothing is retained or the turn is no longer addressable.
    val desiredCutoffOf: turnStartOf: (ToolCallId -> int option) -> window: PhaseCommitWindow -> PhaseWindowDecision

    /// The desired cutoff for the given ordered phase turn starts. Commit order;
    /// equal boundaries are legal because two commits in one turn share their `Bi`.
    val desiredCutoff: k: int -> phaseTurnStarts: int list -> PhaseWindowDecision
