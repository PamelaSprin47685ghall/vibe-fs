namespace Wanxiangshu.Interaction.Dispatch.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Fable.Core.JsInterop

open global.Wanxiangshu.Foundation
open global.Wanxiangshu.Foundation.Identity
open global.Wanxiangshu.Host
open global.Wanxiangshu.Host.Contract
open global.Wanxiangshu.OpenCode
open global.Wanxiangshu.OpenCode.Host
open global.Wanxiangshu.Change
open global.Wanxiangshu.Git
open global.Wanxiangshu.Git.Hook
open global.Wanxiangshu.Resources
open global.Wanxiangshu.Composition.Turn
open global.Wanxiangshu.Context.Companion
open global.Wanxiangshu.Context.Companion.Blogger
open global.Wanxiangshu.Context.Companion.Blogger.OpenCode
open global.Wanxiangshu.Context.Companion.Blogger.Runtime
open global.Wanxiangshu.Context.Prefix
open global.Wanxiangshu.Context.Trace
open global.Wanxiangshu.Enforcer
open global.Wanxiangshu.Enforcer.Cycle
open global.Wanxiangshu.Enforcer.Guidance
open global.Wanxiangshu.Execution.Delegation.Fork
open global.Wanxiangshu.Execution.Delegation.Fork.Host
open global.Wanxiangshu.Execution.Delegation.Handle
open global.Wanxiangshu.Execution.Delegation.SyncDelegate
open global.Wanxiangshu.Execution.Fission
open global.Wanxiangshu.Execution.Session
open global.Wanxiangshu.Execution.Session.Attachment
open global.Wanxiangshu.Execution.Session.Recovery
open global.Wanxiangshu.Execution.Session.Wait
open global.Wanxiangshu.Interaction.Authority
open global.Wanxiangshu.Interaction.Dispatch
open global.Wanxiangshu.Interaction.Repair
open global.Wanxiangshu.Mission.Finality
open global.Wanxiangshu.Mission.Manager
open global.Wanxiangshu.Mission.Manager.Life
open global.Wanxiangshu.Mission.Obligation.Todo
open global.Wanxiangshu.Mission.Obligation.Todo.OpenCode
open global.Wanxiangshu.Mission.Review
open global.Wanxiangshu.Mission.Review.Judgement
open global.Wanxiangshu.Mission.WorkRecord
open global.Wanxiangshu.Participant.Persona
open global.Wanxiangshu.Participant.Provider
open global.Wanxiangshu.Participant.Provider.Attempt
open global.Wanxiangshu.Participant.Provider.Attempt.Fallback
open global.Wanxiangshu.Participant.Provider.Projection
open global.Wanxiangshu.Persistence.EventStore
open global.Wanxiangshu.Persistence.Journal
open global.Wanxiangshu.Repository.Investigation.Semble
open global.Wanxiangshu.Repository.Investigation.WarmStart
open global.Wanxiangshu.Repository.Knowledge.Casebook
open global.Wanxiangshu.Repository.Programming.Js
open global.Wanxiangshu.Strength
open global.Wanxiangshu.Strength.OpenCode
open global.Wanxiangshu.Strength.Persistence
open global.Wanxiangshu.Strength.Prediction
open global.Wanxiangshu.Strength.Projection
open global.Wanxiangshu.Strength.Replica

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
    let private liveDirectory (directory: string option) =
        directory
        |> Option.filter (fun path -> System.IO.Directory.Exists path)
        |> Option.orElse SharedState.RootWorkspace

    let private isFissionReplaced (journal: AgentJournal option) (sessionId: SessionId) : bool =
        FissionRuntime.isSilentInterrupt sessionId
        || (journal
            |> Option.exists (fun durable ->
                FissionProjection.tryActiveForOwner sessionId (AgentJournal.snapshot durable).AgentProjections.Fission
                |> Option.isSome))

    let sendContinuationResult
        (sessionPort: ISessionHostPort)
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
                        (liveDirectory directory)
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
        (sessionId: SessionId)
        (prompt: string)
        (kind: PromptAuthority.ContinuationKind)
        (directory: string option)
        (journal: AgentJournal option)
        : Task<Result<PromptKey, string>> =
        sendContinuationResult
            sessionPort
            sessionId
            prompt
            kind
            directory
            journal
            PromptDispatcher.AwaitMode.Detached
            None

    let private interactionRepairOutcomeOfResult =
        function
        | Ok key -> InteractionRepairSendOutcome.Sent key
        | Error error -> InteractionRepairSendOutcome.Failed error

    let private sendInteractionRepairWithProfile
        (sessionPort: ISessionHostPort)
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
                (liveDirectory directory)
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
        | Superseded
        /// A concurrent observer already admitted this exact durable occasion.
        /// This is idempotency evidence, never transport/protocol failure.
        | AlreadyAdmitted
        /// The logical owner was replaced before this idle continuation could act.
        | Retired
        | Failed of string

    let private idleOutcomeOfDispatch =
        function
        | PromptDispatcher.SendAttemptOutcome.Sent key -> IdleContinuationOutcome.Sent key
        | PromptDispatcher.SendAttemptOutcome.Superseded -> IdleContinuationOutcome.Superseded
        | PromptDispatcher.SendAttemptOutcome.Failed error -> IdleContinuationOutcome.Failed error

    /// HOST-004 + GLORY-029: idle-derived Manager encouragement with exact-terminal
    /// idempotency and no cross-terminal count limit.
    let private sendIdleManagerWithProfile
        (quiescence: SessionQuiescenceGate)
        (permit: QuiescencePermit)
        (sessionPort: ISessionHostPort)
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

        if rt.IdleAlreadyClaimed profile lifeId conditionKey terminalProviderRun then
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
                (liveDirectory directory)
                PromptDispatcher.AwaitMode.Detached
                (fun () -> quiescence.TryConsume permit)
            |> TaskValue.map idleOutcomeOfDispatch

    let trySendIdleManagerEncouragement
        (quiescence: SessionQuiescenceGate)
        (permit: QuiescencePermit)
        (sessionPort: ISessionHostPort)
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

    let private sendIdleRepairFamilyWithProfile
        (quiescence: SessionQuiescenceGate)
        (permit: QuiescencePermit)
        (sessionPort: ISessionHostPort)
        (sessionId: SessionId)
        (prompt: string)
        (directory: string option)
        (journal: AgentJournal option)
        (repairKind: string)
        (durable: AgentJournal)
        (profile: PromptAuthority.AuthorityExecutionProfile)
        : Task<IdleContinuationOutcome> =
        let rt = PromptDispatcher.forJournal durable

        match rt.RepairFamilyAdmission profile repairKind with
        | PromptAuthority.RepairFamilyAdmission.AlreadyAdmitted ->
            Task.FromResult IdleContinuationOutcome.AlreadyAdmitted
        | PromptAuthority.RepairFamilyAdmission.Available ->
            let agent = agentForActiveCursor journal sessionId profile

            rt.SendIdleRepairFamily
                sessionPort
                sessionId
                prompt
                repairKind
                profile
                agent
                (liveDirectory directory)
                PromptDispatcher.AwaitMode.Detached
                (fun () -> quiescence.TryConsume permit)
            |> TaskValue.map idleOutcomeOfDispatch

    /// Ordinary idle-derived repair: one send per LogicalRun + repair family.
    let trySendIdleRepairFamily
        (quiescence: SessionQuiescenceGate)
        (permit: QuiescencePermit)
        (sessionPort: ISessionHostPort)
        (sessionId: SessionId)
        (prompt: string)
        (directory: string option)
        (journal: AgentJournal option)
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
                    sendIdleRepairFamilyWithProfile
                        quiescence
                        permit
                        sessionPort
                        sessionId
                        prompt
                        directory
                        journal
                        repairKind
                        durable
                        profile
        }

    /// Blogger-request + terminal-scoped idle interaction repair. This narrower
    /// occasion identity distinguishes same-terminal re-entry from a new bad
    /// terminal without leaking repair budget across Blogger requests.
    let private sendIdleInteractionRepairWithProfile
        (quiescence: SessionQuiescenceGate)
        (permit: QuiescencePermit)
        (sessionPort: ISessionHostPort)
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
                (liveDirectory directory)
                PromptDispatcher.AwaitMode.Await
                (fun () -> quiescence.TryConsume permit)
            |> TaskValue.map idleOutcomeOfDispatch

    let trySendIdleInteractionRepair
        (quiescence: SessionQuiescenceGate)
        (permit: QuiescencePermit)
        (sessionPort: ISessionHostPort)
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
