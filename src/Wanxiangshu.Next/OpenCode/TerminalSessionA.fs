namespace Wanxiangshu.Next.OpenCode

open System
open Wanxiangshu.Next.Kernel.Identity

/// HOST-005: session-wide A is the whole Session's assistant body — formal text
/// plus host-visible reasoning, accumulated across every provider run. Tool raw
/// streams are excluded.
///
/// Accumulation is segmented by `ProviderRunIdentity` (COMPANION-005: one round's
/// assistant body is the unit appended to B). Everything here is a projection of
/// those segments, so there is no second in-memory copy to keep in step.
module TerminalSessionA =

    let turnText (turn: ReconciledTurn) =
        CompletedTurnClassifier.partsSessionA turn.Parts

    /// Join A segments in provider-run order.
    let private render (records: SessionARecord list) =
        records
        |> List.map (fun record -> record.Text)
        |> List.filter (String.IsNullOrWhiteSpace >> not)
        |> String.concat "\n\n"

    /// Record this turn's A material, then return session-wide A.
    ///
    /// No fallback to the current turn's text. The previous version returned it
    /// whenever the accumulator read back empty, which made a port that silently
    /// dropped A indistinguishable from a Session whose first turn had just been
    /// recorded — and that is exactly the state EXEC-006's `IsValid` check exists
    /// to catch.
    let accumulateTurn (eventPort: IEventObservationPort) (turn: ReconciledTurn) =
        eventPort.RecordSessionA turn.SessionId turn.ProviderRun (turnText turn)
        render (eventPort.SessionARecords turn.SessionId)

    /// Session-wide A as optional text (empty → None).
    let fullText (eventPort: IEventObservationPort) (sessionId: SessionId) =
        match render (eventPort.SessionARecords sessionId) with
        | "" -> None
        | text -> Some text
