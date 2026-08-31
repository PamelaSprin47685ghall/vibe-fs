namespace Wanxiangshu.Mission.Manager

open System.Threading.Tasks
open Wanxiangshu.Change
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Persistence.Journal

/// Vocabulary: durable Orchestrator evidence owns the turn → Manager completes and exits.
module ManagerJobHandoff =

    /// Result of checking whether Manager business ownership has transferred.
    [<RequireQualifiedAccess>]
    type HandoffOutcome =
        /// Orchestrator durable evidence consumed the observation; Manager must stop.
        | Transferred
        /// Manager still owns the turn; caller continues Manager sequencing.
        | ManagerOwnsTurn

    let private tryJob (journal: AgentJournal option) (sessionId: SessionId) =
        journal
        |> Option.bind (fun durable ->
            OrchestratorProjection.tryFindByManagerSession
                sessionId
                (AgentJournal.snapshot durable).AgentProjections.Orchestrator)

    let private orchestrationOwnsTurn (job: ManagerJobProjection) =
        job.CandidateReady.IsSome
        || job.RebasedCandidateReady.IsSome
        || job.PublishClaimed.IsSome
        || job.Terminal.IsSome

    let private managerLifeArchived (journal: AgentJournal option) (sessionId: SessionId) =
        journal
        |> Option.bind (fun durable ->
            AgentProjection.tryFind sessionId (AgentJournal.snapshot durable).AgentProjections)
        |> Option.bind (fun session -> session.ManagerLife)
        |> Option.exists ManagerLifecycleProjection.isLifeArchived

    let private isTransferred
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (outcome: ReconcileProgram.TurnOutcome)
        (job: ManagerJobProjection)
        =
        match outcome with
        | ReconcileProgram.TurnInProgress -> orchestrationOwnsTurn job
        | ReconcileProgram.TurnCompleted ->
            orchestrationOwnsTurn job
            || job.ConflictDetected.IsSome
            || managerLifeArchived journal sessionId
        | _ -> false

    let private completeInProgress
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        =
        task {
            match tryJob journal turn.SessionId with
            | Some job when isTransferred journal turn.SessionId turn.Outcome job ->
                let! _ = TerminalReporter.completeWithEvidence eventPort journal turn
                return HandoffOutcome.Transferred
            | _ -> return HandoffOutcome.ManagerOwnsTurn
        }

    let private recordFallbackSuccess (journal: AgentJournal option) (turn: ReconciledTurn) : Task<unit> =
        task {
            match journal with
            | Some j ->
                let! _ = FallbackLedger.recordConfirmedSuccess j turn.SessionId turn.ProviderRun
                ()
            | None -> ()
        }

    let private recordFallbackSuccessAfterCompletion
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        (completion: XTraceTerminalCompletion)
        : Task<unit> =
        task {
            match completion with
            | XTraceTerminalCompletion.Published _ -> do! recordFallbackSuccess journal turn
            | XTraceTerminalCompletion.CaptureFailed _
            | XTraceTerminalCompletion.RejectedMissingRole
            | XTraceTerminalCompletion.RejectedEmptyOutput -> ()
        }

    let private completeCompleted
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        =
        task {
            match tryJob journal turn.SessionId with
            | Some job when isTransferred journal turn.SessionId turn.Outcome job ->
                let! completion = TerminalReporter.completeWithEvidence eventPort journal turn
                do! recordFallbackSuccessAfterCompletion journal turn completion
                return HandoffOutcome.Transferred
            | _ -> return HandoffOutcome.ManagerOwnsTurn
        }

    /// If durable Orchestrator evidence already owns this Manager turn, complete the agent
    /// and report Transferred; otherwise leave ownership with the Manager.
    let completeIfTransferred
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        : Task<HandoffOutcome> =
        match turn.Outcome with
        | ReconcileProgram.TurnInProgress -> completeInProgress eventPort journal turn
        | ReconcileProgram.TurnCompleted -> completeCompleted eventPort journal turn
        | _ -> Task.FromResult HandoffOutcome.ManagerOwnsTurn
