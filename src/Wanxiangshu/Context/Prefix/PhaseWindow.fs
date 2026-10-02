namespace Wanxiangshu.Context.Prefix

open Wanxiangshu.Foundation.Identity

/// Desired raw-history boundary selected by the latest todowrite checkpoint.
[<RequireQualifiedAccess>]
type PhaseWindowDecision =
    | NoPhases
    | KeepFrom of cutoffExclusive: int

[<RequireQualifiedAccess>]
module PhaseWindow =

    /// context-compression-028: the number of recent todowrite checkpoints whose
    /// semantic turns stay raw. Fixed system parameter, not a session setting.
    let retainCheckpoints = 3

    type PhaseCheckpoint = { ToolCallId: ToolCallId }

    type PhaseCommitWindow = { Checkpoints: PhaseCheckpoint list }

    let emptyWindow: PhaseCommitWindow = { Checkpoints = [] }

    /// Record one successful todowrite checkpoint. The same call identity
    /// re-entering is a replay of the same fact, never a second checkpoint.
    let appendCheckpoint (callId: ToolCallId) (window: PhaseCommitWindow) : PhaseCommitWindow =
        if
            window.Checkpoints
            |> List.exists (fun checkpoint -> checkpoint.ToolCallId = callId)
        then
            window
        else
            { Checkpoints = window.Checkpoints @ [ { ToolCallId = callId } ] }

    /// A committed rebase permanently removes checkpoints strictly before its
    /// cutoff. Unaddressable checkpoints are retained because XTrace capture may
    /// still materialize a newer raw call later in the same generation.
    let pruneBefore
        (turnStartOf: ToolCallId -> int option)
        (cutoffExclusive: int)
        (window: PhaseCommitWindow)
        : PhaseCommitWindow =
        { Checkpoints =
            window.Checkpoints
            |> List.filter (fun checkpoint ->
                match turnStartOf checkpoint.ToolCallId with
                | Some turn -> turn >= cutoffExclusive
                | None -> true) }

    /// The window desire: keep the last `retainCheckpoints` checkpoints and fold
    /// directly before the oldest of them. Retention is a count, so a committed
    /// rebase is the only thing that forgets checkpoint evidence.
    let desiredCutoffOf (turnStartOf: ToolCallId -> int option) (window: PhaseCommitWindow) : PhaseWindowDecision =
        match List.tryLast window.Checkpoints with
        | None -> PhaseWindowDecision.NoPhases
        | Some _ ->
            window.Checkpoints
            |> List.rev
            |> List.truncate retainCheckpoints
            |> List.rev
            |> List.tryHead
            |> Option.bind (fun checkpoint -> turnStartOf checkpoint.ToolCallId)
            |> Option.map PhaseWindowDecision.KeepFrom
            |> Option.defaultValue PhaseWindowDecision.NoPhases

    let desiredCutoff (checkpointTurnStarts: int list) : PhaseWindowDecision =
        match checkpointTurnStarts with
        | [] -> PhaseWindowDecision.NoPhases
        | starts ->
            let n = List.length starts
            let j = max 1 (n - retainCheckpoints + 1)
            let index = min (max j 1) n
            PhaseWindowDecision.KeepFrom(List.item (index - 1) starts)
