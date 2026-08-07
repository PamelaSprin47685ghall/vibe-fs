namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Host
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Outcome
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact

/// PROMPT-005's four-fact send protocol, in one place.
///
/// Claimed → Submitted → PhysicalAccepted, or Claimed → Abandoned. Both members
/// below return the `PromptKey` rather than a message id: at send time no
/// physical message exists yet, and the key is what the caller can later use to
/// recognise the message when `chat.message` delivers it (PROMPT-011).
[<AutoOpen>]
module PromptDispatcherSend =

    /// PROMPT-011: the key is derived, never generated.
    ///
    /// Every input comes from the journal fold or the payload, so the same logical
    /// dispatch produces the same key on any process — which is the only reason
    /// recovery can match a Host message back to a pending claim.
    let private deriveKey
        (projection: PromptAuthority.PromptAuthorityProjection)
        (sessionId: SessionId)
        (logicalRunId: LogicalRunId option)
        (authorityRoot: AuthorityRootUserMessageId option)
        (origin: PromptAuthority.PromptOrigin)
        (effectiveAgent: string option)
        (payloadDigest: string)
        : PromptKey =
        PromptAuthority.claimScopeDigest sessionId logicalRunId origin payloadDigest
        |> fun scope -> PromptAuthority.nextClaimSequence scope projection
        |> PromptAuthority.derivePromptKey
            HostDigest.sha256Hex
            sessionId
            logicalRunId
            authorityRoot
            origin
            effectiveAgent
            payloadDigest

    type PromptDispatcher.Runtime with

        /// Record the Host's answer and report what the caller may conclude.
        ///
        /// `AcceptanceUnknown` deliberately writes nothing. PROMPT-011 keeps such a
        /// key Pending so recovery can look for the physical message later;
        /// abandoning it here would license a resend, and resending is exactly how
        /// one logical prompt becomes two physical ones.
        member private this.RecordSendOutcome
            (key: PromptKey)
            (sessionId: SessionId)
            (outcome: SendOutcome)
            (awaitMode: PromptDispatcher.AwaitMode)
            (onAccepted: (PhysicalUserMessageId -> unit) option)
            (acceptPhysical: PhysicalUserMessageId -> Result<unit, string>)
            : Result<PromptKey, string> =
            // PROMPT-007: Detached never observes PhysicalAccepted at the caller.
            let acceptanceCallback =
                match awaitMode with
                | PromptDispatcher.AwaitMode.Detached -> None
                | PromptDispatcher.AwaitMode.Await -> onAccepted

            let submitted (receipt: TransportReceipt) =
                PromptFact.PluginPromptSubmitted
                    {| PromptKey = key
                       SessionId = sessionId
                       Receipt = receipt |}
                |> this.Persist sessionId None

            // `Abandoned` is written by `Runtime.Abandon` (PROMPT-005 single writer).
            // Constructing the fact here as well would make PROMPT-011's recovery a
            // second writer of the same fact with its own copy of the payload shape.
            let abandon (reason: PromptAbandonReason) (error: string) =
                this.Abandon key sessionId reason |> Result.bind (fun () -> Error error)

            match outcome with
            | AdmittedWithReceipt receipt ->
                // PROMPT-005: an `accepted-*` receipt is not a message identity, so
                // the chain stops at Submitted. `chat.message` supplies the physical
                // id later and PromptIngress writes PhysicalAccepted then.
                // PROMPT-007 Detached: this is already a complete success for the caller.
                submitted receipt |> Result.map (fun () -> key)

            | AdmittedWithPhysicalMessage physicalId ->
                // The Host answered with a real id. That answer is still the
                // transport receipt — it is simply not admission-shaped — so the
                // four-stage chain stays intact instead of skipping Submitted.
                submitted (TransportReceipt.create (PhysicalUserMessageId.value physicalId))
                |> Result.bind (fun () -> acceptPhysical physicalId)
                |> Result.map (fun () ->
                    acceptanceCallback |> Option.iter (fun callback -> callback physicalId)
                    key)

            | Retryable error -> abandon (PromptAbandonReason.SendFailed error) error
            | Fatal error -> abandon (PromptAbandonReason.SendFailed error) error

            | AcceptanceUnknown reason ->
                Error(sprintf "Acceptance unknown for PromptKey %s: %s" (PromptKey.value key) reason)

        /// PROMPT-002: a plugin-owned Authority Root.
        ///
        /// The root's LogicalRunId and AuthorityRootUserMessageId are both `None` in
        /// the key derivation because neither exists yet — this send is what creates
        /// them. Substituting empty strings would make "no run yet" and "a run named
        /// empty" derive the same key.
        member this.SendAgentOwnerRoot
            (port: ISessionHostPort)
            (sessionId: SessionId)
            (text: string)
            (agent: string)
            (directory: string option)
            (awaitMode: PromptDispatcher.AwaitMode)
            (onAccepted: (PhysicalUserMessageId -> unit) option)
            : Task<Result<PromptKey, string>> =
            task {
                let payloadDigest = HostDigest.sha256Hex text

                let origin =
                    PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.AgentOwnerRoot

                let key =
                    deriveKey (this.ProjectionFor sessionId) sessionId None None origin (Some agent) payloadDigest

                match PromptAuthorityRun.claimAgentOwnerRoot key sessionId payloadDigest agent with
                | Error error -> return Error error
                | Ok claim ->
                    let claimed =
                        PromptFact.PluginPromptClaimed
                            {| PromptKey = key
                               SessionId = sessionId
                               ContinuationKind = PromptDispatcher.originLabel origin
                               LogicalRunId = None
                               AuthorityRootUserMessageId = None
                               EffectiveAgent = claim.EffectiveAgent
                               PayloadDigest = payloadDigest |}

                    match this.Persist sessionId None claimed with
                    | Error error -> return Error error
                    | Ok() ->
                        // EXEC-003: the terminal listener must exist before the prompt
                        // does, or a fast completion has nobody to deliver to.
                        use _listener = this.SubscribeNoOp port sessionId

                        let options =
                            { Model = None
                              Agent = Some agent
                              Directory = directory
                              Metadata = Some(this.Metadata key (PromptDispatcher.originLabel origin) None) }

                        let! outcome = port.SendPrompt(sessionId, text, options)

                        return
                            this.RecordSendOutcome key sessionId outcome awaitMode onAccepted (fun physicalId ->
                                this.AcceptPhysicalAgentOwnerRoot key sessionId physicalId agent
                                |> Result.map ignore)
            }

        /// PROMPT-003: a continuation of an existing Logical Run.
        ///
        /// Inherits the run and root from the profile, so its key derivation has
        /// both. `effectiveAgent` is the fallback cursor's current choice
        /// (FALLBACK-004) and participates in the key: the same text retried on the
        /// other side of the pair is a different logical act.
        ///
        /// `payloadDigest` is a parameter rather than `sha256 text` computed here,
        /// because FALLBACK-008 needs one continuation kind to digest something
        /// other than its text. See `SendInteractionRepair`.
        member private this.SendContinuationWithDigest
            (port: ISessionHostPort)
            (sessionId: SessionId)
            (text: string)
            (payloadDigest: string)
            (continuation: PromptAuthority.ContinuationKind)
            (profile: PromptAuthority.AuthorityExecutionProfile)
            (effectiveAgent: string)
            (directory: string option)
            (awaitMode: PromptDispatcher.AwaitMode)
            (onAccepted: (PhysicalUserMessageId -> unit) option)
            : Task<Result<PromptKey, string>> =
            task {
                let origin = PromptAuthority.PromptOrigin.Continuation continuation
                let originLabel = PromptDispatcher.originLabel origin

                let key =
                    deriveKey
                        (this.ProjectionFor sessionId)
                        sessionId
                        (Some profile.LogicalRunId)
                        (Some profile.AuthorityRootUserMessageId)
                        origin
                        (Some effectiveAgent)
                        payloadDigest

                let claim =
                    PromptAuthorityRun.claimContinuation key sessionId continuation profile effectiveAgent payloadDigest

                let claimed =
                    PromptFact.PluginPromptClaimed
                        {| PromptKey = key
                           SessionId = sessionId
                           ContinuationKind = originLabel
                           LogicalRunId = claim.LogicalRunId
                           AuthorityRootUserMessageId = claim.AuthorityRootUserMessageId
                           EffectiveAgent = claim.EffectiveAgent
                           PayloadDigest = payloadDigest |}

                match this.Persist sessionId None claimed with
                | Error error -> return Error error
                | Ok() ->
                    use _listener = this.SubscribeNoOp port sessionId

                    let options =
                        { Model = None
                          Agent = Some effectiveAgent
                          Directory = directory
                          Metadata = Some(this.Metadata key originLabel (Some profile.LogicalRunId)) }

                    let! outcome = port.SendPrompt(sessionId, text, options)

                    return
                        this.RecordSendOutcome key sessionId outcome awaitMode onAccepted (fun physicalId ->
                            this.AcceptContinuation key sessionId physicalId |> Result.map ignore)
            }

        member this.SendContinuation
            (port: ISessionHostPort)
            (sessionId: SessionId)
            (text: string)
            (continuation: PromptAuthority.ContinuationKind)
            (profile: PromptAuthority.AuthorityExecutionProfile)
            (effectiveAgent: string)
            (directory: string option)
            (awaitMode: PromptDispatcher.AwaitMode)
            (onAccepted: (PhysicalUserMessageId -> unit) option)
            : Task<Result<PromptKey, string>> =
            this.SendContinuationWithDigest
                port
                sessionId
                text
                (HostDigest.sha256Hex text)
                continuation
                profile
                effectiveAgent
                directory
                awaitMode
                onAccepted

        /// FALLBACK-008: the one interaction repair an unusable terminal earns.
        ///
        /// Its payload digest names the occasion (terminal provider run + repair
        /// kind), not the prompt text. Repair prompts are fixed per kind, so
        /// digesting the text would make every repair of that kind one logical act
        /// and the per-terminal budget would be a per-session budget.
        ///
        /// Deriving the digest this way is also what makes the budget durable: it
        /// enters the claim scope, so the `ClaimSequences` that PROMPT-005 `Claimed`
        /// already writes is the counter `RepairAlreadyClaimed` reads back.
        member this.SendInteractionRepair
            (port: ISessionHostPort)
            (sessionId: SessionId)
            (text: string)
            (terminalProviderRun: ProviderRunIdentity)
            (repairKind: string)
            (profile: PromptAuthority.AuthorityExecutionProfile)
            (effectiveAgent: string)
            (directory: string option)
            (awaitMode: PromptDispatcher.AwaitMode)
            (onAccepted: (PhysicalUserMessageId -> unit) option)
            : Task<Result<PromptKey, string>> =
            this.SendContinuationWithDigest
                port
                sessionId
                text
                (PromptAuthority.repairPayloadDigest terminalProviderRun repairKind)
                PromptAuthority.ContinuationKind.InteractionRepair
                profile
                effectiveAgent
                directory
                awaitMode
                onAccepted
