namespace Wanxiangshu.Next.Session

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.Identity

/// Companion work log snapshot (session-wide B / LatestB), not a single turn.
type WorkRecordSnapshot =
    { Text: string
      Digest: string
      Freshness: string
      CoveredThrough: string option }

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
type AgentCompletionPayload =
    {
        AgentId: string
        ChildSessionId: SessionId option
        RunId: string
        Role: AgentRole
        AuthorityRoot: AuthorityRootUserMessageId option
        ProviderRun: ProviderRunIdentity option
        /// Session-wide A for the child Session: formal text + reasoning/thinking.
        FinalText: string
        /// Session-wide companion work log (B / LatestB) when available.
        WorkRecord: WorkRecordSnapshot option
        /// The worktree this child ran in, when it has one.
        Directory: string option
    }

type AgentCompletionOutcome =
    | AgentCompleted of AgentCompletionPayload
    | AgentFailed of AgentFailurePayload
    | AgentAborted of AgentFailurePayload

module AgentCompletion =
    let text (outcome: AgentCompletionOutcome) =
        match outcome with
        | AgentCompleted payload -> payload.FinalText
        | AgentFailed payload
        | AgentAborted payload -> payload.Message

    let status (outcome: AgentCompletionOutcome) =
        match outcome with
        | AgentCompleted _ -> "completed"
        | AgentFailed _ -> "failed"
        | AgentAborted _ -> "aborted"

    let isCompleted (outcome: AgentCompletionOutcome) =
        match outcome with
        | AgentCompleted payload -> not (String.IsNullOrWhiteSpace payload.FinalText)
        | _ -> false

    let completed
        (agentId: string)
        (childSessionId: SessionId)
        (runId: string)
        (role: AgentRole)
        (authorityRoot: AuthorityRootUserMessageId)
        (providerRun: ProviderRunIdentity)
        (finalText: string)
        (workRecord: WorkRecordSnapshot option)
        (directory: string option)
        =
        AgentCompleted
            { AgentId = agentId
              ChildSessionId = Some childSessionId
              RunId = runId
              Role = role
              AuthorityRoot = Some authorityRoot
              ProviderRun = Some providerRun
              FinalText = finalText
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
    let ofSimpleText (agentId: string) (runId: string) (role: AgentRole) (text: string) =
        AgentCompleted
            { AgentId = agentId
              ChildSessionId = None
              RunId = runId
              Role = role
              AuthorityRoot = None
              ProviderRun = None
              FinalText = text
              WorkRecord = None
              Directory = None }

    let ofSimpleError (agentId: string) (runId: string) (role: AgentRole) (message: string) =
        failed agentId runId (Some role) None "ERROR" message

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

    let snapshotFromText (text: string) : WorkRecordSnapshot =
        let bytes = System.Text.Encoding.UTF8.GetBytes text
        let mutable hash = 2166136261u

        for b in bytes do
            hash <- (hash ^^^ uint32 b) * 16777619u

        { Text = text
          Digest = sprintf "fnv1a:%08x" hash
          Freshness = "current"
          CoveredThrough = None }

    let snapshotOption (text: string option) : WorkRecordSnapshot option =
        match text with
        | Some value when not (System.String.IsNullOrWhiteSpace value) -> Some(snapshotFromText value)
        | _ -> None

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
