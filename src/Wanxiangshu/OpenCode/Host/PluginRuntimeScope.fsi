namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Execution.Session.Recovery.SessionRecovery
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Strength.OpenCode

/// Session-scoped resource owner implemented by the tool runtime without
/// exposing its concrete dictionaries to the plugin composition root.
type ISessionRuntimeOwner =
    inherit IDisposable
    abstract CancelSessionChildren: string -> Task
    abstract DisposeSession: string -> Task
    abstract DisposeExecutorRuntime: string -> Task
    /// MANAGED-SESSION-018: plugin shutdown drains process-local observers without
    /// manufacturing logical parent cancellation. Durable Active handles survive
    /// for restart recovery before the shared Journal/EventStore is released.
    abstract DisposeAsync: unit -> Task
    /// EXEC-016: live PTY still tracked for this parent session (DevOps).
    abstract HasLivePty: string -> bool

/// Explicit lifetime root for one plugin instance. Collections here are either
/// physical resources, display caches, or bounded per-call deduplication.
type PluginRuntimeScope =
    new: journal: AgentJournal option -> PluginRuntimeScope

    interface IDisposable

    member Journal: AgentJournal option

    member AttachDurabilityActivation: activate: (unit -> unit) -> unit

    member ActivateDurability: unit -> unit

    /// Composition-of-owners: Strength decision-local state lives in its own scope.
    member Strength: PluginStrengthScope

    /// Composition-of-owners: Blogger parking/flight/drain state lives in its own scope.
    member Blogger: PluginBloggerScope
    member BloggerRuntimeHost: IBloggerRuntimeHost

    /// Composition-of-owners: per-instance session registries live in their own scope.
    member Sessions: PluginSessionScope

    /// Composition-of-owners: family recovery + attempt planning live in their own scope.
    member Recovery: PluginRecoveryScope

    member AttachSatelliteRuntime: runtime: SatelliteRuntime -> unit

    member Satellites: SatelliteRuntime

    member AttachSyncDelegateRuntime: runtime: SyncDelegateRuntime -> unit

    member AttachChatRecoveryRuntime: runtime: SessionRecoveryHost -> unit

    member SignalChatRecovery: event: ChatExecutionRecoveryLifecycleEvent -> Task

    member SignalChatRecoverySession:
        sessionId: SessionId -> eventOf: (ChatExecutionKey -> ChatExecutionRecoveryLifecycleEvent) -> Task

    member DrainChatRecovery: sessionId: SessionId -> Task

    member SyncDelegateRuntime: SyncDelegateRuntime option

    member AttachLoopSensor: sensor: LoopSensor -> unit

    member AttachMessageVisibility: hub: MessageVisibilityHub -> unit

    /// None until the signal stack wires the hub; the catch-up re-read then
    /// falls back to its bounded immediate form.
    member MessageVisibility: MessageVisibilityHub option

    member LoopSensor: LoopSensor

    /// Current-process join admission only; no cross-process tool recovery.
    member RequireFamilyRecovery: root: SessionId -> Task<FamilyRecovery>

    /// Await family recovery before business effects. Returns FamilyRecovery so
    /// callers must match FamilyBlocked (P0-RECOVERY-JOIN-001: no collapse to unit).
    member EnsureRecoveryDone: root: SessionId -> Task<FamilyRecovery>

    member ArmRecovery: sessionId: SessionId * physicalUserMessageId: PhysicalUserMessageId -> unit

    member TryTakeRecoveryPermit:
        sessionId: SessionId * physicalUserMessageId: PhysicalUserMessageId -> SlotArming option

    member RecordPendingAttemptPlan:
        sessionId: SessionId -> physicalUserMessageId: PhysicalUserMessageId -> plan: PendingAttemptPlan -> unit

    member TryBindAttemptPlan:
        sessionId: SessionId ->
        physicalUserMessageId: PhysicalUserMessageId ->
        providerRun: ProviderRunIdentity ->
            AttemptPlan option

    member RecordAttemptPlan: sessionId: SessionId -> providerRun: ProviderRunIdentity -> plan: AttemptPlan -> unit

    member TryAttemptPlan: sessionId: SessionId -> providerRun: ProviderRunIdentity -> AttemptPlan option

    member ConsumeAttemptPlan: sessionId: SessionId -> providerRun: ProviderRunIdentity -> AttemptPlan option

    /// HOST-006 prevention layer: the config hook's finding.
    ///
    /// Written once at config time, read once by the startup probe. Not a collection
    /// because there is one verdict per plugin instance — the settings are
    /// instance-global (`config/config.ts:607`), not per session.
    member RecordCompactionSettingGap: gap: CompactionSetting option -> unit

    member CompactionSettingGap: CompactionSetting option

    /// HOST-006 startup probe: has it already run.
    ///
    /// One probe per plugin instance, not per session. The claim it tests is about the
    /// Host build, and the first managed session's first turn is the cheapest place to
    /// observe it — running it again on every later session would keep asking a
    /// question already answered while risking a false refusal from a legitimate
    /// `/compact`.
    ///
    /// `TryClaimStartupProbe` returns true exactly once, so the caller cannot
    /// accidentally judge twice from concurrent reconcile passes.
    member TryClaimStartupProbe: unit -> bool

    /// Cheap read for the common case: after the probe has run, every later reconcile
    /// pass skips the judgement entirely rather than building a verdict and discarding
    /// it.
    member IsStartupProbeOpen: bool

    member AttachToolRuntime: owner: ISessionRuntimeOwner -> unit

    member TrackSubscription: value: IDisposable option -> unit

    member TrackReconcileShutdown: stopAndDrain: (unit -> Task) -> unit

    member RunBackground: start: (unit -> Task) -> unit

    member RunOwnedWork: start: (unit -> Task) -> Task

    member AttachSharedTerminal: key: string option * port: Events.HostEventPort option -> unit

    member DisposeExecutorRuntime: sessionId: string -> Task

    /// EXEC-016: live PTY probe for DevOps join guard.
    member HasLivePty: sessionId: string -> bool

    member CancelSessionChildren: sessionId: string -> Task

    member DisposeSession: sessionId: string -> Task

    member DisposeSessionPreservingIdentity: sessionId: string -> Task

    member DropSessionIdentity: sessionId: string -> unit

    member DisposeAsync: unit -> Task

    member Dispose: unit -> unit
