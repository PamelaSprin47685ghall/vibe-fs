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

/// Vocabulary: Orchestrator progress owns the turn → Manager completes and exits.
///
/// rabbit §8.4 — ManagerWorkflow must not know the Orchestrator JobProgress case
/// set. All CandidateReady / RebasedCandidateReady / PublishClaimed / Published /
/// Failed / Abandoned (and TurnCompleted ConflictPending) handoff lives here.
module ManagerJobHandoff =

    /// Result of checking whether Manager business ownership has transferred.
    [<RequireQualifiedAccess>]
    type HandoffOutcome =
        /// Orchestrator progress consumed the observation; Manager must stop.
        | Transferred
        /// Manager still owns the turn; caller continues Manager sequencing.
        | ManagerOwnsTurn

    let private tryJobProgress (journal: AgentJournal option) (sessionId: SessionId) =
        journal
        |> Option.bind (fun durable ->
            OrchestratorProjection.tryFindByManagerSession
                sessionId
                (AgentJournal.snapshot durable).AgentProjections.Orchestrator)
        |> Option.map (fun job -> job.Progress)

    /// Progress cases that mean the Orchestrator has taken the job from the Manager.
    let private isTransferred (outcome: ReconcileProgram.TurnOutcome) (progress: JobProgress) =
        match outcome, progress with
        | ReconcileProgram.TurnInProgress, JobProgress.CandidateReady _
        | ReconcileProgram.TurnCompleted, JobProgress.CandidateReady _
        | ReconcileProgram.TurnInProgress, JobProgress.RebasedCandidateReady _
        | ReconcileProgram.TurnCompleted, JobProgress.RebasedCandidateReady _
        | ReconcileProgram.TurnInProgress, JobProgress.PublishClaimed _
        | ReconcileProgram.TurnCompleted, JobProgress.PublishClaimed _
        | ReconcileProgram.TurnInProgress, JobProgress.Published _
        | ReconcileProgram.TurnCompleted, JobProgress.Published _
        | ReconcileProgram.TurnInProgress, JobProgress.Failed _
        | ReconcileProgram.TurnCompleted, JobProgress.Failed _
        | ReconcileProgram.TurnInProgress, JobProgress.Abandoned
        | ReconcileProgram.TurnCompleted, JobProgress.Abandoned
        | ReconcileProgram.TurnCompleted, JobProgress.ConflictPending _ -> true
        | _ -> false

    let private completeInProgress
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (abortedSessions: HashSet<string>)
        (turn: ReconciledTurn)
        =
        task {
            match tryJobProgress journal turn.SessionId with
            | Some progress when isTransferred turn.Outcome progress ->
                let! _ = TerminalReporter.complete eventPort journal abortedSessions turn
                return HandoffOutcome.Transferred
            | _ -> return HandoffOutcome.ManagerOwnsTurn
        }

    let private recordFallbackSuccessIfValid
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        (terminalValid: bool)
        : Task<unit> =
        task {
            if terminalValid then
                match journal with
                | Some j ->
                    let! _ = FallbackLedger.recordConfirmedSuccess j turn.SessionId turn.ProviderRun
                    ()
                | None -> ()
        }

    let private completeCompleted
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (abortedSessions: HashSet<string>)
        (turn: ReconciledTurn)
        =
        task {
            match tryJobProgress journal turn.SessionId with
            | Some progress when isTransferred turn.Outcome progress ->
                let! _, terminalValid = TerminalReporter.complete eventPort journal abortedSessions turn
                do! recordFallbackSuccessIfValid journal turn terminalValid
                return HandoffOutcome.Transferred
            | _ -> return HandoffOutcome.ManagerOwnsTurn
        }

    /// If Orchestrator progress already owns this Manager turn, complete the agent
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
