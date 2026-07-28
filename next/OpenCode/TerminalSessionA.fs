namespace Wanxiangshu.Next.OpenCode

open System
open Wanxiangshu.Next.Kernel.Identity

/// Session-wide A accumulation helpers.
/// A is the entire Session assistant body including formal text and reasoning/thinking,
/// not the last turn only. Tool raw streams are excluded.
module TerminalSessionA =

    let turnText (turn: ReconciledTurn) =
        CompletedTurnClassifier.partsSessionA turn.Parts

    /// Append one A chunk (text + reasoning) into the session-wide accumulator.
    let recordFormalAssistantText (eventPort: IEventObservationPort) (sessionId: SessionId) (text: string) =
        if not (String.IsNullOrWhiteSpace text) then
            match eventPort with
            | :? Events.HostEventPort as hostPort -> hostPort.RecordSessionOutput sessionId text
            | _ -> ()

    /// Session-wide A: all A chunks recorded for this Session.
    /// Falls back to the current-turn text when the port does not accumulate.
    let sessionWideA (eventPort: IEventObservationPort) (sessionId: SessionId) (turnText: string) =
        match eventPort.GetSessionOutput sessionId with
        | [] -> turnText
        | chunks ->
            chunks
            |> List.filter (fun chunk -> not (String.IsNullOrWhiteSpace chunk))
            |> function
                | [] -> turnText
                | xs -> String.concat "\n\n" xs

    /// Append this turn's A material, then return full Session A.
    let accumulateTurn (eventPort: IEventObservationPort) (turn: ReconciledTurn) =
        let text = turnText turn
        recordFormalAssistantText eventPort turn.SessionId text
        sessionWideA eventPort turn.SessionId text

    /// Session-wide A as optional background text (empty → None).
    let fullText (eventPort: IEventObservationPort) (sessionId: SessionId) =
        match eventPort.GetSessionOutput sessionId with
        | [] -> None
        | chunks ->
            chunks
            |> List.filter (fun chunk -> not (String.IsNullOrWhiteSpace chunk))
            |> function
                | [] -> None
                | xs -> Some(String.concat "\n\n" xs)
