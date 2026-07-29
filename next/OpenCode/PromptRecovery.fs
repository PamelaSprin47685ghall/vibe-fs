namespace Wanxiangshu.Next.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

/// PROMPT-011: reconcile prompts the Host may have accepted before the plugin
/// crashed.
///
/// The contract is `at-most-one logical effect + fail-closed unknown outcome`. This
/// module therefore only ever PROVES acceptance or ABANDONS the claim. It never
/// resends: the Host may have accepted a message that fell outside the tail window,
/// or already started a provider run, and a resend would produce a second logical
/// effect.
module PromptRecovery =

    /// What one pending claim resolved to.
    ///
    /// `StillPending` carries whether a receipt exists. PROMPT-011 steps 4 and 5
    /// both stay pending, but they are different operator diagnoses: with a receipt
    /// the Host accepted something we cannot locate, without one the send may never
    /// have left.
    type ClaimOutcome =
        /// Step 3: a `role=user` message carries this PromptKey. `PhysicalAccepted`
        /// was written with its real id.
        | Proven of PhysicalUserMessageId
        /// Steps 4 and 5.
        | StillPending of hasReceipt: bool
        /// Budget expired. `Abandoned(UnresolvedAfterRecovery)` was written.
        | GaveUp
        /// The session snapshot could not be read, so nothing is known. Fail closed:
        /// the claim keeps its budget and is retried on the next start.
        | Unreadable of reason: string

    type Reconciled =
        { SessionId: SessionId
          PromptKey: PromptKey
          Outcome: ClaimOutcome }

    /// Step 1+2: the newest `RecoveryTailWindow` messages, searched for this key.
    ///
    /// `role=user` only. The PromptKey travels on the prompt this plugin sent, so a
    /// match on any other role would mean the Host echoed our metadata somewhere it
    /// does not belong — and treating that as physical acceptance would promote a
    /// non-user message to an Authority Root.
    let private findPhysical (key: PromptKey) (messages: SessionMessage list) =
        let window =
            let count = List.length messages

            if count <= PromptAuthority.RecoveryTailWindow then
                messages
            else
                messages |> List.skip (count - PromptAuthority.RecoveryTailWindow)

        window
        |> List.tryPick (fun message ->
            if message.Role = "user" && message.PromptKey = Some(PromptKey.value key) then
                Some(PhysicalUserMessageId.create message.Id)
            else
                None)

    /// Resolve one pending claim.
    ///
    /// The budget is checked only AFTER the search fails. Checking first would
    /// abandon a claim whose message is sitting in the transcript, turning a
    /// provable success into a permanent unknown.
    let private reconcileClaim
        (runtime: PromptDispatcher.Runtime)
        (snapshot: ISessionSnapshotPort)
        (sessionId: SessionId)
        (claim: PromptAuthority.PromptClaim)
        : Task<Reconciled> =
        task {
            let report outcome =
                { SessionId = sessionId
                  PromptKey = claim.PromptKey
                  Outcome = outcome }

            match! snapshot.GetMessages sessionId with
            | Error reason -> return report (Unreadable reason)
            | Ok messages ->
                match findPhysical claim.PromptKey messages with
                | Some physical ->
                    // The claim's own origin decides which acceptance this is: an
                    // AgentOwnerRoot claim must also register its Authority Root
                    // (PROMPT-005 order), a continuation must not.
                    let accepted =
                        match claim.Origin with
                        | PromptAuthority.PromptOrigin.AuthorityRoot _ ->
                            runtime.AcceptAgentOwnerRoot claim.PromptKey sessionId physical
                            |> Result.map ignore
                        | PromptAuthority.PromptOrigin.Continuation _
                        | PromptAuthority.PromptOrigin.HostInternal
                        | PromptAuthority.PromptOrigin.UnknownOrigin ->
                            runtime.AcceptContinuation claim.PromptKey sessionId physical
                            |> Result.map ignore

                    match accepted with
                    | Ok() -> return report (Proven physical)
                    | Error reason -> return report (Unreadable reason)

                | None when PromptAuthority.recoveryBudgetSpent claim ->
                    match runtime.Abandon claim.PromptKey sessionId PromptAbandonReason.UnresolvedAfterRecovery with
                    | Ok() -> return report GaveUp
                    | Error reason -> return report (Unreadable reason)

                | None -> return report (StillPending claim.Receipt.IsSome)
        }

    /// Reconcile every pending claim once, at plugin start.
    ///
    /// Enumerating sessions is legitimate here and only here: PERSIST-008 bounds
    /// per-query lookups, and this is a single startup pass over sessions that hold
    /// state. Every claim it finds is either resolved or abandoned within
    /// `RecoveryAttemptBudget` starts, so the set it walks cannot grow without bound.
    let reconcile (journal: AgentJournal option) (snapshotOpt: ISessionSnapshotPort option) : Task<Reconciled list> =
        task {
            match journal, snapshotOpt with
            // No journal means no durable claim to reconcile. No snapshot port means
            // no way to prove acceptance, and PROMPT-011 forbids resending, so there
            // is nothing this pass could legitimately do.
            | None, _
            | _, None -> return []
            | Some durable, Some snapshot ->
                let runtime = PromptDispatcher.forJournal durable

                let pending =
                    (AgentJournal.snapshot durable).AgentProjections.Sessions
                    |> Map.toList
                    |> List.collect (fun (sessionId, session) ->
                        session.PromptAuthority
                        |> Option.map (fun authority ->
                            authority.PendingClaims
                            |> Map.toList
                            |> List.map (fun (_, claim) -> sessionId, claim))
                        |> Option.defaultValue [])

                let results = ResizeArray<Reconciled>()

                for sessionId, claim in pending do
                    let! outcome = reconcileClaim runtime snapshot sessionId claim
                    results.Add outcome

                return results |> Seq.toList
        }
