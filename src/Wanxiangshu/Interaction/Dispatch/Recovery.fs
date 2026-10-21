namespace Wanxiangshu.Interaction.Dispatch

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Foundation.HostSession
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Composition.Durable

/// PROMPT-011: reconcile prompts the Host may have accepted before the plugin
/// crashed.
///
/// The contract is `at-most-one logical effect + visible unknown outcome`. This
/// detached library only PROVES acceptance when physical evidence exists; otherwise
/// it leaves the old claim pending. It never resends or invents a crash terminal.
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
        /// The session snapshot could not be read, so nothing is known.
        | Unreadable of reason: string

    type Reconciled =
        { SessionId: SessionId
          PromptKey: PromptKey
          Outcome: ClaimOutcome }

    let private acceptClaimOrigin
        (runtime: PromptDispatcher.Runtime)
        (claim: PromptAuthority.PromptClaim)
        sessionId
        physical
        =
        match claim.Origin with
        | PromptAuthority.PromptOrigin.AuthorityRoot _ ->
            runtime.AcceptAgentOwnerRoot claim.PromptKey sessionId physical
            |> TaskValue.map (Result.map ignore)
        | PromptAuthority.PromptOrigin.Continuation _
        | PromptAuthority.PromptOrigin.HostInternal
        | PromptAuthority.PromptOrigin.UnknownOrigin ->
            runtime.AcceptContinuation claim.PromptKey sessionId physical
            |> TaskValue.map (Result.map ignore)

    let private reconcileWithPhysical
        (runtime: PromptDispatcher.Runtime)
        (claim: PromptAuthority.PromptClaim)
        sessionId
        physical
        report
        =
        task {
            let! accepted = acceptClaimOrigin runtime claim sessionId physical

            match accepted with
            | Ok() -> return report (Proven physical)
            | Error reason -> return report (Unreadable reason)
        }

    /// Steps 1-5 as `wanxiangli/dispatch` recover decides them: only a
    /// `role=user` message carrying exactly this PromptKey among the newest
    /// `RecoveryTailWindow` messages proves the landing (any other role would
    /// mean the Host echoed our metadata where it does not belong). Without proof
    /// the claim stays pending, with or without a receipt; nothing is resent.
    let private reconcileMessages
        (runtime: PromptDispatcher.Runtime)
        (claim: PromptAuthority.PromptClaim)
        sessionId
        (messages: SessionMessage list)
        report
        =
        let verdict =
            DispatchLaw.recover
                (PromptKey.value claim.PromptKey)
                claim.Receipt.IsSome
                (messages
                 |> List.map (fun message -> message.Role, message.PromptKey, message.Id))

        match verdict with
        | Ok physical -> reconcileWithPhysical runtime claim sessionId (PhysicalUserMessageId.create physical) report
        | Error hasReceipt -> Task.FromResult(report (StillPending hasReceipt))

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
            | Ok messages -> return! reconcileMessages runtime claim sessionId messages report
        }

    let private reconcileUnsettledClaims runtime snapshot unsettledClaims =
        task {
            // DSL-MUTABLE: algorithm-scratch — reconciled result accumulator
            let results = ResizeArray<Reconciled>()

            for sessionId, claim in unsettledClaims do
                let! outcome = reconcileClaim runtime snapshot sessionId claim
                results.Add outcome

            return results |> Seq.toList
        }

    /// Detached reconciliation library: prove a pending claim from physical Host
    /// evidence, or leave it pending. Ordinary plugin lifecycle never calls this.
    /// A process restart is not authority to abandon an unresolved old tool.
    let reconcile (journal: AgentJournal option) (snapshotOpt: ISessionSnapshotPort option) : Task<Reconciled list> =
        match journal, snapshotOpt with
        // No journal means no durable claim to reconcile. No snapshot port means
        // no way to prove acceptance, and PROMPT-011 forbids resending, so there
        // is nothing this pass could legitimately do.
        | None, _
        | _, None -> Task.FromResult []
        | Some durable, Some snapshot ->
            let runtime = PromptDispatcher.forPrompts (PromptJournalAdapter.create durable)
            let projections = (AgentJournal.snapshot durable).AgentProjections

            let unsettled =
                projections.Sessions
                |> Map.toList
                |> List.collect (fun (sessionId, session) ->
                    session.PromptAuthority
                    |> Option.map (fun authority ->
                        authority.PendingClaims
                        |> Map.toList
                        |> List.map (fun (_, claim) -> sessionId, claim))
                    |> Option.defaultValue [])

            reconcileUnsettledClaims runtime snapshot unsettled
