namespace Wanxiangshu.Mission.Manager

open Wanxiangshu.OpenCode
open Wanxiangshu.Mission.Obligation
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Strength.Persistence

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Host
open Wanxiangshu.Change
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Trace
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

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

    let private isTransferred (outcome: ReconcileProgram.TurnOutcome) (job: ManagerJobProjection) =
        match outcome with
        | ReconcileProgram.TurnInProgress -> orchestrationOwnsTurn job
        | ReconcileProgram.TurnCompleted -> orchestrationOwnsTurn job || job.ConflictDetected.IsSome
        | _ -> false

    let private completeInProgress
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (abortedSessions: HashSet<string>)
        (turn: ReconciledTurn)
        =
        task {
            match tryJob journal turn.SessionId with
            | Some job when isTransferred turn.Outcome job ->
                let! _ = TerminalReporter.complete eventPort journal abortedSessions turn
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

    let private recordFallbackSuccessIfValid
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        (terminalValid: bool)
        : Task<unit> =
        task {
            if terminalValid then
                do! recordFallbackSuccess journal turn
        }

    let private completeCompleted
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (abortedSessions: HashSet<string>)
        (turn: ReconciledTurn)
        =
        task {
            match tryJob journal turn.SessionId with
            | Some job when isTransferred turn.Outcome job ->
                let! _, terminalValid = TerminalReporter.complete eventPort journal abortedSessions turn
                do! recordFallbackSuccessIfValid journal turn terminalValid
                return HandoffOutcome.Transferred
            | _ -> return HandoffOutcome.ManagerOwnsTurn
        }

    /// If durable Orchestrator evidence already owns this Manager turn, complete the agent
    /// and report Transferred; otherwise leave ownership with the Manager.
    let completeIfTransferred
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (abortedSessions: HashSet<string>)
        (turn: ReconciledTurn)
        : Task<HandoffOutcome> =
        match turn.Outcome with
        | ReconcileProgram.TurnInProgress -> completeInProgress eventPort journal abortedSessions turn
        | ReconcileProgram.TurnCompleted -> completeCompleted eventPort journal abortedSessions turn
        | _ -> Task.FromResult HandoffOutcome.ManagerOwnsTurn
