namespace Wanxiangshu.OpenCode

open Wanxiangshu.Domain
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// GLORY-018/020: the pure decisions behind the planning → Activation handoff.
module ManagerLifecycleGate =

    /// GLORY-020: is a ManagerWorkActivation continuation claim already
    /// outstanding. Dedupes against PROMPT-005's pending claims, so a crash
    /// between claim and acceptance never licenses a second logical send.
    let hasPendingActivation (journal: AgentJournal) (sessionId: SessionId) =
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
    let shouldActivate (journal: AgentJournal option) (turn: ReconciledTurn) =
        match journal, turn.Role with
        | Some durable, Some Role.Manager ->
            let lifecycle =
                AgentProjection.tryFind turn.SessionId (AgentJournal.snapshot durable).AgentProjections
                |> Option.bind (fun session -> session.ManagerLife)
                |> Option.defaultValue ManagerLifecycleProjection.empty

            match lifecycle.CurrentLife with
            // Life opened, not yet activated, not completed.
            | Some life when life.ProtectedPrefixEnd.IsNone && not life.Completed ->
                // A legal planning terminal carries formal text; an empty or
                // XML-only terminal does not trigger Activation (GLORY-018).
                not (System.String.IsNullOrWhiteSpace(CompletedTurnClassifier.partsText turn.Parts))
                && not (hasPendingActivation durable turn.SessionId)
            | _ -> false
        | _ -> false
