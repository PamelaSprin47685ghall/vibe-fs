namespace Wanxiangshu.Context.Trace

open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.OpenCode

[<RequireQualifiedAccess>]
type XTraceTerminalCompletion =
    | Published of AgentRunResult
    | CaptureFailed of XTraceCaptureError
    | RejectedMissingRole
    | RejectedEmptyOutput

/// Physical terminal materialisation: ReconciledTurn → AgentRunResult →
/// XTrace capture → NotifyTerminal. No Fallback / Manager /
/// lifecycle / JoinGuard / IdleRepair / LoopSensor.
module TerminalReporter =

    /// Runs the durable-terminal capture through the port, or yields the deterministic
    /// empty receipt when no port is provided (test/dev harness without journal).
    let private runCapture
        (port: TerminalTracePort option)
        (turn: ReconciledTurn)
        (sessionWideText: string)
        : Task<Result<XTraceCaptureReceipt, XTraceCaptureError>> =
        match port with
        | Some tracePort -> tracePort.CaptureTerminalText turn.SessionId sessionWideText turn.ProviderRun
        | None ->
            Task.FromResult(
                Ok
                    { PreviousHead = XTraceCursor.originCursor
                      CurrentHead = XTraceCursor.originCursor
                      CapturedPartCount = 0
                      OpeningCaptured = false
                      TerminalCaptured = false
                      Identity = XTraceCaptureIdentity.NoDurableTrace }
            )

    let private notifyTerminalCompletion
        (eventPort: IEventObservationPort)
        (port: TerminalTracePort option)
        (turn: ReconciledTurn)
        (sessionWideText: string)
        (runResult: AgentRunResult)
        : Task<XTraceTerminalCompletion> =
        task {
            let! captureResult = runCapture port turn sessionWideText

            match captureResult with
            | Ok _ ->
                eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Completed runResult)
                |> ignore

                return XTraceTerminalCompletion.Published runResult
            | Error error -> return XTraceTerminalCompletion.CaptureFailed error
        }

    let private reportResolvedRole
        (eventPort: IEventObservationPort)
        (port: TerminalTracePort option)
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
                return! notifyTerminalCompletion eventPort port turn sessionWideText runResult
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
        (port: TerminalTracePort option)
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
            | Some role -> return! reportResolvedRole eventPort port turn sessionWideText role
        }

    let completeWithEvidence
        (eventPort: IEventObservationPort)
        (port: TerminalTracePort option)
        (turn: ReconciledTurn)
        : Task<XTraceTerminalCompletion> =
        let sessionWideText = CompletedTurnClassifier.partsSessionText turn.Parts
        completeUsingTextEvidence eventPort port turn sessionWideText

    /// Legacy workflow result shape while foreign callers migrate. All terminal
    /// decisions and effects are owned by the typed operation above.
    let complete
        (eventPort: IEventObservationPort)
        (port: TerminalTracePort option)
        (turn: ReconciledTurn)
        : Task<bool * bool> =
        task {
            let! completion = completeWithEvidence eventPort port turn

            return
                match completion with
                | XTraceTerminalCompletion.Published _ -> false, true
                | XTraceTerminalCompletion.CaptureFailed _
                | XTraceTerminalCompletion.RejectedMissingRole
                | XTraceTerminalCompletion.RejectedEmptyOutput -> false, false
        }
