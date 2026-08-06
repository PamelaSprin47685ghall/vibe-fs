namespace Wanxiangshu.Session

open System
open System.Threading.Tasks
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

type AgentFailurePayload =
    {
        AgentId: string
        /// Typed, matching the completed payload. It was `string option` beside a
        /// `SessionId option`, so the same concept had two types across two cases of
        /// one union.
        ChildSessionId: SessionId option
        RunId: string
        Role: Role option
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
        Role: Role
        AuthorityRoot: AuthorityRootUserMessageId option
        ProviderRun: ProviderRunIdentity option
        /// The final LifecycleWorkRecord, materialised at terminal.
        WorkRecord: string
        /// The worktree this child ran in, when it has one.
        Directory: string option
    }

/// Agent join finality only (EXEC-020): Completed | Failed | Abandoned.
/// Host abort is observation, not agent terminal.
type AgentCompletionOutcome =
    | AgentCompleted of AgentCompletionPayload
    | AgentFailed of AgentFailurePayload
    /// EXEC-009: durable HandleAbandoned reported once in a join [[result]] batch.
    /// Flat wire: status="abandoned", agent, reason — not nested [error].
    | AgentAbandoned of agentId: string * reason: string

/// PTY exit payload (kind=pty, status=completed).
type PtyExit =
    { PtyId: string
      Outcome: string
      Closed: bool }

/// PTY failure payload (kind=pty, status=failed).
type PtyFailure =
    { PtyId: string
      Outcome: string
      Closed: bool
      Code: string
      Message: string }

/// PTY abort payload (kind=pty, status=aborted) — PTY-only; agent path forbids aborted.
type PtyAbort =
    { PtyId: string
      Outcome: string
      Closed: bool
      Code: string
      Message: string }

/// Agent item inside a join [[result]] batch. No aborted branch.
type AgentJoinItem =
    | AgentCompletedItem of AgentCompletionPayload
    | AgentFailedItem of AgentFailurePayload
    | AgentAbandonedItem of agentId: string * reason: string

/// PTY item inside a join [[result]] batch. Aborted is legal for PTY only.
type PtyJoinItem =
    | PtyExited of PtyExit
    | PtyFailed of PtyFailure
    | PtyAborted of PtyAbort

/// One join batch item: agent or PTY.
type JoinItem =
    | AgentItem of AgentJoinItem
    | PtyItem of PtyJoinItem

module AgentCompletion =
    let text (outcome: AgentCompletionOutcome) =
        match outcome with
        | AgentCompleted payload -> payload.WorkRecord
        | AgentFailed payload -> payload.Message
        | AgentAbandoned(_, reason) -> reason

    let status (outcome: AgentCompletionOutcome) =
        match outcome with
        | AgentCompleted _ -> "completed"
        | AgentFailed _ -> "failed"
        | AgentAbandoned _ -> "abandoned"

    let isCompleted (outcome: AgentCompletionOutcome) =
        match outcome with
        | AgentCompleted payload -> not (String.IsNullOrWhiteSpace payload.WorkRecord)
        | _ -> false

    let completed
        (agentId: string)
        (childSessionId: SessionId)
        (runId: string)
        (role: Role)
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

    let failed (agentId: string) (runId: string) (role: Role option) (childSessionId: SessionId option) code message =
        AgentFailed
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
    let ofSimpleText (agentId: string) (runId: string) (role: Role) (text: string) =
        AgentCompleted
            { AgentId = agentId
              ChildSessionId = None
              RunId = runId
              Role = role
              AuthorityRoot = None
              ProviderRun = None
              WorkRecord = text
              Directory = None }

    let ofSimpleError (agentId: string) (runId: string) (role: Role) (message: string) =
        failed agentId runId (Some role) None "ERROR" message

    let abandoned (agentId: string) (reason: string) = AgentAbandoned(agentId, reason)

    let withRunIdentity (agentId: string) (runId: string) (role: Role) (outcome: AgentCompletionOutcome) =
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
        | AgentAbandoned(_, reason) -> AgentAbandoned(agentId, reason)

/// One agent or PTY run completion cell for join wire / JoinDrain materialisation.
/// GREEN-5: agent facts live in Journal; mailbox agent channel is wake-only.
/// PTY facts live in mailbox as PtyJoinItem and project here for Join API.
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
        Role: Role

        /// Agent finality only: completed | failed | abandoned (no aborted).
        Outcome: AgentCompletionOutcome

        /// When the run reached terminal state.
        CompletedAt: DateTimeOffset
    }

/// PTY mailbox helpers (GREEN-5): physical item ↔ join wire projection.
module PtyJoinItem =
    let ptyId (item: PtyJoinItem) =
        match item with
        | PtyExited e -> e.PtyId
        | PtyFailed f -> f.PtyId
        | PtyAborted a -> a.PtyId

    /// Compat projection into RunCompletion for JoinWithPermit / ExecutorSummarize /
    /// CompletionMailbox.Join only. Production JoinTool batch path keeps PtyJoinItem
    /// (HostForkRuntime → JoinItem → renderer) so aborted is not lost on wire.
    /// PtyAborted projects with Code = "PTY_ABORTED" so ofRunCompletion can recover it;
    /// never map abort to a generic business AgentFailed without that discriminant.
    let abortedCode = "PTY_ABORTED"

    let toRunCompletion (item: PtyJoinItem) : RunCompletion =
        let id = ptyId item
        let role = Role.DevOps

        match item with
        | PtyExited e ->
            { RunId = id
              AgentId = id
              AgentName = id
              Role = role
              Outcome = AgentCompletion.ofSimpleText id id role e.Outcome
              CompletedAt = DateTimeOffset.UtcNow }
        | PtyFailed f ->
            { RunId = id
              AgentId = id
              AgentName = id
              Role = role
              Outcome = AgentCompletion.failed id id (Some role) None f.Code f.Message
              CompletedAt = DateTimeOffset.UtcNow }
        | PtyAborted a ->
            { RunId = id
              AgentId = id
              AgentName = id
              Role = role
              Outcome =
                AgentCompletion.failed
                    id
                    id
                    (Some role)
                    None
                    abortedCode
                    (if String.IsNullOrWhiteSpace a.Message then
                         a.Outcome
                     else
                         a.Message)
              CompletedAt = DateTimeOffset.UtcNow }

/// Project RunCompletion into typed JoinItem (agent vs PTY).
module JoinItem =
    /// Agent durable → AgentItem. PTY via isPtyRun; Code=PTY_ABORTED recovers PtyAborted.
    let ofRunCompletion (isPtyRun: bool) (completion: RunCompletion) : JoinItem =
        if isPtyRun then
            match completion.Outcome with
            | AgentCompleted payload ->
                PtyItem(
                    PtyExited
                        { PtyId = completion.RunId
                          Outcome = payload.WorkRecord
                          Closed = true }
                )
            | AgentFailed payload when payload.Code = PtyJoinItem.abortedCode ->
                PtyItem(
                    PtyAborted
                        { PtyId = completion.RunId
                          Outcome = payload.Message
                          Closed = true
                          Code = payload.Code
                          Message = payload.Message }
                )
            | AgentFailed payload ->
                PtyItem(
                    PtyFailed
                        { PtyId = completion.RunId
                          Outcome = payload.Message
                          Closed = true
                          Code = payload.Code
                          Message = payload.Message }
                )
            | AgentAbandoned(_, reason) ->
                // PTY type has no abandoned case; surface as failed with ABANDONED code.
                PtyItem(
                    PtyFailed
                        { PtyId = completion.RunId
                          Outcome = reason
                          Closed = true
                          Code = "ABANDONED"
                          Message = reason }
                )
        else
            match completion.Outcome with
            | AgentCompleted payload -> AgentItem(AgentCompletedItem payload)
            | AgentFailed payload -> AgentItem(AgentFailedItem payload)
            | AgentAbandoned(agentId, reason) -> AgentItem(AgentAbandonedItem(agentId, reason))

    /// Direct wrap: agent RunCompletion without PTY classification.
    let ofAgentRunCompletion (completion: RunCompletion) : JoinItem = ofRunCompletion false completion

    /// PTY mailbox fact stays PtyJoinItem through join wire (EXEC-020).
    let ofPtyJoinItem (item: PtyJoinItem) : JoinItem = PtyItem item
