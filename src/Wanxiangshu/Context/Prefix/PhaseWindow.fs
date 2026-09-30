namespace Wanxiangshu.Context.Prefix

open Wanxiangshu.Foundation.Identity

/// Desired raw-history boundary selected by the latest todowrite checkpoint.
[<RequireQualifiedAccess>]
type PhaseWindowDecision =
    | NoPhases
    | KeepFrom of cutoffExclusive: int

[<RequireQualifiedAccess>]
module PhaseWindow =

    let validateK (k: int) : Result<unit, string> =
        if k < 1 then
            Error(sprintf "retainCheckpoints must be a positive integer; got %d (context-compression-028)" k)
        else
            Ok()

    type PhaseCheckpoint =
        { ToolCallId: ToolCallId
          RetainCheckpoints: int }

    type PhaseCommitWindow = { Checkpoints: PhaseCheckpoint list }

    let emptyWindow: PhaseCommitWindow = { Checkpoints = [] }

    let private appendFresh callId retainCheckpoints window =
        Ok
            { Checkpoints =
                window.Checkpoints
                @ [ { ToolCallId = callId
                      RetainCheckpoints = retainCheckpoints } ] }

    let private resolveExisting callId retainCheckpoints window existing =
        match existing with
        | Some checkpoint when checkpoint.RetainCheckpoints = retainCheckpoints -> Ok window
        | Some _ ->
            Error(sprintf "todowrite checkpoint %s committed two retainCheckpoints values" (ToolCallId.value callId))
        | None -> appendFresh callId retainCheckpoints window

    /// Record one successful todowrite checkpoint without prematurely forgetting
    /// older raw checkpoints. A real prefix rebase is what retires old evidence.
    let appendCheckpoint
        (callId: ToolCallId)
        (retainCheckpoints: int)
        (window: PhaseCommitWindow)
        : Result<PhaseCommitWindow, string> =
        match validateK retainCheckpoints with
        | Error reason -> Error reason
        | Ok() ->
            window.Checkpoints
            |> List.tryFind (fun checkpoint -> checkpoint.ToolCallId = callId)
            |> resolveExisting callId retainCheckpoints window

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

    /// Use the K supplied by the latest checkpoint. K includes the current call:
    /// one means fold directly before the current todowrite; two keeps the current
    /// and previous todowrite and folds before the previous one.
    let desiredCutoffOf (turnStartOf: ToolCallId -> int option) (window: PhaseCommitWindow) : PhaseWindowDecision =
        match List.tryLast window.Checkpoints with
        | None -> PhaseWindowDecision.NoPhases
        | Some latest ->
            let retained =
                window.Checkpoints
                |> List.rev
                |> List.truncate latest.RetainCheckpoints
                |> List.rev

            retained
            |> List.tryHead
            |> Option.bind (fun checkpoint -> turnStartOf checkpoint.ToolCallId)
            |> Option.map PhaseWindowDecision.KeepFrom
            |> Option.defaultValue PhaseWindowDecision.NoPhases

    let desiredCutoff (k: int) (checkpointTurnStarts: int list) : PhaseWindowDecision =
        match checkpointTurnStarts with
        | [] -> PhaseWindowDecision.NoPhases
        | starts ->
            let n = List.length starts
            let j = max 1 (n - k + 1)
            let index = min (max j 1) n
            PhaseWindowDecision.KeepFrom(List.item (index - 1) starts)
