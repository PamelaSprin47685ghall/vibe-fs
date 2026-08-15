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
                if effectiveAgent <> profile.SelectedAgent && effectiveAgent <> profile.PeerAgent then
                    return Error "Assistance target is outside the active Authority Root agent pair"
                else
                    match PromptAuthority.parseAgentName effectiveAgent with
                    | Error error -> return Error error
                    | Ok(_, role, _, _) when role <> profile.CanonicalRole ->
                        return Error "Assistance target role differs from the active Authority Root"
                    | Ok _ ->
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

    /// FALLBACK-008: an empty / XML-only terminal earns at most one repair.
    ///
    /// `terminalProviderRun` is the provider run that produced the unusable
    /// terminal, which is what FALLBACK-008 counts against — not the session and
    /// not the Logical Run, both of which would let one bad run consume or reset
    /// another's budget.
    ///
    /// The budget check is a read of durable `ClaimSequences`, so a repair claimed
    /// before a crash is still spent after it.
    let trySendInteractionRepair
        (sessionPort: ISessionHostPort)
        (sessionId: SessionId)
        (prompt: string)
        (directory: string option)
        (journal: AgentJournal option)
        (terminalProviderRun: ProviderRunIdentity)
        (repairKind: string)
        : Task<Result<PromptKey, string>> =
        task {
            match isFissionReplaced journal sessionId, journal, tryActiveProfile journal sessionId with
            | true, _, _ -> return Error "Session is retired by Fission"
            | false, None, _ -> return Error "No journal: an interaction repair cannot be claimed"
            | false, Some _, None -> return Error "No active authority profile"
            | false, Some durable, Some profile ->
                let rt = PromptDispatcher.forJournal durable

                if rt.RepairAlreadyClaimed profile terminalProviderRun repairKind then
                    return Error "Interaction repair already claimed for this provider run"
                else
                    let agent = agentForActiveCursor journal sessionId profile

                    // PROMPT-007 Detached: repair does not wait for PhysicalAccepted.
                    return!
                        rt.SendInteractionRepair
                            sessionPort
                            sessionId
                            prompt
                            terminalProviderRun
                            repairKind
                            profile
                            agent
                            (liveDirectory directory)
                            PromptDispatcher.AwaitMode.Detached
                            None
        }

    /// GLORY-029: one Manager idle encouragement per (Life, trigger ProviderRun).
    ///
    /// Budget is durable ClaimSequences, not a session-wide PendingClaims scan:
    /// Detached keeps A's claim pending until PhysicalAccepted, and that must not
    /// suppress B's independent occasion.
    let trySendManagerIdleEncouragement
        (sessionPort: ISessionHostPort)
        (sessionId: SessionId)
        (prompt: string)
        (directory: string option)
        (journal: AgentJournal option)
        (lifeId: ManagerLifeId)
        (triggerProviderRun: ProviderRunIdentity)
        : Task<Result<PromptKey, string>> =
        task {
            match isFissionReplaced journal sessionId, journal, tryActiveProfile journal sessionId with
            | true, _, _ -> return Error "Session is retired by Fission"
            | false, None, _ -> return Error "No journal: a manager idle encouragement cannot be claimed"
            | false, Some _, None -> return Error "No active authority profile"
            | false, Some durable, Some profile ->
                let rt = PromptDispatcher.forJournal durable

                if rt.IdleAlreadyClaimed profile lifeId triggerProviderRun then
                    return Error "Manager idle encouragement already claimed for this occasion"
                else
                    let agent = agentForActiveCursor journal sessionId profile

                    // PROMPT-007 Detached: idle encouragement does not wait for PhysicalAccepted.
                    return!
                        rt.SendManagerIdleEncouragement
                            sessionPort
                            sessionId
                            prompt
                            lifeId
                            triggerProviderRun
                            profile
                            agent
                            (liveDirectory directory)
                            PromptDispatcher.AwaitMode.Detached
                            None
        }

    // ── idle-derived continuation admission（HOST-004）────────────────────────

    /// What an idle-derived send attempt came to.
    [<RequireQualifiedAccess>]
    type IdleContinuationOutcome =
        | Sent of PromptKey
        /// The idle occasion expired before the physical send (a newer provider
        /// attempt began, or the session was dropped). Not an error, not a
        /// terminal failure: nothing was claimed and nothing was sent — the
        /// system is doing something fresher.
        | Superseded
        | Failed of string

    /// The single idle-derived continuation helper: `TryConsume` first, then the
    /// dispatcher chain immediately (no await in between), so the send boundary
    /// is as tight as the claim/persist/send path allows. `Superseded` never
    /// writes `PluginPromptClaimed` and never sends.
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
            if not (quiescence.TryConsume permit) then
                return IdleContinuationOutcome.Superseded
            else
                match!
                    sendContinuationResult
                        sessionPort
                        sessionId
                        prompt
                        kind
                        directory
                        journal
                        PromptDispatcher.AwaitMode.Detached
                        None
                with
                | Ok key -> return IdleContinuationOutcome.Sent key
                | Error error -> return IdleContinuationOutcome.Failed error
        }

    /// HOST-004 + GLORY-029: idle-derived Manager encouragement with occasion digest.
    /// Same admission contract as `trySendIdleContinuation` / interaction repair.
    let trySendIdleManagerEncouragement
        (quiescence: SessionQuiescenceGate)
        (permit: QuiescencePermit)
        (sessionPort: ISessionHostPort)
        (sessionId: SessionId)
        (prompt: string)
        (directory: string option)
        (journal: AgentJournal option)
        (lifeId: ManagerLifeId)
        (triggerProviderRun: ProviderRunIdentity)
        : Task<IdleContinuationOutcome> =
        task {
            if not (quiescence.TryConsume permit) then
                return IdleContinuationOutcome.Superseded
            else
                match!
                    trySendManagerIdleEncouragement
                        sessionPort
                        sessionId
                        prompt
                        directory
                        journal
                        lifeId
                        triggerProviderRun
                with
                | Ok key -> return IdleContinuationOutcome.Sent key
                | Error error -> return IdleContinuationOutcome.Failed error
        }

    /// The single idle-derived interaction repair helper (missing-final-report /
    /// interaction-repair). Same admission contract as `trySendIdleContinuation`.
    let trySendIdleInteractionRepair
        (quiescence: SessionQuiescenceGate)
        (permit: QuiescencePermit)
        (sessionPort: ISessionHostPort)
        (sessionId: SessionId)
        (prompt: string)
        (directory: string option)
        (journal: AgentJournal option)
        (terminalProviderRun: ProviderRunIdentity)
        (repairKind: string)
        : Task<IdleContinuationOutcome> =
        task {
            if not (quiescence.TryConsume permit) then
                return IdleContinuationOutcome.Superseded
            else
                match!
                    trySendInteractionRepair
                        sessionPort
                        sessionId
                        prompt
                        directory
                        journal
                        terminalProviderRun
                        repairKind
                with
                | Ok key -> return IdleContinuationOutcome.Sent key
                | Error error -> return IdleContinuationOutcome.Failed error
        }
