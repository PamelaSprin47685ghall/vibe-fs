namespace Wanxiangshu.Enforcer

open System.Threading.Tasks
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

/// Session termination capability — same signature as PluginTransforms.fs private type.
type SessionTermination = SessionId -> string -> Task<Result<unit, string>>

/// The three continuation branches of EnforcerHost.handleContinuation (the
/// Blogger continuation-transform host), extracted so EnforcerHost stays a thin
/// dispatcher (ENFORCER-044).
///
/// Branch 1 (emptyCallsBranch): pending/running blog, abort cleanup, pure prose
/// terminal, and AABB repair — nothing is committed here. The first physical
/// protocol nudge is idle-owned; transform must never queue one behind a live
/// Host tool loop.
/// Branch 2 (commitBranch): ENFORCER-044 merge/commit on completed blog tool
/// parts, then drain / park / inject-repair disposition.
/// Branch 3 (firstRequestBranch): COMPANION-005 first request / non-tool step —
/// rebuild only from durable frames + typed CurrentRequest.
module EnforcerContinuation =

    /// Local outcome of one continuation cycle body (no program-counter bools).
    [<RequireQualifiedAccess>]
    type CycleDisposition =
        | Working
        | Committed of afterSquashMain: BloggerRequestContext option
        | InjectRepair of BloggerRequestContext
        | CommitUnknown
        | AbandonThenCatchUp

    /// Continuation transform result. Empty message lists are forbidden: Host
    /// forwards them as provider `messages` and rejects with 400.
    /// StopPhysicalRun asks the plugin to interrupt only the current physical attempt after projecting messages.
    [<RequireQualifiedAccess>]
    type ContinuationOutcome =
        | ProjectMessages of obj list
        | StopPhysicalRun of messages: obj list * reason: string

    /// ENFORCER-153 / DSL-003: the recovery stage probe, injected by the caller
    /// (Application layer owns the derivation; Session cannot reference it by
    /// compile order). Derived from the durable repair claim + provider-visible
    /// transcript on every read — recovery is never stored on a runtime cell
    /// mirror, and this module must never grow one.
    type RecoveryStageProbe = BloggerRequestContext -> BloggerToolRecovery

    /// Closed context shared by the branches: EnforcerHost (the thin dispatcher)
    /// injects every dependency the branch bodies touch, so the branches are pure
    /// transforms with no ambient module state.
    type Context =
        { Scope: IBloggerRuntimeHost
          Journal: AgentJournal option
          Durable: AgentJournal
          Owner: SessionId
          BloggerSessionId: SessionId
          RawMessages: obj list
          RecoveryProbe: AgentJournal -> SessionId -> obj list -> RecoveryStageProbe
          Project: obj list -> ContinuationOutcome
          Stop: string -> ContinuationOutcome
          RefreshMainContext: SessionId -> SessionId -> Task<BloggerRequestContext option>
          IsEmptyTextCycleFailure: string -> bool }

    val key: ctx: Context -> string

    val invalidCardinalityBranch:
        ctx: Context -> messageId: string -> callCount: int -> assistantCompleted: bool -> Task<ContinuationOutcome>

    /// Branch 1 — empty completed-blog list. Host transform msgs do NOT include
    /// the newly created outbound assistant (prompt.ts: updateMessage then
    /// trigger transform on prior msgs). lastAssistant = historical tail. An
    /// empty completed-blog list means:
    /// 1) pending/running blog — Host re-enters after tool completion
    /// 2) abort cleanup: blog status=error+interrupted, assistant completed
    ///    → NOT pure prose; fail closed if still InFlight
    /// 3) outbound after prior success is non-empty EnforcerCycleDecode.extractCalls (other arm)
    /// 4) pure prose terminal (no blog parts at all) — ENFORCER-060 once when live
    /// 5) no live request + interrupted/prose terminal → stop, never invent repair
    val emptyCallsBranch: ctx: Context -> assistantCompleted: bool -> Task<ContinuationOutcome>

    /// Branch 2 — ENFORCER-044: merge/commit on completed blog tool parts when
    /// this plugin owns the cycle (live CurrentRequest).
    ///
    /// Host prompt.ts: transform msgs do NOT include the newly created
    /// outbound assistant — lastAssistant is always the previous one.
    /// processor.cleanup sets time.completed AFTER tools finish and BEFORE
    /// the next loop iteration reloads msgs and re-triggers transform.
    /// So the only Host trajectory that shows blog tool status=completed
    /// also has assistant.time.completed. Skipping commit on that flag
    /// freezes RecordCoverage: every later delta restarts at the origin
    /// 200 KiB window with no fatal (silent stall).
    ///
    /// ENFORCER-154 alreadyEntry/alreadyReceipt still refuse re-commit.
    /// liveCtx=None means we do not own this step — never invent authority.
    val commitBranch:
        ctx: Context ->
        messageId: string ->
        calls: (int * ToolCallId * EnforcerCodec.CanonicalBlogCall) list ->
        assistantCompleted: bool ->
            Task<ContinuationOutcome>

    /// Branch 3 — COMPANION-005 first request / non-tool step: rebuild only from
    /// durable frames + typed CurrentRequest. Never extract TOML from raw user
    /// messages (C2).
    val firstRequestBranch:
        scope: IBloggerRuntimeHost ->
        journal: AgentJournal option ->
        bloggerSessionId: SessionId ->
        rawMessages: obj list ->
        project: (obj list -> ContinuationOutcome) ->
            Task<ContinuationOutcome>

    /// The Blogger continuation-transform handler (moved from EnforcerHost to
    /// break the Host.fs ↔ Continuation.fs compile-order cycle).
    ///
    /// Thin dispatcher over the three branches (emptyCallsBranch / commitBranch /
    /// firstRequestBranch): it only derives the closed branch context and forwards.
    val handleContinuation:
        scope: IBloggerRuntimeHost ->
        journal: AgentJournal option ->
        recoveryProbe: (AgentJournal -> SessionId -> obj list -> RecoveryStageProbe) ->
        bloggerSessionId: SessionId ->
        rawMessages: obj list ->
            Task<ContinuationOutcome>

    val applyContinuation:
        scope: PluginRuntimeScope ->
        journal: AgentJournal option ->
        terminateSession: SessionTermination ->
        projectionSessionIdOpt: string option ->
        outObj: obj ->
            Task
