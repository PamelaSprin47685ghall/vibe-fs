namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Domain.SessionRecovery
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session
open Wanxiangshu.Host

/// Session-scoped resource owner implemented by the tool runtime without
/// exposing its concrete dictionaries to the plugin composition root.
type ISessionRuntimeOwner =
    inherit IDisposable
    abstract DisposeSession: string -> unit
    abstract DisposeExecutorRuntime: string -> unit
    /// EXEC-016: live PTY still tracked for this parent session (DevOps).
    abstract HasLivePty: string -> bool

/// Explicit lifetime root for one plugin instance. Collections here are either
/// physical resources, display caches, or bounded per-call deduplication.
type PluginRuntimeScope(journal: AgentJournal option) =
    let strength = PluginStrengthScope()
    let blogger = PluginBloggerScope()
    let sessions = PluginSessionScope()
    let recovery = PluginRecoveryScope()

    let toolRuntimeGate = obj ()
    // DSL-MUTABLE: resource — session tool runtime owner handle
    let mutable toolRuntime: ISessionRuntimeOwner option = None
    // DSL-MUTABLE: subscription — plugin host event subscription
    let mutable subscription: IDisposable option = None
    // DSL-MUTABLE: resource — shared terminal bus key
    let mutable sharedTerminalKey: string option = None
    // DSL-MUTABLE: resource — shared terminal bus port handle
    let mutable sharedTerminalPort: Events.HostEventPort option = None
    // DSL-MUTABLE: resource — scope dispose latch
    let mutable disposed = false

    /// HOST-006: the first compaction setting the config hook could not establish.
    ///
    /// Recorded rather than thrown, because HOST-006's verdict needs both halves — the
    /// settings and the first turn's observation. Throwing at config time would report
    /// the symptom before the probe could say whether anything actually compacted.
    // DSL-MUTABLE: resource — HOST-006 compaction setting gap observation
    let mutable compactionSettingGap: Wanxiangshu.Domain.CompactionSetting option = None

    /// HOST-006 startup probe latch, with its own gate.
    ///
    /// Not sharing `toolRuntimeGate`: two unrelated invariants behind one lock read as
    /// if they were related, and the next person to touch either has to prove they are
    /// not.
    let startupProbeGate = obj ()
    // DSL-MUTABLE: single-flight — HOST-006 startup probe one-shot latch
    let mutable startupProbeDone = false

    /// LOOP-006: process-local LoopKillArmed lives inside the sensor.
    /// Optional until HostSignalBootstrap wires abort + ownership.
    // DSL-MUTABLE: resource — loop sensor attachment slot
    let mutable loopSensor: LoopSensor option = None
    // DSL-MUTABLE: resource — NEEDHELP reasoning sensor attachment slot
    let mutable needHelpSensor: NeedHelpSensor option = None
    // DSL-MUTABLE: resource — satellite runtime attachment slot
    let mutable satelliteRuntime: SatelliteRuntime option = None
    // DSL-MUTABLE: resource — sync-delegate runtime attachment slot
    let mutable syncDelegateRuntime: SyncDelegateRuntime option = None
    // DSL-MUTABLE: resource — assistance workflow callbacks attach after
    // LifecycleWorkRecord composition, without reversing compile-layer ownership.
    // DSL-MUTABLE: resource — assistance reconciled-turn handler attachment slot
    let mutable assistanceTurnHandler: (ReconciledTurnContext -> Task<AssistanceTurnDisposition>) option =
        None

    // DSL-MUTABLE: resource — assistance session-drop handler attachment slot
    let mutable assistanceDropSession: (SessionId -> unit) option = None

    member _.Journal = journal

    /// Composition-of-owners: Strength decision-local state lives in its own scope.
    member _.Strength = strength

    /// Composition-of-owners: Blogger parking/flight/drain state lives in its own scope.
    member _.Blogger = blogger
    member _.ParkedTransformHost: IParkedTransformHost = blogger :> IParkedTransformHost

    /// Composition-of-owners: per-instance session registries live in their own scope.
    member _.Sessions = sessions

    /// Composition-of-owners: family recovery + attempt planning live in their own scope.
    member _.Recovery = recovery

    member _.AttachSatelliteRuntime(runtime: SatelliteRuntime) = satelliteRuntime <- Some runtime

    member _.Satellites =
        match satelliteRuntime with
        | Some runtime -> runtime
        | None -> invalidOp "SatelliteRuntime has not been attached"

    member _.AttachSyncDelegateRuntime(runtime: SyncDelegateRuntime) = syncDelegateRuntime <- Some runtime

    member _.SyncDelegateRuntime = syncDelegateRuntime

    member _.AttachAssistance
        (handleTurn: ReconciledTurnContext -> Task<AssistanceTurnDisposition>, dropSession: SessionId -> unit)
        =
        assistanceTurnHandler <- Some handleTurn
        assistanceDropSession <- Some dropSession

    member _.HandleAssistanceTurn(context: ReconciledTurnContext) =
        match assistanceTurnHandler with
        | Some handle -> handle context
        | None -> Task.FromResult AssistanceTurnDisposition.NotAssistance

    member _.DropAssistanceSession(sessionId: SessionId) =
        assistanceDropSession |> Option.iter (fun drop -> drop sessionId)

    member _.AttachLoopSensor(sensor: LoopSensor) = loopSensor <- Some sensor

    member _.AttachNeedHelpSensor(sensor: NeedHelpSensor) = needHelpSensor <- Some sensor

    member _.NeedHelpSensor =
        match needHelpSensor with
        | Some sensor -> sensor
        | None ->
            // Journal/unit-only scopes have no streaming source. Keep a no-op
            // sensor so turn classification can still ask exact attempt identity.
            let empty = NeedHelpSensor((fun _ -> false), (fun _ -> Task.FromResult(Ok())))
            needHelpSensor <- Some empty
            empty

    member _.LoopSensor =
        match loopSensor with
        | Some sensor -> sensor
        | None ->
            // Tests / journal-only scopes never stream deltas. A no-op sensor keeps
            // completion paths callable without inventing an abort port.
            let empty = LoopSensor((fun _ -> false), (fun _ -> Task.FromResult(Ok())))

            loopSensor <- Some empty
            empty

    member this.AttachFamilyRecoveryPorts(ports: SessionRecoveryWorkflow.Ports) =
        recovery.AttachFamilyRecoveryPorts ports

    /// RECOVERY-FAMILY: obtain FamilyRecovery for a parent before business work.
    /// Missing ports → FamilyBlocked (fail closed). Never synthetic FamilyReady.
    member this.RequireFamilyRecovery(root: SessionId) : Task<FamilyRecovery> = recovery.RequireFamilyRecovery root

    /// Await family recovery before business effects. Returns FamilyRecovery so
    /// callers must match FamilyBlocked (P0-RECOVERY-JOIN-001: no collapse to unit).
    member this.EnsureRecoveryDone(root: SessionId) : Task<FamilyRecovery> = recovery.EnsureRecoveryDone root

    member this.ArmRecovery(sessionId: SessionId) = recovery.ArmRecovery sessionId

    member this.TryRecoveryArming(sessionId: SessionId) = recovery.TryRecoveryArming sessionId

    member this.RecordAttemptPlan (sessionId: SessionId) (providerRun: ProviderRunIdentity) (plan: AttemptPlan) =
        recovery.RecordAttemptPlan sessionId providerRun plan

    member this.TryAttemptPlan (sessionId: SessionId) (providerRun: ProviderRunIdentity) =
        recovery.TryAttemptPlan sessionId providerRun

    member this.ClearRecovery(sessionId: SessionId) =
        recovery.ClearRecovery(SessionId.value sessionId)

    member this.ClearAttemptPlan (sessionId: SessionId) (providerRun: ProviderRunIdentity) =
        recovery.AttemptPlans.Remove(SessionId.value sessionId + "\u001f" + ProviderRunIdentity.value providerRun)
        |> ignore


    /// HOST-006 prevention layer: the config hook's finding.
    ///
    /// Written once at config time, read once by the startup probe. Not a collection
    /// because there is one verdict per plugin instance — the settings are
    /// instance-global (`config/config.ts:607`), not per session.
    member _.RecordCompactionSettingGap(gap: Wanxiangshu.Domain.CompactionSetting option) = compactionSettingGap <- gap

    member _.CompactionSettingGap = compactionSettingGap

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
    member _.TryClaimStartupProbe() : bool =
        lock startupProbeGate (fun () ->
            if startupProbeDone then
                false
            else
                startupProbeDone <- true
                true)

    /// Cheap read for the common case: after the probe has run, every later reconcile
    /// pass skips the judgement entirely rather than building a verdict and discarding
    /// it.
    member _.IsStartupProbeOpen = lock startupProbeGate (fun () -> not startupProbeDone)

    member _.AttachToolRuntime(owner: ISessionRuntimeOwner) =
        lock toolRuntimeGate (fun () -> toolRuntime <- Some owner)

    member _.TrackSubscription(value: IDisposable option) = subscription <- value

    member _.AttachSharedTerminal(key: string option, port: Events.HostEventPort option) =
        sharedTerminalKey <- key
        sharedTerminalPort <- port

    member _.DisposeExecutorRuntime(sessionId: string) =
        lock toolRuntimeGate (fun () ->
            toolRuntime |> Option.iter (fun owner -> owner.DisposeExecutorRuntime sessionId))

    /// EXEC-016: live PTY probe for DevOps join guard.
    member _.HasLivePty(sessionId: string) : bool =
        lock toolRuntimeGate (fun () ->
            match toolRuntime with
            | Some owner -> owner.HasLivePty sessionId
            | None -> false)

    member this.DisposeSession(sessionId: string) =
        lock toolRuntimeGate (fun () -> toolRuntime |> Option.iter (fun owner -> owner.DisposeSession sessionId))

        // C6 item 27: waiters are keyed by BloggerSessionId. When the MAIN is
        // deleted, cancel the linked Blogger's parked waiter + request slots too.
        let linkedBloggerKeys = sessions.LinkedBloggerKeys sessionId
        sessions.ClearSession sessionId
        recovery.ClearSession sessionId
        strength.ClearSession sessionId
        this.LoopSensor.DropSession(SessionId.create sessionId)

        // Always cancel the deleted id; also cancel linked Blogger keys.
        let cancelKeys = (sessionId :: linkedBloggerKeys) |> List.distinct

        for key in cancelKeys do
            (blogger :> IParkedTransformHost).CancelParked key

            lock SharedState.BloggerFlightGate (fun () -> SharedState.BloggerFlights.Remove key |> ignore)

            blogger.DropDrainWindow key

            recovery.ClearAttemptPlansFor key

    member this.Dispose() =
        if not disposed then
            disposed <- true
            subscription |> Option.iter (fun active -> active.Dispose())
            subscription <- None

            blogger.Dispose()

            lock toolRuntimeGate (fun () ->
                toolRuntime |> Option.iter (fun owner -> owner.Dispose())
                toolRuntime <- None)

            sessions.Dispose()
            syncDelegateRuntime |> Option.iter (fun sd -> sd.Dispose())
            syncDelegateRuntime <- None
            strength.Dispose()
            SharedAgentJournal.release journal
            SharedTerminalBus.release sharedTerminalKey sharedTerminalPort
            sharedTerminalKey <- None
            sharedTerminalPort <- None

    interface IDisposable with
        member this.Dispose() = this.Dispose()
