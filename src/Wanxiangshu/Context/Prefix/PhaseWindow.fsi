namespace Wanxiangshu.Context.Prefix

open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type PhaseWindowDecision =
    | NoPhases
    | KeepFrom of cutoffExclusive: int

[<RequireQualifiedAccess>]
module PhaseWindow =
    /// context-compression-028: fixed number of recent todowrite checkpoints kept raw.
    val retainCheckpoints: int

    type PhaseCheckpoint = { ToolCallId: ToolCallId }

    type PhaseCommitWindow = { Checkpoints: PhaseCheckpoint list }

    val emptyWindow: PhaseCommitWindow

    val appendCheckpoint: callId: ToolCallId -> window: PhaseCommitWindow -> PhaseCommitWindow

    val pruneBefore:
        turnStartOf: (ToolCallId -> int option) ->
        cutoffExclusive: int ->
        window: PhaseCommitWindow ->
            PhaseCommitWindow

    val desiredCutoffOf: turnStartOf: (ToolCallId -> int option) -> window: PhaseCommitWindow -> PhaseWindowDecision

    val desiredCutoff: checkpointTurnStarts: int list -> PhaseWindowDecision
