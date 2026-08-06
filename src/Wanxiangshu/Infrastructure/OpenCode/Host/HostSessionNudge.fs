namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Host
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Journal
open Wanxiangshu.Session

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
        |> Option.map (fun j ->
            DurableFallback.effectiveAgentForActiveCursor sessionId (AgentJournal.snapshot j) profile)
        |> Option.defaultValue profile.SelectedAgent

    /// Reconciled linked children have a host-proven root user message even when
    /// the host omitted agent metadata from `chat.message`. Register that real
    /// AgentOwner authority once; never use this for an unlinked/unknown session.
    /// `agent` must be a Managed Agent name (fast-* / deep-*).
    let ensureAgentOwnerAuthority
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (rootUserMessageId: PhysicalUserMessageId)
        (agent: string)
        : Result<unit, string> =
        match journal with
        | None -> Error "No journal: an Authority Root cannot be accepted without somewhere to record it"
        | Some durable ->
            match tryActiveProfile journal sessionId with
            | Some _ -> Ok()
            | None ->
                let rt = PromptDispatcher.forJournal durable

                PromptAuthorityRun.createAuthorityRoot
                    HostDigest.sha256Hex
                    rt.RuntimeId
                    sessionId
                    PromptAuthority.RootAuthorityKind.AgentOwnerRoot
                    rootUserMessageId
                    agent
                |> Result.bind rt.RegisterAuthority

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
            match journal, tryActiveProfile journal sessionId with
            | None, _ -> return Error "No journal: a continuation cannot be claimed"
            | Some _, None -> return Error "No active authority profile"
            | Some durable, Some profile ->
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
            match journal, tryActiveProfile journal sessionId with
            | None, _ -> return Error "No journal: an interaction repair cannot be claimed"
            | Some _, None -> return Error "No active authority profile"
            | Some durable, Some profile ->
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
