namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open Wanxiangshu.Domain
open Wanxiangshu.Host
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// Physical terminal materialisation: ReconciledTurn → AgentRunResult →
/// XTrace capture → NotifyTerminal. No Fallback / Manager / Reviewer /
/// cohort lifecycle / JoinGuard / IdleRepair / LoopSensor.
module TerminalReporter =

    /// Build the `AgentRunResult`, validate via `runResult.IsValid`, capture the
    /// XTrace terminal segment, and report Completed / Failed. Also clears the
    /// session's abort bookkeeping flag for the caller.
    let complete
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (abortedSessions: HashSet<string>)
        (turn: ReconciledTurn)
        =
        let sessionKey = SessionId.value turn.SessionId
        let wasAborted = abortedSessions.Contains sessionKey
        abortedSessions.Remove sessionKey |> ignore
        let sessionWideText = CompletedTurnClassifier.partsSessionText turn.Parts

        let terminalValid =
            match turn.Role with
            | None ->
                eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed "completed with no resolved role")
                |> ignore

                false
            | Some role ->
                let runResult: AgentRunResult =
                    { SessionId = turn.SessionId
                      AuthorityRootUserMessageId = turn.AuthorityRootUserMessageId
                      ProviderRun = turn.ProviderRun
                      Role = AgentRoleIdentity.toRole role
                      Directory = turn.Directory
                      TerminalText = sessionWideText
                      TurnFormalText = CompletedTurnClassifier.partsText turn.Parts }

                if runResult.IsValid then
                    XTraceCapture.captureTerminal journal turn

                    eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Completed runResult)
                    |> ignore

                    true
                else
                    eventPort.NotifyTerminal
                        turn.SessionId
                        (TerminalOutcome.Failed "completed with empty terminal output")
                    |> ignore

                    false

        wasAborted, terminalValid
