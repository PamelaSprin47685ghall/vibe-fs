namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Change.Host
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Session.Recovery.SessionRecovery
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Mission.Review
open Wanxiangshu.Persistence.Journal

/// Owns every per-session tool runtime.
///
/// AGENT-007: Role comes from the Authority Root's CanonicalRole and nothing
/// else. The previous version consulted three sources in order — authority, a
/// per-session role cache, then the Host context's `agent` field — so a session
/// whose authority said Coder could still be gated as DevOps because a cache
/// entry or a message field said so. Two of the three are gone.
type ToolRuntimeScope =
    new:
        sessions: ISessionHostPort *
        journal: AgentJournal option *
        gitTreePort: GitTreePort option *
        workspaceDirectory: string option *
        sessionParents: Dictionary<string, string> *
        currentPhysicalUserMessage: (string -> string option) *
        verdictSubmissions: HashSet<string> *
        sessionDirectories: Dictionary<string, string> *
        onRunStarted: (SessionId -> Role -> string option -> unit) option *
        parentWorkRecordFor: (string -> Task<string option>) option *
        childWorkRecordFor: (string -> Task<string option>) option *
        snapshot: ISessionSnapshotPort option *
        cancelSignals: (SessionId seq -> unit) option *
        ?eventPort: IEventObservationPort *
        ?finalityReviewerTimeoutMs: int ->
            ToolRuntimeScope

    interface ISessionRuntimeOwner
    interface IDisposable

    member FinalityReviewerTimeoutMs: int option
    member Sessions: ISessionHostPort
    member Journal: AgentJournal option
    member Snapshot: ISessionSnapshotPort option
    member EventPort: IEventObservationPort option
    member WorkspaceDirectory: string option
    member ActiveProfileFor: sessionId: SessionId -> PromptAuthority.AuthorityExecutionProfile option
    /// GLORY-003: the run-started callback wired by the plugin bootstrap, exposed
    /// so the Finality workflow's hidden Reviewer binds the same reconciler.
    member RunStarted: (SessionId -> Role -> string option -> unit)

    /// EXEC-011: the administrator's ceiling on any single process.
    ///
    /// Resolved once per scope so every executor call in a session shares one
    /// ceiling. A non-positive or unparseable setting falls back to the default
    /// rather than being treated as "no limit": the clause requires the hard limit
    /// to be finite, so an unreadable configuration must not widen it.
    member ProcessHardLimit: TimeSpan

    member SessionParents: Dictionary<string, string>
    member CurrentPhysicalUserMessage: sessionId: string -> string option
    member DirectoryFor: sessionId: string -> string option
    member LogicalOwnerFor: sessionId: SessionId -> SessionId

    member RegisterPhysicalParent: sessionId: SessionId * parentId: SessionId option -> unit

    member ParentWorkRecordFor: sessionId: string -> Task<string option>
    member ChildWorkRecordFor: sessionId: string -> Task<string option>

    member RegisterDirectory: sessionId: string * path: string -> unit

    member RoleFor: ctx: HostToolContext -> Role option
    member EnsureRoleFor: ctx: HostToolContext -> Task<Role option>

    /// AGENT-013 + PROMPT-008: the managed agent a PTY is opened for.
    member ManagedAgentFor: ctx: HostToolContext -> ManagedAgent option

    member IsRole: ctx: HostToolContext * expected: Role -> bool

    /// Wire PluginRuntimeScope.RequireFamilyRecovery (or test double).
    member AttachFamilyRecovery: fn: (SessionId -> Task<FamilyRecovery>) -> unit

    /// EXEC-017: share PluginRuntimeScope.JoinAttempts with JoinTool.
    member AttachJoinAttempts: registry: IJoinAttemptRegistry -> unit

    member JoinAttempts: IJoinAttemptRegistry

    /// P0-RECOVERY-JOIN-001: join / JoinPublishedAvailable require FamilyReady. Missing attach → FamilyBlocked.
    member RequireFamilyRecovery: root: SessionId -> Task<FamilyRecovery>

    member RuntimeFor: ctx: HostToolContext -> Result<HostForkRuntime, string>

    /// CRASH-018: process-local adoption for explicit /continue. The durable
    /// handle stays byte-for-byte as it was at the crash boundary; a later LLM
    /// fork reuse is the first action allowed to reopen it durably.
    member AdoptExistingChild: parentSessionId: SessionId * record: HandleRecord -> Result<unit, string>

    member ExecutorRuntimeFor: ctx: HostToolContext -> HostForkRuntime

    member OrchestratorHostFor: sessionId: string -> OrchestratorHost

    member TreePortFor: reviewerId: string -> GitTreePort option

    member MarkVerdictSubmitted: reviewerId: string * physicalUserMessageId: PhysicalUserMessageId -> unit

    member HasVerdictSubmitted: reviewerId: string * physicalUserMessageId: PhysicalUserMessageId -> bool

    member RunOwnedWork: start: (unit -> Task) -> bool

    member DisposeExecutorRuntime: sessionId: string -> Task

    /// EXEC-016: live PTY on the parent fork runtime (not Executor runtime).
    member HasLivePty: sessionId: string -> bool

    member CancelSessionChildren: sessionId: string -> Task

    /// MANAGED-SESSION-017: a successor-less internal stop is a synchronous
    /// logical termination CE. Failed delivery is what completes the durable
    /// fork handle and wakes the parent; no future TurnAborted callback carries
    /// workflow continuation state.
    member TerminateSession: sessionId: string * reason: string -> Task<Result<unit, string>>

    member DisposeSession: sessionId: string -> Task

    member DisposeAsync: unit -> Task

    member Dispose: unit -> unit
