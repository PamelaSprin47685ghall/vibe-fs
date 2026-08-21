namespace Wanxiangshu.Context.Trace

open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Persistence.Journal

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
        : Task<bool * bool> =
        task {
            let runResult: AgentRunResult =
                { SessionId = turn.SessionId
                  AuthorityRootUserMessageId = turn.AuthorityRootUserMessageId
                  ProviderRun = turn.ProviderRun
                  Role = AgentRoleIdentity.toRole role
                  Directory = turn.Directory
                  TerminalText = sessionWideText
                  TurnFormalText = CompletedTurnClassifier.partsText turn.Parts }

            if runResult.IsValid then
                do! XTraceCapture.captureTerminal journal turn

                eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Completed runResult)
                |> ignore

                return false, true
            else
                eventPort.NotifyTerminal
                    turn.SessionId
                    (TerminalOutcome.Failed(
                        TerminalStop.forAuthority turn.AuthorityRootUserMessageId "completed with empty terminal output"
                    ))
                |> ignore

                return false, false
        }

    /// Build the `AgentRunResult`, validate via `runResult.IsValid`, capture the
    /// XTrace terminal segment, and report Completed / Failed.
    let complete
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        : Task<bool * bool> =
        task {
            let sessionWideText = CompletedTurnClassifier.partsSessionText turn.Parts

            match turn.Role with
            | None ->
                eventPort.NotifyTerminal
                    turn.SessionId
                    (TerminalOutcome.Failed(
                        TerminalStop.forAuthority turn.AuthorityRootUserMessageId "completed with no resolved role"
                    ))
                |> ignore

                return false, false
            | Some role -> return! reportResolvedRole eventPort journal turn sessionWideText role
        }
