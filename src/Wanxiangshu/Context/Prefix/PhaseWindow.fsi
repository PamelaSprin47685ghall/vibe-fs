namespace Wanxiangshu.Context.Prefix

open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type PhaseWindowDecision =
    | NoPhases
    | KeepFrom of cutoffExclusive: int

[<RequireQualifiedAccess>]
module PhaseWindow =
    val validateK: k: int -> Result<unit, string>

    type PhaseCheckpoint =
        { ToolCallId: ToolCallId
          RetainCheckpoints: int }

    type PhaseCommitWindow = { Checkpoints: PhaseCheckpoint list }

    val emptyWindow: PhaseCommitWindow

    val appendCheckpoint:
        callId: ToolCallId -> retainCheckpoints: int -> window: PhaseCommitWindow -> Result<PhaseCommitWindow, string>

    val pruneBefore:
        turnStartOf: (ToolCallId -> int option) ->
        cutoffExclusive: int ->
        window: PhaseCommitWindow ->
            PhaseCommitWindow

    val desiredCutoffOf: turnStartOf: (ToolCallId -> int option) -> window: PhaseCommitWindow -> PhaseWindowDecision

    val desiredCutoff: k: int -> checkpointTurnStarts: int list -> PhaseWindowDecision
