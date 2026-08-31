namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
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
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Interaction.Dispatch.OpenCode
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
open Wanxiangshu.Execution.Session.Recovery.SessionRecovery
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Execution.Session
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
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
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Host

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
type PluginRuntimeScope(journal: AgentJournal option) =
    let strength = PluginStrengthScope()
    let blogger = PluginBloggerScope()
    let sessions = PluginSessionScope()
    let recovery = PluginRecoveryScope(journal)

    let toolRuntimeGate = obj ()
    // DSL-MUTABLE: resource — session tool runtime owner handle
    let mutable toolRuntime: ISessionRuntimeOwner option = None
    // DSL-MUTABLE: resource
    let mutable subscription: IDisposable option = None
    // DSL-MUTABLE: resource — shared terminal bus key
    let mutable sharedTerminalKey: string option = None
    // DSL-MUTABLE: resource — shared terminal bus port handle
    let mutable sharedTerminalPort: Events.HostEventPort option = None
    let disposeGate = obj ()
    let ownedWorkGate = obj ()
    // DSL-MUTABLE: resource — scope dispose latch
    let mutable disposed = false
    // DSL-MUTABLE: resource — one shutdown Task owns the Journal/store release sequence.
    let mutable disposeTask: Task option = None
    // DSL-MUTABLE: resource — scheduler shutdown hook owned by this scope.
    let mutable reconcileShutdown: (unit -> Task) option = None
    // DSL-MUTABLE: resource — Host-work admission latch shared by foreground hooks and detached work.
    let mutable acceptingOwnedWork = true
    // DSL-MUTABLE: resource — in-flight Host-work count.
    let mutable ownedWorkCount = 0
    // DSL-MUTABLE: single-flight — shared waiter for Host-work drain.
    let mutable ownedWorkDrainWaiter: TaskCompletionSource<unit> option = None
    // DSL-MUTABLE: resource — first detached Host-work failure for shutdown propagation.
    let mutable backgroundFailure: exn option = None
    let durabilityActivationGate = obj ()
    // DSL-MUTABLE: resource — one-shot callbacks that may force deferred durable Current.
    let mutable durabilityActivators: (unit -> unit) list = []
    // DSL-MUTABLE: resource — first real durable admission owns activation exactly once.
    let mutable durabilityActivated = false

    /// HOST-006: the first compaction setting the config hook could not establish.
    ///
    /// Recorded rather than thrown, because HOST-006's verdict needs both halves — the
    /// settings and the first turn's observation. Throwing at config time would report
    /// the symptom before the probe could say whether anything actually compacted.
    // DSL-MUTABLE: resource — HOST-006 compaction setting gap observation
    let mutable compactionSettingGap: Wanxiangshu.Host.CompactionSetting option = None

    /// HOST-006 startup probe latch, with its own gate.
    ///
    /// Not sharing `toolRuntimeGate`: two unrelated invariants behind one lock read as
    /// if they were related, and the next person to touch either has to prove they are
    /// not.
    let startupProbeGate = obj ()
    // DSL-MUTABLE: single-flight — HOST-006 startup probe one-shot latch
    let mutable startupProbeDone = false

    /// DG-008: process-local armed anomaly lives inside the sensor.
    /// Optional until HostSignalBootstrap wires abort + ownership.
    // DSL-MUTABLE: resource — loop sensor attachment slot
    let mutable loopSensor: LoopSensor option = None
    // DSL-MUTABLE: resource — message-visibility hub attachment slot
    let mutable messageVisibility: MessageVisibilityHub option = None
    // DSL-MUTABLE: resource — satellite runtime attachment slot
    let mutable satelliteRuntime: SatelliteRuntime option = None
    // DSL-MUTABLE: resource — sync-delegate runtime attachment slot
    let mutable syncDelegateRuntime: SyncDelegateRuntime option = None
    // DSL-MUTABLE: resource — event-driven managed chat recovery owner
    let mutable chatRecoveryRuntime: SessionRecoveryHost option = None

    let disposeRuntimeOwner (owner: ISessionRuntimeOwner option) =
        task {
            match owner with
            | Some active -> do! active.DisposeAsync()
            | None -> ()
        }

    let captureTaskFailure (work: Task) : Task<exn option> =
        task {
            try
                do! work
                return None
            with ex ->
                return Some ex
        }

    let captureSyncFailure (work: unit -> unit) : exn option =
        try
            work ()
            None
        with ex ->
            Some ex

    member _.Journal = journal

    member _.AttachDurabilityActivation(activate: unit -> unit) =
        let runNow =
            lock durabilityActivationGate (fun () ->
                if durabilityActivated then
                    true
                else
                    durabilityActivators <- activate :: durabilityActivators
                    false)

        if runNow then
            activate ()

    member _.ActivateDurability() =
        let activators =
            lock durabilityActivationGate (fun () ->
                if durabilityActivated then
                    []
                else
                    durabilityActivated <- true
                    let pending = List.rev durabilityActivators
                    durabilityActivators <- []
                    pending)

        for activate in activators do
            activate ()

    /// Composition-of-owners: Strength decision-local state lives in its own scope.
    member _.Strength = strength

    /// Composition-of-owners: Blogger parking/flight/drain state lives in its own scope.
    member _.Blogger = blogger
    member _.BloggerRuntimeHost: IBloggerRuntimeHost = blogger :> IBloggerRuntimeHost

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

    member _.AttachChatRecoveryRuntime(runtime: SessionRecoveryHost) = chatRecoveryRuntime <- Some runtime

    member _.SignalChatRecovery(event: ChatExecutionRecoveryLifecycleEvent) : Task =
        match chatRecoveryRuntime with
        | Some runtime -> runtime.Signal event
        | None -> Task.FromResult(()) :> Task

    member _.SignalChatRecoverySession
        (sessionId: SessionId)
        (eventOf: ChatExecutionKey -> ChatExecutionRecoveryLifecycleEvent)
        : Task =
        match chatRecoveryRuntime with
        | Some runtime -> runtime.SignalSession(sessionId, eventOf)
        | None -> Task.FromResult(()) :> Task

    member _.DrainChatRecovery(sessionId: SessionId) : Task =
        match chatRecoveryRuntime with
        | Some runtime -> runtime.Drain sessionId
        | None -> Task.FromResult(()) :> Task

    member _.SyncDelegateRuntime = syncDelegateRuntime

    member _.AttachLoopSensor(sensor: LoopSensor) = loopSensor <- Some sensor

    member _.AttachMessageVisibility(hub: MessageVisibilityHub) = messageVisibility <- Some hub

    /// None until the signal stack wires the hub; the catch-up re-read then
    /// falls back to its bounded immediate form.
    member _.MessageVisibility = messageVisibility

    member _.LoopSensor =
        match loopSensor with
        | Some sensor -> sensor
        | None ->
            // Tests / journal-only scopes never stream deltas. A no-op sensor keeps
            // completion paths callable without inventing an abort port.
            let empty =
                LoopSensor((fun _ -> false), (fun _ -> Task.FromResult(Ok())), (fun _ _ _ -> Task.FromResult(Ok())))

            loopSensor <- Some empty
            empty

    /// Current-process join admission only; no cross-process tool recovery.
    member this.RequireFamilyRecovery(root: SessionId) : Task<FamilyRecovery> = recovery.RequireFamilyRecovery root

    /// Await family recovery before business effects. Returns FamilyRecovery so
    /// callers must match FamilyBlocked (P0-RECOVERY-JOIN-001: no collapse to unit).
    member this.EnsureRecoveryDone(root: SessionId) : Task<FamilyRecovery> = recovery.EnsureRecoveryDone root

    member this.ArmRecovery(sessionId: SessionId, physicalUserMessageId: PhysicalUserMessageId) =
        recovery.ArmRecovery(sessionId, physicalUserMessageId)

    member this.TryTakeRecoveryPermit(sessionId: SessionId, physicalUserMessageId: PhysicalUserMessageId) =
        recovery.TryTakeRecoveryPermit(sessionId, physicalUserMessageId)

    member this.RecordPendingAttemptPlan
        (sessionId: SessionId)
        (physicalUserMessageId: PhysicalUserMessageId)
        (plan: PendingAttemptPlan)
        =
        recovery.RecordPendingAttemptPlan sessionId physicalUserMessageId plan

    member this.TryBindAttemptPlan
        (sessionId: SessionId)
        (physicalUserMessageId: PhysicalUserMessageId)
        (providerRun: ProviderRunIdentity)
        =
        recovery.TryBindAttemptPlan sessionId physicalUserMessageId providerRun

    member this.RecordAttemptPlan (sessionId: SessionId) (providerRun: ProviderRunIdentity) (plan: AttemptPlan) =
        recovery.RecordAttemptPlan sessionId providerRun plan

    member this.TryAttemptPlan (sessionId: SessionId) (providerRun: ProviderRunIdentity) =
        recovery.TryAttemptPlan sessionId providerRun

    member this.ConsumeAttemptPlan (sessionId: SessionId) (providerRun: ProviderRunIdentity) =
        recovery.ConsumeAttemptPlan sessionId providerRun

    /// HOST-006 prevention layer: the config hook's finding.
    ///
    /// Written once at config time, read once by the startup probe. Not a collection
    /// because there is one verdict per plugin instance — the settings are
    /// instance-global (`config/config.ts:607`), not per session.
    member _.RecordCompactionSettingGap(gap: Wanxiangshu.Host.CompactionSetting option) = compactionSettingGap <- gap

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

    member _.TrackReconcileShutdown(stopAndDrain: unit -> Task) = reconcileShutdown <- Some stopAndDrain

    member private _.AdmitOwnedWork() : bool =
        lock ownedWorkGate (fun () ->
            if not acceptingOwnedWork then
                false
            else
                ownedWorkCount <- ownedWorkCount + 1
                true)

    member private _.RecordBackgroundFailure(failure: exn) =
        lock ownedWorkGate (fun () -> backgroundFailure <- Option.orElse backgroundFailure (Some failure))

    member private _.FinishOwnedWork() =
        lock ownedWorkGate (fun () ->
            ownedWorkCount <- ownedWorkCount - 1

            if not acceptingOwnedWork && ownedWorkCount = 0 then
                ownedWorkDrainWaiter
                |> Option.iter (fun waiter -> AsyncSupport.trySetResult waiter () |> ignore))

    member private _.CaptureBackgroundFailure(start: unit -> Task) : Task<exn option> =
        task {
            try
                do! start ()
                return None
            with ex ->
                return Some ex
        }

    member private this.ObserveBackgroundWork(start: unit -> Task) : Task =
        task {
            let! failure = this.CaptureBackgroundFailure start
            failure |> Option.iter this.RecordBackgroundFailure
            this.FinishOwnedWork()
        }
        :> Task

    member this.RunBackground(start: unit -> Task) : unit =
        if this.AdmitOwnedWork() then
            this.ObserveBackgroundWork(start) |> ignore

    member private this.RunAdmittedOwnedWork(start: unit -> Task) : Task =
        task {
            try
                do! start ()
            finally
                this.FinishOwnedWork()
        }
        :> Task

    member this.RunOwnedWork(start: unit -> Task) : Task =
        if this.AdmitOwnedWork() then
            this.RunAdmittedOwnedWork start
        else
            Task.FromResult(()) :> Task

    member private _.OwnedWorkDrainTask() : Task =
        match ownedWorkDrainWaiter with
        | Some waiter -> waiter.Task :> Task
        | None ->
            let waiter =
                TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

            ownedWorkDrainWaiter <- Some waiter
            waiter.Task :> Task

    member private this.StopOwnedWorkAndDrain() : Task<exn option> =
        let waiting =
            lock ownedWorkGate (fun () ->
                acceptingOwnedWork <- false

                if ownedWorkCount = 0 then
                    Task.FromResult(()) :> Task
                else
                    this.OwnedWorkDrainTask())

        task {
            do! waiting
            return lock ownedWorkGate (fun () -> backgroundFailure)
        }

    member _.AttachSharedTerminal(key: string option, port: Events.HostEventPort option) =
        sharedTerminalKey <- key
        sharedTerminalPort <- port

    member _.DisposeExecutorRuntime(sessionId: string) : Task =
        let owner = lock toolRuntimeGate (fun () -> toolRuntime)

        match owner with
        | Some active -> active.DisposeExecutorRuntime sessionId
        | None -> Task.FromResult(()) :> Task

    /// EXEC-016: live PTY probe for DevOps join guard.
    member _.HasLivePty(sessionId: string) : bool =
        lock toolRuntimeGate (fun () ->
            match toolRuntime with
            | Some owner -> owner.HasLivePty sessionId
            | None -> false)

    member _.CancelSessionChildren(sessionId: string) : Task =
        let owner = lock toolRuntimeGate (fun () -> toolRuntime)

        match owner with
        | Some active -> active.CancelSessionChildren sessionId
        | None -> Task.FromResult(()) :> Task

    member private this.DisposeSessionCore(sessionId: string, preserveIdentity: bool) : Task =
        task {
            let owner = lock toolRuntimeGate (fun () -> toolRuntime)

            match owner with
            | Some active -> do! active.DisposeSession sessionId
            | None -> ()

            // C6 item 27: waiters are keyed by BloggerSessionId. When the MAIN is
            // deleted, cancel the linked Blogger's parked waiter + request slots too.
            let linkedBloggerKeys = sessions.LinkedBloggerKeys sessionId
            sessions.ClearSession(sessionId, preserveIdentity)
            recovery.ClearSession sessionId
            strength.ClearSession sessionId
            this.LoopSensor.DropSession(SessionId.create sessionId)

            // Always cancel the deleted id; also cancel linked Blogger keys.
            let cancelKeys = (sessionId :: linkedBloggerKeys) |> List.distinct

            for key in cancelKeys do
                (blogger :> IBloggerRuntimeHost).CancelParked key

                lock SharedState.BloggerFlightGate (fun () -> SharedState.BloggerFlights.Remove key |> ignore)

                blogger.DropDrainWindow key
                recovery.ClearAttemptPlansFor key
        }
        :> Task

    member this.DisposeSession(sessionId: string) =
        this.DisposeSessionCore(sessionId, false)

    member this.DisposeSessionPreservingIdentity(sessionId: string) =
        this.DisposeSessionCore(sessionId, true)

    member _.DropSessionIdentity(sessionId: string) = sessions.DropSessionIdentity sessionId

    member private _.TakeReconcileDrain() : Task =
        let shutdown = reconcileShutdown
        reconcileShutdown <- None

        match shutdown with
        | Some stopAndDrain -> stopAndDrain ()
        | None -> Task.FromResult(()) :> Task

    member private _.TakeRuntimeOwner() : ISessionRuntimeOwner option =
        lock toolRuntimeGate (fun () ->
            let owner = toolRuntime
            toolRuntime <- None
            owner)

    member private _.RethrowFirstFailure(firstFailure: exn option) =
        match firstFailure with
        | Some failure -> raise failure
        | None -> ()

    member private this.StartDisposeAsync() : Task =
        disposed <- true

        task {
            // Teardown is best-effort-complete but never error-silent: remember
            // the first real failure, continue safe independent cleanup, rethrow last.
            // DSL-MUTABLE: algorithm-scratch — first teardown failure accumulator.
            let mutable firstFailure: exn option = None

            let remember failure =
                firstFailure <- Option.orElse firstFailure failure

            // Close external admission first. Close both internal admissions before
            // awaiting either drain, so no durable work can enter during shutdown.
            remember (captureSyncFailure (fun () -> subscription |> Option.iter (fun active -> active.Dispose())))
            subscription <- None

            blogger.BeginShutdown()

            let reconcileDrain = this.TakeReconcileDrain()
            let ownedWorkDrain = this.StopOwnedWorkAndDrain()

            let! reconcileFailure = captureTaskFailure reconcileDrain
            remember reconcileFailure

            let! backgroundFailure = ownedWorkDrain
            remember backgroundFailure

            remember (captureSyncFailure (fun () -> blogger.Dispose()))

            let! runtimeFailure = captureTaskFailure (disposeRuntimeOwner (this.TakeRuntimeOwner()))
            remember runtimeFailure

            remember (captureSyncFailure (fun () -> sessions.Dispose()))
            remember (captureSyncFailure (fun () -> syncDelegateRuntime |> Option.iter (fun sd -> sd.Dispose())))
            syncDelegateRuntime <- None
            remember (captureSyncFailure (fun () -> strength.Dispose()))

            // MANAGED-SESSION-018: the shared durable substrate is the last owner
            // released, after scheduler/background/process-local detach drains.
            let! journalFailure = captureTaskFailure (SharedAgentJournal.releaseAsync journal)
            remember journalFailure
            remember (captureSyncFailure (fun () -> SharedTerminalBus.release sharedTerminalKey sharedTerminalPort))
            sharedTerminalKey <- None
            sharedTerminalPort <- None

            this.RethrowFirstFailure firstFailure
        }
        :> Task

    member this.DisposeAsync() : Task =
        lock disposeGate (fun () ->
            match disposeTask with
            | Some running -> running
            | None ->
                let running = this.StartDisposeAsync()
                disposeTask <- Some running
                running)

    member this.Dispose() = this.DisposeAsync() |> ignore

    interface IDisposable with
        member this.Dispose() = this.Dispose()
