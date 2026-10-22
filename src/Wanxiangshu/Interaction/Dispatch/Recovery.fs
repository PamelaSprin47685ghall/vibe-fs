namespace Wanxiangshu.Interaction.Dispatch

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Host.Ports
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Composition.Durable

/// PROMPT-011: reconcile prompts the Host may have accepted before the plugin
/// crashed.
///
/// The contract is `at-most-one logical effect + visible unknown outcome`. This
/// detached library only PROVES acceptance when physical evidence exists; otherwise
/// it leaves the old claim pending. It never resends or invents a crash terminal.
module PromptRecovery =

    /// The neutral transcript error channel rendered back into the reason text
    /// the recovery outcome reports. The wording is the one the Host adapter
    /// surfaces already use: a transport or refusal reason is passed through
    /// verbatim, so the operator sees the host's own text.
    let private hostErrorReason (error: HostError) =
        match error with
        | HostError.Unsupported capability -> sprintf "Host capability %A is unsupported" capability
        | HostError.NotFound what -> what
        | HostError.Refused reason -> reason
        | HostError.Transport reason -> reason
        | HostError.Unknown reason -> reason

    /// The neutral role rendered as the wire role the claim law matches on.
    /// Only a user message proves a landing, so the spelling must stay exact.
    let private roleText (role: TranscriptRole) =
        match role with
        | TranscriptRole.User -> "user"
        | TranscriptRole.Assistant -> "assistant"
        | TranscriptRole.System -> "system"

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
        (messages: TranscriptMessage list)
        report
        =
        let verdict =
            DispatchLaw.recover
                (PromptKey.value claim.PromptKey)
                claim.Receipt.IsSome
                (messages
                 |> List.map (fun message ->
                        roleText message.Role,
                        message.Correlation |> Option.map PromptKey.value,
                        TranscriptMessageAddress.value message.Id))

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
        (transcript: ITranscriptPort)
        (sessionId: SessionId)
        (claim: PromptAuthority.PromptClaim)
        : Task<Reconciled> =
        task {
            let report outcome =
                { SessionId = sessionId
                  PromptKey = claim.PromptKey
                  Outcome = outcome }

            match! transcript.Messages sessionId with
            | Error error -> return report (Unreadable (hostErrorReason error))
            | Ok messages -> return! reconcileMessages runtime claim sessionId messages report
        }

    let private reconcileUnsettledClaims runtime transcript unsettledClaims =
        task {
            // DSL-MUTABLE: algorithm-scratch — reconciled result accumulator
            let results = ResizeArray<Reconciled>()

            for sessionId, claim in unsettledClaims do
                let! outcome = reconcileClaim runtime transcript sessionId claim
                results.Add outcome

            return results |> Seq.toList
        }

    /// Detached reconciliation library: prove a pending claim from physical Host
    /// evidence, or leave it pending. Ordinary plugin lifecycle never calls this.
    /// A process restart is not authority to abandon an unresolved old tool.
    let reconcile (journal: AgentJournal option) (transcriptOpt: ITranscriptPort option) : Task<Reconciled list> =
        match journal, transcriptOpt with
        // No journal means no durable claim to reconcile. No transcript port
        // means no way to prove acceptance, and PROMPT-011 forbids resending, so
        // there is nothing this pass could legitimately do.
        | None, _
        | _, None -> Task.FromResult []
        | Some durable, Some transcript ->
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

            reconcileUnsettledClaims runtime transcript unsettled
