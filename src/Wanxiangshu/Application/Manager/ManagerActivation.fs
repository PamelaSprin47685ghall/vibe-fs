namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Host
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// Vocabulary: planning → Activation handoff (exactly-once).
///
/// GLORY-018/020 + rabbit §8.1 — if planning completed legally and Activation
/// is not yet established, complete exactly-once activation; otherwise
/// acknowledge the existing activation / no-op.
module ManagerActivation =

    /// Result of ensuring Activation for a reconciled Manager turn.
    [<RequireQualifiedAccess>]
    type EnsureAcceptedResult =
        /// Activation was sent this observation, or Activation is not yet
        /// warranted / no current Life — caller should stop here.
        | Deferred
        /// Activation already established (or Life present without a fresh
        /// activation send); caller may proceed to idle/labor.
        | Ready of LifeProjection

    let private currentLife (journal: AgentJournal option) (sessionId: SessionId) =
        journal
        |> Option.bind (fun durable ->
            AgentProjection.tryFind sessionId (AgentJournal.snapshot durable).AgentProjections)
        |> Option.bind (fun session -> session.ManagerLife)
        |> Option.bind (fun lifecycle -> lifecycle.CurrentLife)

    /// GLORY-020: is a ManagerWorkActivation continuation claim already
    /// outstanding. Dedupes against PROMPT-005's pending claims, so a crash
    /// between claim and acceptance never licenses a second logical send.
    let private hasPendingActivation (journal: AgentJournal) (sessionId: SessionId) =
        AgentProjection.tryFind sessionId (AgentJournal.snapshot journal).AgentProjections
        |> Option.bind (fun session -> session.PromptAuthority)
        |> Option.exists (fun authority ->
            authority.PendingClaims
            |> Map.exists (fun _ claim ->
                match claim.Origin with
                | PromptAuthority.PromptOrigin.Continuation PromptAuthority.ContinuationKind.ManagerWorkActivation ->
                    true
                | _ -> false))

    /// GLORY-018: every condition for sending the Activation continuation.
    ///
    /// `turn` must already be a reconciled `TurnCompleted`; this function checks
    /// the Life state, the terminal's formal text, and the absence of a pending
    /// activation claim. Provider failure / abort / repair / compaction turns
    /// never reach here (they are not TurnCompleted).
    let private shouldActivate (journal: AgentJournal option) (turn: ReconciledTurn) =
        match journal, turn.Role with
        | Some durable, Some Role.Manager ->
            let lifecycle =
                AgentProjection.tryFind turn.SessionId (AgentJournal.snapshot durable).AgentProjections
                |> Option.bind (fun session -> session.ManagerLife)
                |> Option.defaultValue ManagerLifecycleProjection.empty

            match lifecycle.CurrentLife with
            // Life opened, not yet activated, not completed.
            | Some life when
                life.ProtectedPrefixEnd.IsNone
                && not life.Completed
                && turn.Outcome = ReconcileProgram.TurnCompleted
                ->
                // A legal planning terminal carries formal text; an empty or
                // XML-only terminal does not trigger Activation (GLORY-018).
                not (System.String.IsNullOrWhiteSpace(CompletedTurnClassifier.partsText turn.Parts))
                && not (hasPendingActivation durable turn.SessionId)
            | _ -> false
        | _ -> false

    let private sendActivation
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        =
        task {
            match!
                HostSessionNudge.sendContinuationResult
                    sessionPort
                    turn.SessionId
                    ManagerLifecyclePrompt.WorkActivation
                    PromptAuthority.ContinuationKind.ManagerWorkActivation
                    turn.Directory
                    journal
                    PromptDispatcher.AwaitMode.Detached
                    None
            with
            | Error error -> eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed error) |> ignore
            | Ok _ -> ()
        }
        :> Task

    /// If planning completed legally and Activation is not established,
    /// complete exactly-once activation; otherwise return ready Life / defer.
    let ensureAccepted
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        : Task<EnsureAcceptedResult> =
        task {
            if shouldActivate journal turn then
                do! sendActivation sessionPort eventPort journal turn
                return EnsureAcceptedResult.Deferred
            else
                match currentLife journal turn.SessionId with
                | Some life -> return EnsureAcceptedResult.Ready life
                | None -> return EnsureAcceptedResult.Deferred
        }
