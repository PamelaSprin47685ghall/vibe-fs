namespace Wanxiangshu.Interaction.Dispatch

open Wanxiangshu.OpenCode
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Change
open Wanxiangshu.Participant.Provider.Attempt.Fallback

open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Host
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Outcome
open Wanxiangshu.Foundation

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

    let private handleAdmittedPhysical
        (submitted: TransportReceipt -> Task<Result<unit, string>>)
        (acceptPhysical: PhysicalUserMessageId -> Task<Result<unit, string>>)
        (physicalId: PhysicalUserMessageId)
        (key: PromptKey)
        : Task<Result<PromptKey, string>> =
        taskResult {
            let! _ = submitted (TransportReceipt.create (PhysicalUserMessageId.value physicalId))
            let! _ = acceptPhysical physicalId
            return key
        }

    let private cancelPhysicalOnError (key: PromptKey) (result: Result<PromptKey, string>) =
        match result with
        | Ok _ -> result
        | Error _ ->
            PromptPhysicalAcceptance.cancel key
            result

    let private awaitPhysicalAwareSend
        (key: PromptKey)
        (sendTask: Task<SendOutcome>)
        (record: SendOutcome -> Task<Result<PromptKey, string>>)
        : Task<Result<PromptKey, string>> =
        task {
            try
                let! outcome = sendTask
                let! result = record outcome
                return cancelPhysicalOnError key result
            with ex ->
                PromptPhysicalAcceptance.cancel key
                return raise ex
        }

    let private publicResultOfAttempt =
        function
        | PromptDispatcher.SendAttemptOutcome.Sent key -> Ok key
        | PromptDispatcher.SendAttemptOutcome.NotSent error -> Error error
        | PromptDispatcher.SendAttemptOutcome.Failed error -> Error error
        | PromptDispatcher.SendAttemptOutcome.AdmissionRejected failure ->
            Error(sprintf "idle-derived send admission rejected before physical dispatch: %A" failure)

    let private continuationAttemptOutcome (key: PromptKey) (outcome: SendOutcome) (result: Result<PromptKey, string>) =
        match outcome, result with
        | (Retryable _ | Fatal _), Error error ->
            PromptPhysicalAcceptance.cancel key
            PromptDispatcher.SendAttemptOutcome.NotSent error
        | _, Ok sentKey -> PromptDispatcher.SendAttemptOutcome.Sent sentKey
        | _, Error error ->
            PromptPhysicalAcceptance.cancel key
            PromptDispatcher.SendAttemptOutcome.Failed error

    let private awaitPhysicalAwareContinuationAttempt
        (key: PromptKey)
        (sendTask: Task<SendOutcome>)
        (record: SendOutcome -> Task<Result<PromptKey, string>>)
        : Task<PromptDispatcher.SendAttemptOutcome> =
        task {
            try
                let! outcome = sendTask
                let! result = record outcome
                return continuationAttemptOutcome key outcome result
            with ex ->
                PromptPhysicalAcceptance.cancel key
                return raise ex
        }

    let private physicalSendAdmission (admission: (unit -> Result<unit, QuiescencePermitFailure>) option) =
        admission |> Option.map (fun admit -> admit ()) |> Option.defaultValue (Ok())

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
            (acceptPhysical: PhysicalUserMessageId -> Task<Result<unit, string>>)
            : Task<Result<PromptKey, string>> =
            task {
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
                    task {
                        match! this.Abandon key sessionId reason with
                        | Ok() -> return Error error
                        | Error persistError -> return Error persistError
                    }

                match outcome with
                | AdmittedWithReceipt receipt ->
                    // PROMPT-005: an `accepted-*` receipt is not a message identity, so
                    // the chain stops at Submitted. `chat.message` supplies the physical
                    // id later and PromptIngress writes PhysicalAccepted then.
                    // PROMPT-007 Detached: this is already a complete success for the caller.
                    let! persisted = submitted receipt
                    return persisted |> Result.map (fun () -> key)

                | AdmittedWithPhysicalMessage physicalId ->
                    // The Host answered with a real id. That answer is still the
                    // transport receipt — it is simply not admission-shaped — so the
                    // four-stage chain stays intact instead of skipping Submitted.
                    return! handleAdmittedPhysical submitted acceptPhysical physicalId key

                | Retryable error -> return! abandon (PromptAbandonReason.SendFailed error) error
                | Fatal error -> return! abandon (PromptAbandonReason.SendFailed error) error

                | AcceptanceUnknown reason ->
                    return Error(sprintf "Acceptance unknown for PromptKey %s: %s" (PromptKey.value key) reason)
            }

        member private this.PersistDetachedInvocation(key: PromptKey, sessionId: SessionId) =
            // PROMPT-007: this is a local invocation receipt, not a physical
            // message id and not the eventual SDK Promise result. It is durable
            // before the Detached caller returns so immediate reuse/recovery sees
            // the claim as already handed to Host async enqueue.
            PromptFact.PluginPromptSubmitted
                {| PromptKey = key
                   SessionId = sessionId
                   Receipt = TransportReceipt.create ("accepted-detached-" + PromptKey.value key) |}
            |> this.Persist sessionId None

        /// PROMPT-007: observe a Host send after a Detached caller has already
        /// received its PromptKey. Any later non-success is no longer a normal
        /// tool consequence: the caller cannot safely retract its success or
        /// decide whether resending would duplicate a physical message, so the
        /// current process must stop. Submitted is not rewritten here: the local
        /// invocation receipt was already durable before caller return.
        member private this.ObserveDetachedSend
            (key: PromptKey)
            (sessionId: SessionId)
            (sendTask: Task<SendOutcome>)
            (onFailure: (string -> Task) option)
            : unit =
            let classifyDetachedOutcome outcome =
                match outcome with
                | AdmittedWithReceipt _
                | AdmittedWithPhysicalMessage _ ->
                    // Detached never races chat.message by writing
                    // PhysicalAccepted from an SDK return value. The exact
                    // Host ingress is the sole physical-identity authority.
                    Ok()
                | Retryable error
                | Fatal error -> Error error
                | AcceptanceUnknown reason ->
                    Error(sprintf "Acceptance unknown for PromptKey %s: %s" (PromptKey.value key) reason)

            let settle =
                task {
                    try
                        let! outcome = sendTask
                        return classifyDetachedOutcome outcome
                    with ex ->
                        return Error ex.Message
                }

            let failDetached error =
                task {
                    match onFailure with
                    | Some callback -> do! callback error
                    | None -> ()

                    Diagnostic.fatal
                        "detached-prompt-dispatch-failed"
                        [ "session_id", SessionId.value sessionId; "result", error ]
                }

            task {
                let! settled = settle

                match settled with
                | Ok() -> ()
                | Error error -> do! failDetached error
            }
            |> ignore

        /// PROMPT-002: a plugin-owned Authority Root.
        ///
        /// The root's LogicalRunId and AuthorityRootUserMessageId are both `None` in
        /// the key derivation because neither exists yet — this send is what creates
        /// them. Substituting empty strings would make "no run yet" and "a run named
        /// empty" derive the same key.
        member private this.SendAgentOwnerRootCore
            (port: ISessionHostPort)
            (sessionId: SessionId)
            (text: string)
            (identitySeed: PromptAuthority.IdentitySeed)
            (directory: string option)
            (awaitMode: PromptDispatcher.AwaitMode)
            (onAccepted: (PhysicalUserMessageId -> unit) option)
            (onDetachedFailure: (string -> Task) option)
            (tools: Map<string, bool> option)
            (model: OpencodeModel option)
            : Task<Result<PromptKey, string>> =
            taskResult {
                let! participantIdentity =
                    this.ValidateAgentOwnerIdentitySeed identitySeed
                    |> Result.mapError PromptDispatcher.describeIdentitySeedRejection

                let agent = ParticipantIdentity.selectedAgent participantIdentity
                let payloadDigest = HostDigest.sha256Hex text

                let origin =
                    PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.AgentOwnerRoot

                let key =
                    deriveKey (this.ProjectionFor sessionId) sessionId None None origin (Some agent) payloadDigest

                let! claim = PromptAuthorityRun.claimAgentOwnerRoot key sessionId payloadDigest identitySeed

                let claimed =
                    PromptFact.PluginPromptClaimed
                        {| PromptKey = key
                           SessionId = sessionId
                           ContinuationKind = PromptDispatcher.originLabel origin
                           LogicalRunId = None
                           AuthorityRootUserMessageId = None
                           EffectiveAgent = claim.EffectiveAgent
                           IdentitySeed = claim.IdentitySeed
                           PayloadDigest = payloadDigest |}

                let! _ = this.Persist sessionId None claimed

                // EXEC-003: the terminal listener must exist before the prompt
                // does, or a fast completion has nobody to deliver to.
                use _listener = this.SubscribeNoOp port sessionId

                let options =
                    { Model = model
                      Agent = Some agent
                      Directory = directory
                      Metadata = Some(this.Metadata key (PromptDispatcher.originLabel origin) None)
                      Tools = tools
                      BindingIntent = SessionBindingIntent.Preserve }

                match awaitMode, onAccepted with
                | PromptDispatcher.AwaitMode.Await, Some callback -> PromptPhysicalAcceptance.register key callback
                | _ -> ()

                let sendTask = port.SendPrompt(sessionId, text, options)

                let acceptFn physicalId =
                    this.AcceptPhysicalAgentOwnerRoot key sessionId physicalId claim.IdentitySeed
                    |> TaskValue.map (Result.map ignore)

                match awaitMode with
                | PromptDispatcher.AwaitMode.Detached ->
                    let! _ = this.PersistDetachedInvocation(key, sessionId)
                    this.ObserveDetachedSend key sessionId sendTask onDetachedFailure
                    return key
                | PromptDispatcher.AwaitMode.Await ->
                    return!
                        awaitPhysicalAwareSend key sendTask (fun outcome ->
                            this.RecordSendOutcome key sessionId outcome acceptFn)
            }

        member this.SendAgentOwnerRoot
            (port: ISessionHostPort)
            (sessionId: SessionId)
            (text: string)
            (identitySeed: PromptAuthority.IdentitySeed)
            (directory: string option)
            (awaitMode: PromptDispatcher.AwaitMode)
            (onAccepted: (PhysicalUserMessageId -> unit) option)
            : Task<Result<PromptKey, string>> =
            this.SendAgentOwnerRootCore port sessionId text identitySeed directory awaitMode onAccepted None None None

        member this.SendAgentOwnerRootDetachedObserved
            (port: ISessionHostPort)
            (sessionId: SessionId)
            (text: string)
            (identitySeed: PromptAuthority.IdentitySeed)
            (directory: string option)
            (onFailure: string -> Task)
            : Task<Result<PromptKey, string>> =
            this.SendAgentOwnerRootCore
                port
                sessionId
                text
                identitySeed
                directory
                PromptDispatcher.AwaitMode.Detached
                None
                (Some onFailure)
                None
                None

        member this.SendAgentOwnerRootWithTools
            (port: ISessionHostPort)
            (sessionId: SessionId)
            (text: string)
            (identitySeed: PromptAuthority.IdentitySeed)
            (directory: string option)
            (awaitMode: PromptDispatcher.AwaitMode)
            (onAccepted: (PhysicalUserMessageId -> unit) option)
            (tools: Map<string, bool>)
            (model: OpencodeModel option)
            : Task<Result<PromptKey, string>> =
            this.SendAgentOwnerRootCore
                port
                sessionId
                text
                identitySeed
                directory
                awaitMode
                onAccepted
                None
                (Some tools)
                model

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
        member private this.SendClaimedContinuation
            (port: ISessionHostPort)
            (sessionId: SessionId)
            (text: string)
            (originLabel: string)
            (profile: PromptAuthority.AuthorityExecutionProfile)
            (effectiveAgent: string)
            (directory: string option)
            (awaitMode: PromptDispatcher.AwaitMode)
            (onAccepted: (PhysicalUserMessageId -> unit) option)
            (tools: Map<string, bool> option)
            (physicalAdmission: (unit -> Result<unit, QuiescencePermitFailure>) option)
            (key: PromptKey)
            : Task<PromptDispatcher.SendAttemptOutcome> =
            task {
                use _listener = this.SubscribeNoOp port sessionId

                let bindingIntent =
                    if effectiveAgent = profile.SelectedAgent then
                        SessionBindingIntent.Preserve
                    else
                        SessionBindingIntent.ExplicitExecutionOverride

                let options =
                    { Model = None
                      Agent = Some effectiveAgent
                      Directory = directory
                      Metadata = Some(this.Metadata key originLabel (Some profile.LogicalRunId))
                      Tools = tools
                      BindingIntent = bindingIntent }

                match awaitMode, onAccepted with
                | PromptDispatcher.AwaitMode.Await, Some callback -> PromptPhysicalAcceptance.register key callback
                | _ -> ()

                // The admission check and the Host call are deliberately
                // synchronous neighbours. No await may reopen a window where
                // newer physical material can arrive after quiescence was
                // proven but before SendPrompt is invoked.
                let sendAdmitted () : Task<PromptDispatcher.SendAttemptOutcome> =
                    task {
                        let sendTask = port.SendPrompt(sessionId, text, options)

                        let acceptFn physicalId =
                            this.AcceptContinuation key sessionId physicalId
                            |> TaskValue.map (Result.map ignore)

                        let detachedOutcome () : Task<PromptDispatcher.SendAttemptOutcome> =
                            task {
                                let! persisted = this.PersistDetachedInvocation(key, sessionId)

                                match persisted with
                                | Error error -> return PromptDispatcher.SendAttemptOutcome.Failed error
                                | Ok() ->
                                    this.ObserveDetachedSend key sessionId sendTask None
                                    return PromptDispatcher.SendAttemptOutcome.Sent key
                            }

                        let sendAfterAdmission () : Task<PromptDispatcher.SendAttemptOutcome> =
                            match awaitMode with
                            | PromptDispatcher.AwaitMode.Detached -> detachedOutcome ()
                            | PromptDispatcher.AwaitMode.Await ->
                                awaitPhysicalAwareContinuationAttempt key sendTask (fun outcome ->
                                    this.RecordSendOutcome key sessionId outcome acceptFn)

                        return! sendAfterAdmission ()
                    }

                match physicalSendAdmission physicalAdmission with
                | Error failure ->
                    return!
                        this.Abandon key sessionId PromptAbandonReason.SupersededBeforePhysicalSend
                        |> TaskValue.map (function
                            | Ok() -> PromptDispatcher.SendAttemptOutcome.AdmissionRejected failure
                            | Error error -> PromptDispatcher.SendAttemptOutcome.Failed error)
                | Ok() -> return! sendAdmitted ()
            }

        member private this.SendContinuationWithDigestAttempt
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
            (tools: Map<string, bool> option)
            (physicalAdmission: (unit -> Result<unit, QuiescencePermitFailure>) option)
            : Task<PromptDispatcher.SendAttemptOutcome> =
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
                           IdentitySeed = claim.IdentitySeed
                           PayloadDigest = payloadDigest |}

                match! this.Persist sessionId None claimed with
                | Error error -> return PromptDispatcher.SendAttemptOutcome.Failed error
                | Ok() ->
                    return!
                        this.SendClaimedContinuation
                            port
                            sessionId
                            text
                            originLabel
                            profile
                            effectiveAgent
                            directory
                            awaitMode
                            onAccepted
                            tools
                            physicalAdmission
                            key
            }

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
            (tools: Map<string, bool> option)
            : Task<Result<PromptKey, string>> =
            this.SendContinuationWithDigestAttempt
                port
                sessionId
                text
                payloadDigest
                continuation
                profile
                effectiveAgent
                directory
                awaitMode
                onAccepted
                tools
                None
            |> TaskValue.map publicResultOfAttempt

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
                None

        /// Non-idle gate reminder with exact terminal occasion identity. Used by
        /// terminal-subscriber gates (for example Relay exit-required)
        /// that do not derive authority from SessionIdle.
        member this.SendGateNudge
            (port: ISessionHostPort)
            (sessionId: SessionId)
            (text: string)
            (continuation: PromptAuthority.ContinuationKind)
            (gateKind: string)
            (terminalProviderRun: ProviderRunIdentity)
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
                (PromptAuthority.gateNudgePayloadDigest gateKind terminalProviderRun)
                continuation
                profile
                effectiveAgent
                directory
                awaitMode
                onAccepted
                None

        member this.SendContinuationWithTools
            (port: ISessionHostPort)
            (sessionId: SessionId)
            (text: string)
            (continuation: PromptAuthority.ContinuationKind)
            (profile: PromptAuthority.AuthorityExecutionProfile)
            (effectiveAgent: string)
            (directory: string option)
            (awaitMode: PromptDispatcher.AwaitMode)
            (onAccepted: (PhysicalUserMessageId -> unit) option)
            (tools: Map<string, bool>)
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
                (Some tools)

        /// FALLBACK-008: the one Blogger-request + terminal-scoped interaction repair an unusable terminal earns.
        ///
        /// Its payload digest names the occasion (BloggerRequestId + terminal
        /// provider run + repair kind), not the prompt text. Request identity
        /// prevents an earlier Blogger request on the same long-lived run from
        /// spending the next request's nudge/AABB budget.
        ///
        /// Deriving the digest this way is also what makes the budget durable: it
        /// enters the claim scope, so the `ClaimSequences` that PROMPT-005 `Claimed`
        /// already writes is the counter `RepairAlreadyClaimed` reads back.
        member this.SendInteractionRepair
            (port: ISessionHostPort)
            (sessionId: SessionId)
            (text: string)
            (requestId: BloggerRequestId)
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
                (PromptAuthority.repairPayloadDigest requestId terminalProviderRun repairKind)
                PromptAuthority.ContinuationKind.InteractionRepair
                profile
                effectiveAgent
                directory
                awaitMode
                onAccepted
                None

        /// HOST-004: idle-derived continuation whose quiescence permit is
        /// consumed at the final physical SendPrompt boundary, after durable
        /// claim persistence. This is the only continuation send surface that
        /// may return `Superseded`.
        member internal this.SendIdleContinuation
            (port: ISessionHostPort)
            (sessionId: SessionId)
            (text: string)
            (continuation: PromptAuthority.ContinuationKind)
            (profile: PromptAuthority.AuthorityExecutionProfile)
            (effectiveAgent: string)
            (directory: string option)
            (awaitMode: PromptDispatcher.AwaitMode)
            (onAccepted: (PhysicalUserMessageId -> unit) option)
            (physicalAdmission: unit -> Result<unit, QuiescencePermitFailure>)
            : Task<PromptDispatcher.SendAttemptOutcome> =
            this.SendContinuationWithDigestAttempt
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
                None
                (Some physicalAdmission)

        /// Gate reminder: exactly-once for one terminal occasion, intentionally
        /// unbounded across fresh terminals while the business gate remains open.
        member internal this.SendIdleGateNudge
            (port: ISessionHostPort)
            (sessionId: SessionId)
            (text: string)
            (continuation: PromptAuthority.ContinuationKind)
            (gateKind: string)
            (terminalProviderRun: ProviderRunIdentity)
            (profile: PromptAuthority.AuthorityExecutionProfile)
            (effectiveAgent: string)
            (directory: string option)
            (awaitMode: PromptDispatcher.AwaitMode)
            (physicalAdmission: unit -> Result<unit, QuiescencePermitFailure>)
            : Task<PromptDispatcher.SendAttemptOutcome> =
            this.SendContinuationWithDigestAttempt
                port
                sessionId
                text
                (PromptAuthority.gateNudgePayloadDigest gateKind terminalProviderRun)
                continuation
                profile
                effectiveAgent
                directory
                awaitMode
                None
                None
                (Some physicalAdmission)

        member internal this.SendIdleInteractionRepair
            (port: ISessionHostPort)
            (sessionId: SessionId)
            (text: string)
            (requestId: BloggerRequestId)
            (terminalProviderRun: ProviderRunIdentity)
            (repairKind: string)
            (profile: PromptAuthority.AuthorityExecutionProfile)
            (effectiveAgent: string)
            (directory: string option)
            (awaitMode: PromptDispatcher.AwaitMode)
            (physicalAdmission: unit -> Result<unit, QuiescencePermitFailure>)
            : Task<PromptDispatcher.SendAttemptOutcome> =
            this.SendContinuationWithDigestAttempt
                port
                sessionId
                text
                (PromptAuthority.repairPayloadDigest requestId terminalProviderRun repairKind)
                PromptAuthority.ContinuationKind.InteractionRepair
                profile
                effectiveAgent
                directory
                awaitMode
                None
                None
                (Some physicalAdmission)
