namespace Wanxiangshu.Next.OpenCode

open System
open Wanxiangshu.Next.Kernel.Identity

/// Session-wide formal assistant text (A) accumulation helpers.
/// A is the entire Session's formal assistant body, not the last turn only.
module TerminalSessionA =

    let turnText (turn: ReconciledTurn) =
        CompletedTurnClassifier.partsText turn.Parts

    /// Append one formal-assistant chunk into the session-wide A accumulator.
    let recordFormalAssistantText (eventPort: IEventObservationPort) (sessionId: SessionId) (text: string) =
        if not (String.IsNullOrWhiteSpace text) then
            match eventPort with
            | :? Events.HostEventPort as hostPort -> hostPort.RecordSessionOutput sessionId text
            | _ -> ()

    /// Session-wide A: all formal assistant chunks recorded for this Session.
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

    /// Append this turn's formal text, then return full Session A.
    let accumulateTurn (eventPort: IEventObservationPort) (turn: ReconciledTurn) =
        let text = turnText turn
        recordFormalAssistantText eventPort turn.SessionId text
        sessionWideA eventPort turn.SessionId text
