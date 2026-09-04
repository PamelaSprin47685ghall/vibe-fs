namespace Wanxiangshu.Interaction.Dispatch.OpenCode

open System
open System.Threading.Tasks
open FsToolkit.ErrorHandling

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

open Wanxiangshu.Execution.Fission
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Provider.Attempt.Fallback

/// Continuation sends against an already-accepted Authority Root.
///
/// Every entry point takes `AgentJournal option` because the host callbacks that
/// reach here do, but none of them substitutes a journal-less dispatcher when it
/// is `None`. PROMPT-005 makes a plugin prompt a durable act: with nowhere to
/// record the claim there is nothing legitimate to send, so these fail closed.
module HostSessionNudge =

    let tryActiveProfile (journal: AgentJournal option) (sessionId: SessionId) =
        journal
        |> Option.bind (fun j ->
            PromptAuthorityLedger.activeProfile sessionId (AgentJournal.snapshot j).AgentProjections)

    /// Look up the agent the current cursor selects.
    ///
    /// Named for the lookup, not the algorithm: FALLBACK-002's side selection is
    /// owned by AgentPairCursor, and this only fetches the cursor and asks. A
    /// second `effectiveAgent` here would read as a competing implementation.
    ///
    /// No cursor means no accepted Authority Root (FALLBACK-001), so there is no
    /// fallback state to consult and SelectedAgent is the only defensible answer.
    let private agentForActiveCursor
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (profile: PromptAuthority.AuthorityExecutionProfile)
        =
        journal
        |> Option.map (fun j -> FallbackEvidence.effectiveAgent sessionId (AgentJournal.snapshot j) profile)
        |> Option.defaultValue profile.SelectedAgent

    /// The continuation target directory.
    ///
    /// ORCH-006: guard nudges now pass the root workspace explicitly. The
    /// manager worktree is removed at publish, so a residual guard-round
    /// continuation would otherwise load Host instructions from a deleted path,
    /// truncating the system prompt and breaking the ARCH-004 seal. Using the
    /// root workspace gives a stable, deterministic set of root instructions,
    /// which differs from the previous worktree version by design; the
    /// scenario's prefix-probe boundary carries that transition.
    let private liveDirectory (rootWorkspace: IRootWorkspaceReader) (directory: string option) =
        RootWorkspaceDirectory.select System.IO.Directory.Exists rootWorkspace directory

    let private isFissionReplaced (journal: AgentJournal option) (sessionId: SessionId) : bool =
        FissionRuntime.isSilentInterrupt sessionId
        || (journal
            |> Option.exists (fun durable ->
                FissionProjection.tryActiveForOwner sessionId (AgentJournal.snapshot durable).AgentProjections.Fission
                |> Option.isSome))

    let sendContinuationResult
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (sessionId: SessionId)
        (prompt: string)
        (kind: PromptAuthority.ContinuationKind)
        (directory: string option)
        (journal: AgentJournal option)
        (awaitMode: PromptDispatcher.AwaitMode)
        (onAccepted: (PhysicalUserMessageId -> unit) option)
        : Task<Result<PromptKey, string>> =
        task {
            match isFissionReplaced journal sessionId, journal, tryActiveProfile journal sessionId with
            | true, _, _ -> return Error "Session is retired by Fission"
            | false, None, _ -> return Error "No journal: a continuation cannot be claimed"
            | false, Some _, None -> return Error "No active authority profile"
            | false, Some durable, Some profile ->
                let agent = agentForActiveCursor journal sessionId profile
                let rt = PromptDispatcher.forJournal durable

                return!
                    rt.SendContinuation
                        sessionPort
                        sessionId
                        prompt
                        kind
                        profile
                        agent
                        (liveDirectory rootWorkspace directory)
                        awaitMode
                        onAccepted
        }

    /// PROMPT-007 fire-and-forget: Detached — caller does not await PhysicalAccepted.
    ///
    /// The task is still observed. Discarding it — as `|> ignore` on the task did
    /// — also discarded the claim/abandon bookkeeping inside it, so a send that
    /// failed left a Claimed fact with nothing following it and no log line.
    let sendContinuation
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (sessionId: SessionId)
        (prompt: string)
        (kind: PromptAuthority.ContinuationKind)
        (directory: string option)
        (journal: AgentJournal option)
        : Task<Result<PromptKey, string>> =
        sendContinuationResult
            sessionPort
            rootWorkspace
            sessionId
            prompt
            kind
            directory
            journal
            PromptDispatcher.AwaitMode.Detached
            None

    [<RequireQualifiedAccess>]
    type GateContinuationOutcome =
        | Sent of PromptKey
        | AlreadyAdmitted
        | Retired
        | Failed of string

    let private sendGateContinuationWithProfile
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (sessionId: SessionId)
        (prompt: string)
        (continuation: PromptAuthority.ContinuationKind)
        (directory: string option)
        (journal: AgentJournal option)
        (gateKind: string)
        (terminalProviderRun: ProviderRunIdentity)
        (onAccepted: (PhysicalUserMessageId -> unit) option)
        (durable: AgentJournal)
        (profile: PromptAuthority.AuthorityExecutionProfile)
        : Task<GateContinuationOutcome> =
        let rt = PromptDispatcher.forJournal durable

        if rt.GateNudgeAlreadyAdmitted profile continuation gateKind terminalProviderRun then
            Task.FromResult GateContinuationOutcome.AlreadyAdmitted
        else
            let agent = agentForActiveCursor journal sessionId profile

            rt.SendGateNudge
                sessionPort
                sessionId
                prompt
                continuation
                gateKind
                terminalProviderRun
                profile
                agent
                (liveDirectory rootWorkspace directory)
                PromptDispatcher.AwaitMode.Await
                onAccepted
            |> TaskValue.map (function
                | Ok key -> GateContinuationOutcome.Sent key
                | Error error -> GateContinuationOutcome.Failed error)

    /// Gate reminder for a terminal-driven protocol that is not idle-derived.
    /// Durable dedupe is exact `(gate kind, ProviderRunIdentity)` only.
    let trySendGateContinuation
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (sessionId: SessionId)
        (prompt: string)
        (continuation: PromptAuthority.ContinuationKind)
        (directory: string option)
        (journal: AgentJournal option)
        (gateKind: string)
        (terminalProviderRun: ProviderRunIdentity)
        : Task<GateContinuationOutcome> =
        task {
            match isFissionReplaced journal sessionId, journal, tryActiveProfile journal sessionId with
            | true, _, _ -> return GateContinuationOutcome.Retired
            | false, None, _ -> return GateContinuationOutcome.Failed "No journal: a gate nudge cannot be claimed"
            | false, Some _, None -> return GateContinuationOutcome.Failed "No active authority profile"
            | false, Some durable, Some profile ->
                return!
                    sendGateContinuationWithProfile
                        sessionPort
                        rootWorkspace
                        sessionId
                        prompt
                        continuation
                        directory
                        journal
                        gateKind
                        terminalProviderRun
                        None
                        durable
                        profile
        }

    let private sendGateContinuationPhysicalWithProfile
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (sessionId: SessionId)
        (prompt: string)
        (continuation: PromptAuthority.ContinuationKind)
        (directory: string option)
        (journal: AgentJournal option)
        (gateKind: string)
        (terminalProviderRun: ProviderRunIdentity)
        (durable: AgentJournal)
        (profile: PromptAuthority.AuthorityExecutionProfile)
        : Task<Result<PhysicalUserMessageId, string>> =
        let rt = PromptDispatcher.forJournal durable

        let physicalResult outcome acceptedPhysical =
            match outcome, acceptedPhysical with
            | GateContinuationOutcome.Sent _, Some physical -> Ok physical
            | GateContinuationOutcome.Sent _, None -> Error "gate nudge was admitted without a PhysicalUserMessageId"
            | GateContinuationOutcome.AlreadyAdmitted, _ -> Error "gate nudge is pending physical acceptance"
            | GateContinuationOutcome.Retired, _ -> Error "gate nudge target is retired"
            | GateContinuationOutcome.Failed error, _ -> Error error

        match rt.GateNudgeAcceptedPhysical profile continuation gateKind terminalProviderRun with
        | Some physical -> Task.FromResult(Ok physical)
        | None when rt.GateNudgeAlreadyAdmitted profile continuation gateKind terminalProviderRun ->
            Task.FromResult(Error "gate nudge is pending physical acceptance")
        | None ->
            task {
                let acceptedPhysical = ref None

                let! outcome =
                    sendGateContinuationWithProfile
                        sessionPort
                        rootWorkspace
                        sessionId
                        prompt
                        continuation
                        directory
                        journal
                        gateKind
                        terminalProviderRun
                        (Some(fun physical -> acceptedPhysical.Value <- Some physical))
                        durable
                        profile

                return physicalResult outcome acceptedPhysical.Value
            }

    let trySendGateContinuationPhysical
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (sessionId: SessionId)
        (prompt: string)
        (continuation: PromptAuthority.ContinuationKind)
        (directory: string option)
        (journal: AgentJournal option)
        (gateKind: string)
        (terminalProviderRun: ProviderRunIdentity)
        : Task<Result<PhysicalUserMessageId, string>> =
        task {
            match isFissionReplaced journal sessionId, journal, tryActiveProfile journal sessionId with
            | true, _, _ -> return Error "gate nudge target is retired"
            | false, None, _ -> return Error "No journal: a gate nudge cannot be claimed"
            | false, Some _, None -> return Error "No active authority profile"
            | false, Some durable, Some profile ->
                return!
                    sendGateContinuationPhysicalWithProfile
                        sessionPort
                        rootWorkspace
                        sessionId
                        prompt
                        continuation
                        directory
                        journal
                        gateKind
                        terminalProviderRun
                        durable
                        profile
        }

    let private interactionRepairOutcomeOfResult =
        function
        | Ok key -> InteractionRepairSendOutcome.Sent key
        | Error error -> InteractionRepairSendOutcome.Failed error

    let private sendInteractionRepairWithProfile
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (sessionId: SessionId)
        (prompt: string)
        (directory: string option)
        (journal: AgentJournal option)
        (requestId: BloggerRequestId)
        (terminalProviderRun: ProviderRunIdentity)
        (repairKind: string)
        (durable: AgentJournal)
        (profile: PromptAuthority.AuthorityExecutionProfile)
        : Task<InteractionRepairSendOutcome> =
        let rt = PromptDispatcher.forJournal durable

        if rt.RepairAlreadyClaimed profile requestId terminalProviderRun repairKind then
            Task.FromResult InteractionRepairSendOutcome.AlreadyAdmitted
        else
            let agent = agentForActiveCursor journal sessionId profile

            // Blogger repair must know whether Host transport accepted or
            // refused this nudge so a hard refusal can immediately advance
            // to AABB. Await waits only the SendPrompt transport result; it
            // never waits for provider execution/slots.
            rt.SendInteractionRepair
                sessionPort
                sessionId
                prompt
                requestId
                terminalProviderRun
                repairKind
                profile
                agent
                (liveDirectory rootWorkspace directory)
                PromptDispatcher.AwaitMode.Await
                None
            |> TaskValue.map interactionRepairOutcomeOfResult

    /// FALLBACK-008: an empty / XML-only terminal earns at most one repair.
    ///
    /// `requestId + terminalProviderRun` names the exact Blogger repair occasion.
    /// Neither the long-lived session nor LogicalRun alone may spend another
    /// Blogger request's protocol budget.
    ///
    /// The budget check is a read of durable `ClaimSequences`, so a repair claimed
    /// before a crash is still spent after it.
    let trySendInteractionRepair
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (sessionId: SessionId)
        (prompt: string)
        (directory: string option)
        (journal: AgentJournal option)
        (requestId: BloggerRequestId)
        (terminalProviderRun: ProviderRunIdentity)
        (repairKind: string)
        : Task<InteractionRepairSendOutcome> =
        task {
            match isFissionReplaced journal sessionId, journal, tryActiveProfile journal sessionId with
            | true, _, _ -> return InteractionRepairSendOutcome.Retired
            | false, None, _ ->
                return InteractionRepairSendOutcome.Failed "No journal: an interaction repair cannot be claimed"
            | false, Some _, None -> return InteractionRepairSendOutcome.Failed "No active authority profile"
            | false, Some durable, Some profile ->
                return!
                    sendInteractionRepairWithProfile
                        sessionPort
                        rootWorkspace
                        sessionId
                        prompt
                        directory
                        journal
                        requestId
                        terminalProviderRun
                        repairKind
                        durable
                        profile
        }

    // ── idle-derived continuation admission（HOST-004）────────────────────────

    /// What an idle-derived send attempt came to.
    [<RequireQualifiedAccess>]
    type IdleContinuationOutcome =
        | Sent of PromptKey
        /// The idle occasion expired before the physical send (a newer provider
        /// attempt/new physical message began, or the session was dropped). Not
        /// an error and never a physical send. If supersession wins after durable
        /// claim persistence, the dispatcher closes that audit trail with
        /// `PluginPromptAbandoned(SupersededBeforePhysicalSend)`.
        | AdmissionRejected of QuiescencePermitFailure
        /// A concurrent observer already admitted this exact durable occasion.
        /// This is idempotency evidence, never transport/protocol failure.
        | AlreadyAdmitted
        /// The logical owner was replaced before this idle continuation could act.
        | Retired
        /// Host definitively rejected before physical acceptance. The exact
        /// quiescence permit has been returned to Idle and may be retried.
        | NotSent of string
        | Failed of string

    let private idleOutcomeOfDispatch =
        function
        | PromptDispatcher.SendAttemptOutcome.Sent key -> IdleContinuationOutcome.Sent key
        | PromptDispatcher.SendAttemptOutcome.AdmissionRejected failure ->
            IdleContinuationOutcome.AdmissionRejected failure
        | PromptDispatcher.SendAttemptOutcome.NotSent error -> IdleContinuationOutcome.NotSent error
        | PromptDispatcher.SendAttemptOutcome.Failed error -> IdleContinuationOutcome.Failed error

    let private gateIdleOutcome
        (releaseAdmission: unit -> Result<unit, QuiescencePermitFailure>)
        (outcome: PromptDispatcher.SendAttemptOutcome)
        =
        let releaseResult =
            match outcome with
            | PromptDispatcher.SendAttemptOutcome.NotSent _ -> Some(releaseAdmission ())
            | _ -> None

        match outcome, releaseResult with
        | PromptDispatcher.SendAttemptOutcome.NotSent _, Some(Error failure) ->
            IdleContinuationOutcome.AdmissionRejected failure
        | _ -> idleOutcomeOfDispatch outcome

    /// HOST-004 + GLORY-029: idle-derived Manager encouragement with exact-terminal
    /// idempotency and no cross-terminal count limit.
    let private sendIdleManagerWithProfile
        (quiescence: ISessionQuiescenceGate)
        (permit: QuiescencePermit)
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (sessionId: SessionId)
        (prompt: string)
        (directory: string option)
        (journal: AgentJournal option)
        (lifeId: ManagerLifeId)
        (conditionKey: string)
        (terminalProviderRun: ProviderRunIdentity)
        (durable: AgentJournal)
        (profile: PromptAuthority.AuthorityExecutionProfile)
        : Task<IdleContinuationOutcome> =
        let rt = PromptDispatcher.forJournal durable

        if rt.IdleAlreadyAdmitted profile lifeId conditionKey terminalProviderRun then
            Task.FromResult IdleContinuationOutcome.AlreadyAdmitted
        else
            let agent = agentForActiveCursor journal sessionId profile

            rt.SendIdleManagerIdleEncouragement
                sessionPort
                sessionId
                prompt
                lifeId
                conditionKey
                terminalProviderRun
                profile
                agent
                (liveDirectory rootWorkspace directory)
                PromptDispatcher.AwaitMode.Await
                (fun () -> quiescence.TryConsume permit)
            |> TaskValue.map (gateIdleOutcome (fun () -> quiescence.TryRelease permit))

    let trySendIdleManagerEncouragement
        (quiescence: ISessionQuiescenceGate)
        (permit: QuiescencePermit)
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (sessionId: SessionId)
        (prompt: string)
        (directory: string option)
        (journal: AgentJournal option)
        (lifeId: ManagerLifeId)
        (conditionKey: string)
        (terminalProviderRun: ProviderRunIdentity)
        : Task<IdleContinuationOutcome> =
        task {
            match isFissionReplaced journal sessionId, journal, tryActiveProfile journal sessionId with
            | true, _, _ -> return IdleContinuationOutcome.Retired
            | false, None, _ ->
                return IdleContinuationOutcome.Failed "No journal: a manager idle encouragement cannot be claimed"
            | false, Some _, None -> return IdleContinuationOutcome.Failed "No active authority profile"
            | false, Some durable, Some profile ->
                return!
                    sendIdleManagerWithProfile
                        quiescence
                        permit
                        sessionPort
                        rootWorkspace
                        sessionId
                        prompt
                        directory
                        journal
                        lifeId
                        conditionKey
                        terminalProviderRun
                        durable
                        profile
        }

    let private sendGateContinuationWithAdmissionProfile
        (physicalAdmission: unit -> Result<unit, QuiescencePermitFailure>)
        (releaseAdmission: unit -> Result<unit, QuiescencePermitFailure>)
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (sessionId: SessionId)
        (prompt: string)
        (directory: string option)
        (journal: AgentJournal option)
        (continuation: PromptAuthority.ContinuationKind)
        (gateKind: string)
        (terminalProviderRun: ProviderRunIdentity)
        (awaitMode: PromptDispatcher.AwaitMode)
        (durable: AgentJournal)
        (profile: PromptAuthority.AuthorityExecutionProfile)
        : Task<IdleContinuationOutcome> =
        let rt = PromptDispatcher.forJournal durable

        if rt.GateNudgeAlreadyAdmitted profile continuation gateKind terminalProviderRun then
            Task.FromResult IdleContinuationOutcome.AlreadyAdmitted
        else
            let agent = agentForActiveCursor journal sessionId profile

            rt.SendIdleGateNudge
                sessionPort
                sessionId
                prompt
                continuation
                gateKind
                terminalProviderRun
                profile
                agent
                (liveDirectory rootWorkspace directory)
                awaitMode
                physicalAdmission
            |> TaskValue.map (gateIdleOutcome releaseAdmission)

    let trySendGateContinuationWithAdmission
        (physicalAdmission: unit -> Result<unit, QuiescencePermitFailure>)
        (releaseAdmission: unit -> Result<unit, QuiescencePermitFailure>)
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (sessionId: SessionId)
        (prompt: string)
        (continuation: PromptAuthority.ContinuationKind)
        (directory: string option)
        (journal: AgentJournal option)
        (gateKind: string)
        (terminalProviderRun: ProviderRunIdentity)
        (awaitMode: PromptDispatcher.AwaitMode)
        : Task<IdleContinuationOutcome> =
        task {
            match isFissionReplaced journal sessionId, journal, tryActiveProfile journal sessionId with
            | true, _, _ -> return IdleContinuationOutcome.Retired
            | false, None, _ -> return IdleContinuationOutcome.Failed "No journal: a gate nudge cannot be claimed"
            | false, Some _, None -> return IdleContinuationOutcome.Failed "No active authority profile"
            | false, Some durable, Some profile ->
                return!
                    sendGateContinuationWithAdmissionProfile
                        physicalAdmission
                        releaseAdmission
                        sessionPort
                        rootWorkspace
                        sessionId
                        prompt
                        directory
                        journal
                        continuation
                        gateKind
                        terminalProviderRun
                        awaitMode
                        durable
                        profile
        }

    /// Shared gate-nudge transport: only duplicate observation of the same exact
    /// terminal is suppressed. A fresh terminal remains eligible while the gate
    /// owner still says the condition is unsatisfied.
    let trySendIdleGateContinuation
        (quiescence: ISessionQuiescenceGate)
        (permit: QuiescencePermit)
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (sessionId: SessionId)
        (prompt: string)
        (continuation: PromptAuthority.ContinuationKind)
        (directory: string option)
        (journal: AgentJournal option)
        (gateKind: string)
        (terminalProviderRun: ProviderRunIdentity)
        (awaitMode: PromptDispatcher.AwaitMode)
        : Task<IdleContinuationOutcome> =
        trySendGateContinuationWithAdmission
            (fun () -> quiescence.TryConsume permit)
            (fun () -> quiescence.TryRelease permit)
            sessionPort
            rootWorkspace
            sessionId
            prompt
            continuation
            directory
            journal
            gateKind
            terminalProviderRun
            awaitMode

    /// Ordinary interaction nudges are gate reminders, not a finite repair
    /// budget: duplicate delivery of one terminal is idempotent, while every
    /// fresh terminal may remind again until the gate is satisfied.
    let trySendIdleGateRepair
        (quiescence: ISessionQuiescenceGate)
        (permit: QuiescencePermit)
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (sessionId: SessionId)
        (prompt: string)
        (directory: string option)
        (journal: AgentJournal option)
        (repairKind: string)
        (terminalProviderRun: ProviderRunIdentity)
        : Task<IdleContinuationOutcome> =
        trySendIdleGateContinuation
            quiescence
            permit
            sessionPort
            rootWorkspace
            sessionId
            prompt
            PromptAuthority.ContinuationKind.InteractionRepair
            directory
            journal
            repairKind
            terminalProviderRun
            PromptDispatcher.AwaitMode.Await

    /// Blogger-request + terminal-scoped idle interaction repair. This narrower
    /// occasion identity distinguishes same-terminal re-entry from a new bad
    /// terminal without leaking repair budget across Blogger requests.
    let private sendIdleInteractionRepairWithProfile
        (quiescence: ISessionQuiescenceGate)
        (permit: QuiescencePermit)
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (sessionId: SessionId)
        (prompt: string)
        (directory: string option)
        (journal: AgentJournal option)
        (requestId: BloggerRequestId)
        (terminalProviderRun: ProviderRunIdentity)
        (repairKind: string)
        (durable: AgentJournal)
        (profile: PromptAuthority.AuthorityExecutionProfile)
        : Task<IdleContinuationOutcome> =
        let rt = PromptDispatcher.forJournal durable

        if rt.RepairAlreadyClaimed profile requestId terminalProviderRun repairKind then
            Task.FromResult IdleContinuationOutcome.AlreadyAdmitted
        else
            let agent = agentForActiveCursor journal sessionId profile

            rt.SendIdleInteractionRepair
                sessionPort
                sessionId
                prompt
                requestId
                terminalProviderRun
                repairKind
                profile
                agent
                (liveDirectory rootWorkspace directory)
                PromptDispatcher.AwaitMode.Await
                (fun () -> quiescence.TryConsume permit)
            |> TaskValue.map (gateIdleOutcome (fun () -> quiescence.TryRelease permit))

    let trySendIdleInteractionRepair
        (quiescence: ISessionQuiescenceGate)
        (permit: QuiescencePermit)
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (sessionId: SessionId)
        (prompt: string)
        (directory: string option)
        (journal: AgentJournal option)
        (requestId: BloggerRequestId)
        (terminalProviderRun: ProviderRunIdentity)
        (repairKind: string)
        : Task<IdleContinuationOutcome> =
        task {
            match isFissionReplaced journal sessionId, journal, tryActiveProfile journal sessionId with
            | true, _, _ -> return IdleContinuationOutcome.Retired
            | false, None, _ ->
                return IdleContinuationOutcome.Failed "No journal: an interaction repair cannot be claimed"
            | false, Some _, None -> return IdleContinuationOutcome.Failed "No active authority profile"
            | false, Some durable, Some profile ->
                return!
                    sendIdleInteractionRepairWithProfile
                        quiescence
                        permit
                        sessionPort
                        rootWorkspace
                        sessionId
                        prompt
                        directory
                        journal
                        requestId
                        terminalProviderRun
                        repairKind
                        durable
                        profile
        }
