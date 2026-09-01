namespace Wanxiangshu.Interaction.Dispatch

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

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

    let private reconcileMessages
        (runtime: PromptDispatcher.Runtime)
        (claim: PromptAuthority.PromptClaim)
        sessionId
        messages
        report
        =
        match findPhysical claim.PromptKey messages with
        | Some physical -> reconcileWithPhysical runtime claim sessionId physical report
        | None -> Task.FromResult(report (StillPending claim.Receipt.IsSome))

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
            let runtime = PromptDispatcher.forJournal durable
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
