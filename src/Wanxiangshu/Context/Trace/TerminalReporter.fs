namespace Wanxiangshu.Context.Trace

open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
type XTraceTerminalCompletion =
    | Published of AgentRunResult
    | CaptureFailed of XTraceCaptureError
    | RejectedMissingRole
    | RejectedEmptyOutput

/// Physical terminal materialisation: ReconciledTurn → AgentRunResult →
/// XTrace capture → NotifyTerminal. No Fallback / Manager / Reviewer /
/// cohort lifecycle / JoinGuard / IdleRepair / LoopSensor.
module TerminalReporter =

    let private reportResolvedRole
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        (sessionWideText: string)
        (role: Role)
        : Task<XTraceTerminalCompletion> =
        task {
            let runResult: AgentRunResult =
                { SessionId = turn.SessionId
                  AuthorityRootUserMessageId = turn.AuthorityRootUserMessageId
                  ProviderRun = turn.ProviderRun
                  Role = role
                  Directory = turn.Directory
                  TerminalText = sessionWideText
                  TurnFormalText = CompletedTurnClassifier.partsText turn.Parts }

            if runResult.IsValid then
                match!
                    XTraceCapture.captureTerminalTextWithReceipt journal turn.SessionId sessionWideText turn.ProviderRun
                with
                | Ok _ ->
                    eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Completed runResult)
                    |> ignore

                    return XTraceTerminalCompletion.Published runResult
                | Error error -> return XTraceTerminalCompletion.CaptureFailed error
            else
                eventPort.NotifyTerminal
                    turn.SessionId
                    (TerminalOutcome.Failed(
                        TerminalStop.forAuthority turn.AuthorityRootUserMessageId "completed with empty terminal output"
                    ))
                |> ignore

                return XTraceTerminalCompletion.RejectedEmptyOutput
        }

    /// Build the `AgentRunResult`, validate via `runResult.IsValid`, capture the
    /// XTrace terminal segment, and report Completed / Failed.
    let completeUsingTextEvidence
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        (sessionWideText: string)
        : Task<XTraceTerminalCompletion> =
        task {
            match turn.Role with
            | None ->
                eventPort.NotifyTerminal
                    turn.SessionId
                    (TerminalOutcome.Failed(
                        TerminalStop.forAuthority turn.AuthorityRootUserMessageId "completed with no resolved role"
                    ))
                |> ignore

                return XTraceTerminalCompletion.RejectedMissingRole
            | Some role -> return! reportResolvedRole eventPort journal turn sessionWideText role
        }

    let completeWithEvidence
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        : Task<XTraceTerminalCompletion> =
        let sessionWideText = CompletedTurnClassifier.partsSessionText turn.Parts
        completeUsingTextEvidence eventPort journal turn sessionWideText

    /// Legacy workflow result shape while foreign callers migrate. All terminal
    /// decisions and effects are owned by the typed operation above.
    let complete
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        : Task<bool * bool> =
        task {
            let! completion = completeWithEvidence eventPort journal turn

            return
                match completion with
                | XTraceTerminalCompletion.Published _ -> false, true
                | XTraceTerminalCompletion.CaptureFailed _
                | XTraceTerminalCompletion.RejectedMissingRole
                | XTraceTerminalCompletion.RejectedEmptyOutput -> false, false
        }
