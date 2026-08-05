namespace Wanxiangshu.Session

open System
open System.Threading.Tasks
open Wanxiangshu.Kernel.Identity

type AgentFailurePayload =
    {
        AgentId: string
        /// Typed, matching the completed payload. It was `string option` beside a
        /// `SessionId option`, so the same concept had two types across two cases of
        /// one union.
        ChildSessionId: SessionId option
        RunId: string
        Role: AgentRole option
        Code: string
        Message: string
    }

/// One finished child run, as `join` reports it (EXEC-004).
///
/// The four identities are `option` because a PTY run genuinely has none of them:
/// it owns no Host child session, no Authority Root, no provider run, no worktree.
/// They used to be `string` filled with `""`, which made "absent" and "the empty
/// string" the same value — the sentinel PROMPT-001 exists to remove — and forced
/// every reader to re-test for blankness to find out which it had.
///
/// Typed, not `string option`: HOST-010 makes the terminal provider run the
/// assistant message id, and PROMPT-002 makes the root a promoted physical
/// message. `AssistantMessageId` was a second name for the former.
///
/// COMPANION-003: `WorkRecord` IS the final LifecycleWorkRecord — Opening + Y
/// frames + X gap + terminal. The old `FinalText` (session-wide A) and
/// `WorkRecordSnapshot` (B with digest/freshness/coverage metadata) are both gone:
/// one self-contained record replaces the pair, and runtime-only metadata never
/// reaches the LLM-visible wire (EXEC-004).
type AgentCompletionPayload =
    {
        AgentId: string
        ChildSessionId: SessionId option
        RunId: string
        Role: AgentRole
        AuthorityRoot: AuthorityRootUserMessageId option
        ProviderRun: ProviderRunIdentity option
        /// The final LifecycleWorkRecord, materialised at terminal.
        WorkRecord: string
        /// The worktree this child ran in, when it has one.
        Directory: string option
    }

type AgentCompletionOutcome =
    | AgentCompleted of AgentCompletionPayload
    | AgentFailed of AgentFailurePayload
    | AgentAborted of AgentFailurePayload
    /// EXEC-009: durable HandleAbandoned reported once in a join [[result]] batch.
    /// Flat wire: status="abandoned", agent, reason — not nested [error].
    | AgentAbandoned of agentId: string * reason: string

module AgentCompletion =
    let text (outcome: AgentCompletionOutcome) =
        match outcome with
        | AgentCompleted payload -> payload.WorkRecord
        | AgentFailed payload
        | AgentAborted payload -> payload.Message
        | AgentAbandoned(_, reason) -> reason

    let status (outcome: AgentCompletionOutcome) =
        match outcome with
        | AgentCompleted _ -> "completed"
        | AgentFailed _ -> "failed"
        | AgentAborted _ -> "aborted"
        | AgentAbandoned _ -> "abandoned"

    let isCompleted (outcome: AgentCompletionOutcome) =
        match outcome with
        | AgentCompleted payload -> not (String.IsNullOrWhiteSpace payload.WorkRecord)
        | _ -> false

    let completed
        (agentId: string)
        (childSessionId: SessionId)
        (runId: string)
        (role: AgentRole)
        (authorityRoot: AuthorityRootUserMessageId)
        (providerRun: ProviderRunIdentity)
        (workRecord: string)
        (directory: string option)
        =
        AgentCompleted
            { AgentId = agentId
              ChildSessionId = Some childSessionId
              RunId = runId
              Role = role
              AuthorityRoot = Some authorityRoot
              ProviderRun = Some providerRun
              WorkRecord = workRecord
              Directory = directory }

    let failed
        (agentId: string)
        (runId: string)
        (role: AgentRole option)
        (childSessionId: SessionId option)
        code
        message
        =
        AgentFailed
            { AgentId = agentId
              ChildSessionId = childSessionId
              RunId = runId
              Role = role
              Code = code
              Message = message }

    let aborted
        (agentId: string)
        (runId: string)
        (role: AgentRole option)
        (childSessionId: SessionId option)
        code
        message
        =
        AgentAborted
            { AgentId = agentId
              ChildSessionId = childSessionId
              RunId = runId
              Role = role
              Code = code
              Message = message }

    /// A PTY or local run that has no Host child session and no Authority Root.
    ///
    /// The four identity arguments are absent rather than blank. `completed` used to
    /// be called here with `"" "" ""` for ChildSessionId, root and directory, which
    /// made "this run has no child session" and "its session id is the empty string"
    /// the same value — the sentinel pattern PROMPT-001 exists to remove.
    ///
    /// The work record is the run's plain text: a PTY has no LWR, and its completion
    /// schema stays deliberately minimal (EXEC-004).
    let ofSimpleText (agentId: string) (runId: string) (role: AgentRole) (text: string) =
        AgentCompleted
            { AgentId = agentId
              ChildSessionId = None
              RunId = runId
              Role = role
              AuthorityRoot = None
              ProviderRun = None
              WorkRecord = text
              Directory = None }

    let ofSimpleError (agentId: string) (runId: string) (role: AgentRole) (message: string) =
        failed agentId runId (Some role) None "ERROR" message

    let abandoned (agentId: string) (reason: string) = AgentAbandoned(agentId, reason)

    let withRunIdentity (agentId: string) (runId: string) (role: AgentRole) (outcome: AgentCompletionOutcome) =
        match outcome with
        | AgentCompleted payload ->
            AgentCompleted
                { payload with
                    RunId = runId
                    AgentId = agentId
                    Role = role }
        | AgentFailed payload ->
            AgentFailed
                { payload with
                    RunId = runId
                    AgentId = agentId
                    Role = Some role }
        | AgentAborted payload ->
            AgentAborted
                { payload with
                    RunId = runId
                    AgentId = agentId
                    Role = Some role }
        | AgentAbandoned(_, reason) -> AgentAbandoned(agentId, reason)

/// A completed (or failed/aborted) agent run.
///
/// Future: AgentId will be removed in P6 since the agent identity is the
/// key in ForkRuntime's Map<string, ChildRun>. AgentName is the preferred
/// field for consumer code that needs the managed agent name.
type RunCompletion =
    {
        /// Unique identity for this run attempt.
        RunId: string

        /// DEPRECATED: The agentId that owns this completion. Kept for HostFork*
        /// backward compatibility. New code should use the Map key or AgentName.
        AgentId: string

        /// The managed agent name (e.g. "fast-coder", "deep-reviewer").
        AgentName: string

        /// Canonical role of the agent.
        Role: AgentRole

        /// The completion outcome (completed/failed/aborted payload).
        Outcome: AgentCompletionOutcome

        /// When the run reached terminal state.
        CompletedAt: DateTimeOffset
    }
