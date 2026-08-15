namespace Wanxiangshu.Interaction.Dispatch

open Wanxiangshu.Composition.Durable

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
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
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
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
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
                    let! accepted =
                        match claim.Origin with
                        | PromptAuthority.PromptOrigin.AuthorityRoot _ ->
                            task {
                                match! runtime.AcceptAgentOwnerRoot claim.PromptKey sessionId physical with
                                | Ok _ -> return Ok()
                                | Error reason -> return Error reason
                            }
                        | PromptAuthority.PromptOrigin.Continuation _
                        | PromptAuthority.PromptOrigin.HostInternal
                        | PromptAuthority.PromptOrigin.UnknownOrigin ->
                            task {
                                match! runtime.AcceptContinuation claim.PromptKey sessionId physical with
                                | Ok _ -> return Ok()
                                | Error reason -> return Error reason
                            }

                    match accepted with
                    | Ok() -> return report (Proven physical)
                    | Error reason -> return report (Unreadable reason)

                | None -> return report (StillPending claim.Receipt.IsSome)
        }

    /// Detached reconciliation library: prove a pending claim from physical Host
    /// evidence, or leave it pending. Ordinary plugin lifecycle never calls this.
    /// A process restart is not authority to abandon an unresolved old tool.
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
                let projections = (AgentJournal.snapshot durable).AgentProjections

                let pending =
                    projections.Sessions
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
