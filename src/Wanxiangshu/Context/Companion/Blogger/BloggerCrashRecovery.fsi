namespace Wanxiangshu.Context.Companion.Blogger

open System
open System.Threading.Tasks
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

/// C5 item 20: crash windows for the Blogger vertical slice only.
///
/// Recovery inputs (item 23): durable request context + Host snapshot + Journal
/// receipts. No TOML reverse parse, no guess from latest X, no log strings.
///
/// Live in-process: materialize → EnsureRecoveryDone may run before/during the
/// first provider step. Never abandon or stomp CurrentRequest when the host
/// already holds physical flight ownership (HasFlight).
module BloggerCrashRecovery =

    /// Must match EnforcerHost interactionNudge repairKind (ENFORCER-066 claim scope).
    [<Literal>]
    val BloggerMissingToolRepairKind: string = "blogger-missing-tool"

    [<RequireQualifiedAccess>]
    type WindowOutcome =
        /// A: Host session gone after materialize → abandon, clear flight.
        | AbandonedUnsent of BloggerRequestId
        /// C: tool results present, no receipt → restore physical flight for re-entry.
        | Recommitted of ProviderRunIdentity
        /// D: receipt present, no waiter → nothing to restore; next material
        /// flows through startFrozen and the drain re-checks receipts.
        | ReceiptedIdle of SessionId
        /// E: Parked, new material exists → leave for next coordinator offer.
        | PendingMaterial of SessionId
        /// Still open and Host still running — restore physical flight in memory.
        | RestoredInFlight of SessionId
        /// Live process already owns the request; recovery is a no-op.
        | AlreadyLive of SessionId
        /// Startup snapshot was superseded before this Blogger acquired materialization admission.
        | Superseded of BloggerRequestId
        | Unreadable of SessionId * reason: string

    /// <summary>Pure decision for window A/B/C given Host tool-call presence and
    /// receipts. Exported so tests can exercise the cold-window table directly
    /// without driving a full crash.</summary>
    val classifyOpenRequest:
        hasPhysicalAccepted: bool -> hasCompletedBlogTool: bool -> hasCycleReceipt: bool -> WindowOutcome option

    /// Startup pass: walk open materializations + receipts.
    val reconcile:
        journal: AgentJournal option ->
        host: IBloggerRuntimeHost ->
        snapshotOpt: ISessionSnapshotPort option ->
            Task<WindowOutcome list>

    /// Single-flight gate, same lifecycle as PromptRecovery (not in constructor).
    type RecoveryGate =
        new:
            journal: AgentJournal option * host: IBloggerRuntimeHost * snapshotOpt: ISessionSnapshotPort option ->
                RecoveryGate

        member EnsureDone: unit -> Task
