namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Host
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

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
        let progressTransferred =
            match progress with
            | JobProgress.CandidateReady _
            | JobProgress.RebasedCandidateReady _
            | JobProgress.PublishClaimed _
            | JobProgress.Published _
            | JobProgress.Failed _
            | JobProgress.Abandoned -> true
            | JobProgress.ConflictPending _ ->
                match outcome with
                | ReconcileProgram.TurnCompleted -> true
                | _ -> false
            | _ -> false

        match outcome with
        | ReconcileProgram.TurnInProgress
        | ReconcileProgram.TurnCompleted -> progressTransferred
        | _ -> false

    /// If Orchestrator progress already owns this Manager turn, complete the agent
    /// and report Transferred; otherwise leave ownership with the Manager.
    let completeIfTransferred
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (abortedSessions: HashSet<string>)
        (turn: ReconciledTurn)
        : Task<HandoffOutcome> =
        task {
            match turn.Outcome with
            | ReconcileProgram.TurnInProgress ->
                match tryJobProgress journal turn.SessionId with
                | Some progress when isTransferred turn.Outcome progress ->
                    TerminalReporter.complete eventPort journal abortedSessions turn
                    |> ignore

                    return HandoffOutcome.Transferred
                | _ -> return HandoffOutcome.ManagerOwnsTurn
            | ReconcileProgram.TurnCompleted ->
                match tryJobProgress journal turn.SessionId with
                | Some progress when isTransferred turn.Outcome progress ->
                    let _, terminalValid =
                        TerminalReporter.complete eventPort journal abortedSessions turn

                    if terminalValid then
                        AgentJournal.recordDerivedFallbackSuccess journal turn.SessionId

                    return HandoffOutcome.Transferred
                | _ -> return HandoffOutcome.ManagerOwnsTurn
            | _ -> return HandoffOutcome.ManagerOwnsTurn
        }
