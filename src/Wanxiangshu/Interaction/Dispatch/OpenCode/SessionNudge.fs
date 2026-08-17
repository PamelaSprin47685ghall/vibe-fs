namespace Wanxiangshu.Interaction.Dispatch.OpenCode

open Wanxiangshu.OpenCode
open Wanxiangshu.Change
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Resources
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

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
open Wanxiangshu.Interaction.Dispatch
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
open Wanxiangshu.Host
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

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

    let private continuationTargetValidation
        (profile: PromptAuthority.AuthorityExecutionProfile)
        (effectiveAgent: string)
        : Result<unit, string> =
        let pairValidation =
            if effectiveAgent <> profile.SelectedAgent && effectiveAgent <> profile.PeerAgent then
                Error "Assistance target is outside the active Authority Root agent pair"
            else
                Ok()

        pairValidation
        |> Result.bind (fun () ->
            PromptAuthority.parseAgentName effectiveAgent
            |> Result.bind (fun (_, role, _, _) ->
                if role <> profile.CanonicalRole then
                    Error "Assistance target role differs from the active Authority Root"
                else
                    Ok()))

    let private sendContinuationToValidatedAgent
        (sessionPort: ISessionHostPort)
        (sessionId: SessionId)
        (prompt: string)
        (kind: PromptAuthority.ContinuationKind)
        (effectiveAgent: string)
        (directory: string option)
        (awaitMode: PromptDispatcher.AwaitMode)
        (durable: AgentJournal)
        (profile: PromptAuthority.AuthorityExecutionProfile)
        : Task<Result<PromptKey, string>> =
        task {
            match continuationTargetValidation profile effectiveAgent with
            | Error error -> return Error error
            | Ok() ->
                let rt = PromptDispatcher.forJournal durable

                return!
                    rt.SendContinuation
                        sessionPort
                        sessionId
                        prompt
                        kind
                        profile
                        effectiveAgent
                        (liveDirectory directory)
                        awaitMode
                        None
        }

    /// PROMPT-018: assistance continuation with an explicit execution binding.
    /// This bypasses fallback cursor selection but not PromptAuthority. The target
    /// must be one of the Authority Root's immutable same-role pair.
    let sendContinuationToAgentResult
        (sessionPort: ISessionHostPort)
        (sessionId: SessionId)
        (prompt: string)
        (kind: PromptAuthority.ContinuationKind)
        (effectiveAgent: string)
        (directory: string option)
        (journal: AgentJournal option)
        (awaitMode: PromptDispatcher.AwaitMode)
        : Task<Result<PromptKey, string>> =
        task {
            match isFissionReplaced journal sessionId, journal, tryActiveProfile journal sessionId with
            | true, _, _ -> return Error "Session is retired by Fission"
            | false, None, _ -> return Error "No journal: an assistance continuation cannot be claimed"
            | false, Some _, None -> return Error "No active authority profile"
            | false, Some durable, Some profile ->
                return!
                    sendContinuationToValidatedAgent
                        sessionPort
                        sessionId
                        prompt
                        kind
                        effectiveAgent
                        directory
                        awaitMode
                        durable
                        profile
        }

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
        : Task<Result<PromptKey, string>> =
        let rt = PromptDispatcher.forJournal durable

        if rt.RepairAlreadyClaimed profile requestId terminalProviderRun repairKind then
            Task.FromResult(Error "Interaction repair already claimed for this provider run")
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
        : Task<Result<PromptKey, string>> =
        task {
            match isFissionReplaced journal sessionId, journal, tryActiveProfile journal sessionId with
            | true, _, _ -> return Error "Session is retired by Fission"
            | false, None, _ -> return Error "No journal: an interaction repair cannot be claimed"
            | false, Some _, None -> return Error "No active authority profile"
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

    [<RequireQualifiedAccess>]
    type RepairFamilySendOutcome =
        | Sent of PromptKey
        | BudgetExhausted
        | Retired
        | Failed of string

    let private sendRepairFamilyWithProfile
        (sessionPort: ISessionHostPort)
        (sessionId: SessionId)
        (prompt: string)
        (directory: string option)
        (journal: AgentJournal option)
        (repairKind: string)
        (durable: AgentJournal)
        (profile: PromptAuthority.AuthorityExecutionProfile)
        : Task<RepairFamilySendOutcome> =
        task {
            let rt = PromptDispatcher.forJournal durable

            if rt.RepairFamilyAlreadyClaimed profile repairKind then
                return RepairFamilySendOutcome.BudgetExhausted
            else
                let agent = agentForActiveCursor journal sessionId profile

                match!
                    rt.SendRepairFamily
                        sessionPort
                        sessionId
                        prompt
                        repairKind
                        profile
                        agent
                        (liveDirectory directory)
                        PromptDispatcher.AwaitMode.Detached
                        None
                with
                | Ok key -> return RepairFamilySendOutcome.Sent key
                | Error error -> return RepairFamilySendOutcome.Failed error
        }

    /// Ordinary interaction repair is bounded by LogicalRun + repair family.
    /// This is intentionally different from Blogger's terminal-scoped repair:
    /// a generic missing-final-report nudge must never be able to nudge its own
    /// bad response again under a new ProviderRunIdentity.
    let trySendRepairFamily
        (sessionPort: ISessionHostPort)
        (sessionId: SessionId)
        (prompt: string)
        (directory: string option)
        (journal: AgentJournal option)
        (repairKind: string)
        : Task<RepairFamilySendOutcome> =
        task {
            match isFissionReplaced journal sessionId, journal, tryActiveProfile journal sessionId with
            | true, _, _ -> return RepairFamilySendOutcome.Retired
            | false, None, _ ->
                return RepairFamilySendOutcome.Failed "No journal: an interaction repair cannot be claimed"
            | false, Some _, None -> return RepairFamilySendOutcome.Failed "No active authority profile"
            | false, Some durable, Some profile ->
                return!
                    sendRepairFamilyWithProfile
                        sessionPort
                        sessionId
                        prompt
                        directory
                        journal
                        repairKind
                        durable
                        profile
        }

    let private sendManagerIdleWithProfile
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
        : Task<Result<PromptKey, string>> =
        let rt = PromptDispatcher.forJournal durable

        if rt.IdleAlreadyClaimed profile lifeId conditionKey terminalProviderRun then
            Task.FromResult(Error "Manager idle encouragement already claimed for this terminal")
        else
            let agent = agentForActiveCursor journal sessionId profile

            // PROMPT-007 Detached: idle encouragement does not wait for PhysicalAccepted.
            rt.SendManagerIdleEncouragement
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
                None

    /// GLORY-029: one Manager idle encouragement per exact terminal occasion.
    /// Durable ClaimSequences dedupe duplicate delivery/restart replay of that
    /// terminal; fresh ProviderRun identities remain intentionally unbounded.
    let trySendManagerIdleEncouragement
        (sessionPort: ISessionHostPort)
        (sessionId: SessionId)
        (prompt: string)
        (directory: string option)
        (journal: AgentJournal option)
        (lifeId: ManagerLifeId)
        (conditionKey: string)
        (terminalProviderRun: ProviderRunIdentity)
        : Task<Result<PromptKey, string>> =
        task {
            match isFissionReplaced journal sessionId, journal, tryActiveProfile journal sessionId with
            | true, _, _ -> return Error "Session is retired by Fission"
            | false, None, _ -> return Error "No journal: a manager idle encouragement cannot be claimed"
            | false, Some _, None -> return Error "No active authority profile"
            | false, Some durable, Some profile ->
                return!
                    sendManagerIdleWithProfile
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
        | Failed of string

    let private idleOutcomeOfDispatch =
        function
        | PromptDispatcher.SendAttemptOutcome.Sent key -> IdleContinuationOutcome.Sent key
        | PromptDispatcher.SendAttemptOutcome.Superseded -> IdleContinuationOutcome.Superseded
        | PromptDispatcher.SendAttemptOutcome.Failed error -> IdleContinuationOutcome.Failed error

    /// The single generic idle-derived continuation helper. Preflight may await,
    /// but the permit itself is consumed only inside PromptDispatcher immediately
    /// before the real `SendPrompt` invocation.
    let trySendIdleContinuation
        (quiescence: SessionQuiescenceGate)
        (permit: QuiescencePermit)
        (sessionPort: ISessionHostPort)
        (sessionId: SessionId)
        (prompt: string)
        (kind: PromptAuthority.ContinuationKind)
        (directory: string option)
        (journal: AgentJournal option)
        : Task<IdleContinuationOutcome> =
        task {
            match isFissionReplaced journal sessionId, journal, tryActiveProfile journal sessionId with
            | true, _, _ -> return IdleContinuationOutcome.Failed "Session is retired by Fission"
            | false, None, _ ->
                return IdleContinuationOutcome.Failed "No journal: a continuation cannot be claimed"
            | false, Some _, None -> return IdleContinuationOutcome.Failed "No active authority profile"
            | false, Some durable, Some profile ->
                let agent = agentForActiveCursor journal sessionId profile
                let rt = PromptDispatcher.forJournal durable

                return!
                    rt.SendIdleContinuation
                        sessionPort
                        sessionId
                        prompt
                        kind
                        profile
                        agent
                        (liveDirectory directory)
                        PromptDispatcher.AwaitMode.Detached
                        None
                        (fun () -> quiescence.TryConsume permit)
                    |> TaskValue.map idleOutcomeOfDispatch
        }

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
            Task.FromResult(IdleContinuationOutcome.Failed "Manager idle encouragement already claimed for this terminal")
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
            | true, _, _ -> return IdleContinuationOutcome.Failed "Session is retired by Fission"
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

    [<RequireQualifiedAccess>]
    type IdleRepairFamilyOutcome =
        | Sent of PromptKey
        | Superseded
        | BudgetExhausted
        | Retired
        | Failed of string

    let private idleRepairOutcomeOfDispatch =
        function
        | PromptDispatcher.SendAttemptOutcome.Sent key -> IdleRepairFamilyOutcome.Sent key
        | PromptDispatcher.SendAttemptOutcome.Superseded -> IdleRepairFamilyOutcome.Superseded
        | PromptDispatcher.SendAttemptOutcome.Failed error -> IdleRepairFamilyOutcome.Failed error

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
        : Task<IdleRepairFamilyOutcome> =
        let rt = PromptDispatcher.forJournal durable

        if rt.RepairFamilyAlreadyClaimed profile repairKind then
            Task.FromResult IdleRepairFamilyOutcome.BudgetExhausted
        else
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
            |> TaskValue.map idleRepairOutcomeOfDispatch

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
        : Task<IdleRepairFamilyOutcome> =
        task {
            match isFissionReplaced journal sessionId, journal, tryActiveProfile journal sessionId with
            | true, _, _ -> return IdleRepairFamilyOutcome.Retired
            | false, None, _ ->
                return IdleRepairFamilyOutcome.Failed "No journal: an interaction repair cannot be claimed"
            | false, Some _, None -> return IdleRepairFamilyOutcome.Failed "No active authority profile"
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
            Task.FromResult(IdleContinuationOutcome.Failed "Interaction repair already claimed for this provider run")
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
            | true, _, _ -> return IdleContinuationOutcome.Failed "Session is retired by Fission"
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
