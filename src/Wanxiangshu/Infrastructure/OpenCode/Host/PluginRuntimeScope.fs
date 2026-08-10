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

    /// ENFORCER-160/162: parked continuation transforms, keyed by session id.
    ///
    /// At most one parked transform per session (a session's step loop is
    /// serial, so two parks for one session cannot race in practice — the
    /// dictionary entry is the guard that makes the invariant structural).
    let parkedGate = obj ()
    let parked = Dictionary<string, ParkedTransform>()
    // ENFORCER-047/050: dual slots without dual storage for PendingOffer.
    // CurrentRequest ownership = physical flight registry (entry = in-flight).
    // PendingOffer = separate dictionary for the next Main material while Parked.
    let pendingOffer = Dictionary<string, BloggerRequestContext>()
    // Physical Blogger flight ownership lives in SharedState (cross worktree/root).
    // DSL-MUTABLE: single-flight — physical drain-window slot
    let drainWindows = Dictionary<string, DrainWindow>()

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

    /// Family recovery coordinator ports (PROMPT-011 + C5 + RECOVERY-FAMILY).
    ///
    /// Attached after `createHost`. First business entry point runs
    /// SessionRecoveryWorkflow; later callers await the same single-flight task.
    // DSL-MUTABLE: resource — family recovery ports attachment slot
    let mutable familyRecoveryPorts: SessionRecoveryWorkflow.Ports option = None
    let recoveryGateLock = obj ()

    /// LOOP-006: process-local LoopKillArmed lives inside the sensor.
    /// Optional until HostSignalBootstrap wires abort + ownership.
    // DSL-MUTABLE: resource — loop sensor attachment slot
    let mutable loopSensor: LoopSensor option = None
    // DSL-MUTABLE: resource — satellite runtime attachment slot
    let mutable satelliteRuntime: SatelliteRuntime option = None
    // DSL-MUTABLE: resource — student-teacher runtime attachment slot
    let mutable studentTeacherRuntime: StudentTeacherRuntime option = None
    // DSL-MUTABLE: resource — student-teacher unavailable reason latch
    let mutable studentTeacherUnavailable: string option = None

    member _.Journal = journal

    member _.AttachSatelliteRuntime(runtime: SatelliteRuntime) = satelliteRuntime <- Some runtime

    member _.Satellites =
        match satelliteRuntime with
        | Some runtime -> runtime
        | None -> invalidOp "SatelliteRuntime has not been attached"

    member _.AttachStudentTeacherRuntime(runtime: StudentTeacherRuntime) =
        studentTeacherRuntime <- Some runtime
        studentTeacherUnavailable <- None

    member _.MarkStudentTeacherUnavailable(reason: string) =
        studentTeacherUnavailable <- Some reason

    member _.StudentTeacherRuntime = studentTeacherRuntime

    member _.ObserveStudentMessage(message: PromptIngressCodec.DecodedMessage) =
        match studentTeacherRuntime with
        | Some runtime -> runtime.ObserveChatMessage message
        | None ->
            match
                message.ExplicitAgent
                |> Option.bind (PromptAuthority.parseAgentName >> Result.toOption)
            with
            | Some(_, Role.Student, _, _) ->
                Error(defaultArg studentTeacherUnavailable "Student–Teacher runtime is unavailable")
            | _ -> Ok()
    // HOST-012: 跨实例共享（模块级单例）——worktree 独立插件实例的 fork→verdict
    // 链必须读写同一份。每实例独有状态（OwnedSessions、UserMessageBindings、
    // Companions 等）保持 per-instance。
    member val SessionDirectories = SharedState.SessionDirectories
    member val OwnedSessions = HashSet<string>()
    member val UserMessageBindings = Dictionary<string, PhysicalUserMessageId>()
    member val SessionParents = SharedState.SessionParents
    member val Companions = Dictionary<string, CompanionHost>()
    member val CompanionGate = obj ()
    member val VerdictSessions = SharedState.VerdictSessions
    member val NudgeSent = HashSet<string>()
    member val JoinGuardNudges = HashSet<string>()
    member val AbortedSessions = HashSet<string>()
    member val RecoveryArming = Dictionary<string, SlotArming>()
    member val AttemptPlans = Dictionary<string, AttemptPlan>()
    // HOST-004: process-local idle-derived continuation admission. Per plugin
    // instance like NudgeSent / LoopSensor; never journalled (HOST-007). A
    // worktree owner transfer starts a fresh gate — no old permit survives.
    member val Quiescence = SessionQuiescenceGate()
    /// EXEC-017: process-local attempt-scoped join registry. External user messages
    /// signal only the CURRENT active JoinAttempt (UserMessageArrived), without
    /// cancelling mailbox/runtime and without a future latch (not journaled).
    member val JoinInterrupts: IJoinAttemptRegistry = JoinAttemptRegistry() :> IJoinAttemptRegistry

    member _.AttachLoopSensor(sensor: LoopSensor) = loopSensor <- Some sensor

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
        lock recoveryGateLock (fun () -> familyRecoveryPorts <- Some ports)

    /// RECOVERY-FAMILY: obtain FamilyRecovery for a parent before business work.
    /// Missing ports → FamilyBlocked (fail closed). Never synthetic FamilyReady.
    member this.RequireFamilyRecovery(root: SessionId) : Task<FamilyRecovery> =
        task {
            match lock recoveryGateLock (fun () -> familyRecoveryPorts) with
            | None ->
                return FamilyRecovery.FamilyBlocked(NonEmpty.one (RecoveryBlock.RecoveryCoordinatorUnavailable root))
            | Some ports -> return! SessionRecoveryWorkflow.Coordinator.recoverFamily ports root
        }

    /// Await family recovery before business effects. Returns FamilyRecovery so
    /// callers must match FamilyBlocked (P0-RECOVERY-JOIN-001: no collapse to unit).
    member this.EnsureRecoveryDone(root: SessionId) : Task<FamilyRecovery> = this.RequireFamilyRecovery root

    member this.ArmRecovery(sessionId: SessionId) =
        this.RecoveryArming.[SessionId.value sessionId] <- RecoverySlot.afterFailureAdvance

    member this.TryRecoveryArming(sessionId: SessionId) =
        match this.RecoveryArming.TryGetValue(SessionId.value sessionId) with
        | true, arming -> Some arming
        | false, _ -> None

    member this.RecordAttemptPlan (sessionId: SessionId) (providerRun: ProviderRunIdentity) (plan: AttemptPlan) =
        this.AttemptPlans.[SessionId.value sessionId + "\u001f" + ProviderRunIdentity.value providerRun] <- plan

    member this.TryAttemptPlan (sessionId: SessionId) (providerRun: ProviderRunIdentity) =
        let key =
            SessionId.value sessionId + "\u001f" + ProviderRunIdentity.value providerRun

        match this.AttemptPlans.TryGetValue(key) with
        | true, plan -> Some plan
        | false, _ -> None

    member this.ClearRecovery(sessionId: SessionId) =
        this.RecoveryArming.Remove(SessionId.value sessionId) |> ignore

    member this.ClearAttemptPlan (sessionId: SessionId) (providerRun: ProviderRunIdentity) =
        this.AttemptPlans.Remove(SessionId.value sessionId + "\u001f" + ProviderRunIdentity.value providerRun)
        |> ignore

    interface IParkedTransformHost with
        member this.ParkTransform(sessionId: string, lifetime: TimeSpan) : Task<bool> =
            task {
                let (entry, staged) =
                    lock parkedGate (fun () ->
                        match parked.TryGetValue sessionId with
                        | true, existing -> existing, false
                        | false, _ ->
                            let created = ParkedTransform(sessionId, lifetime)
                            parked.[sessionId] <- created

                            // ENFORCER-050 offer-first merge: PendingOffer staged
                            // while no transform was parked makes this park return
                            // immediately with `true`.
                            let staged = pendingOffer.ContainsKey sessionId

                            if staged then
                                created.TryResume()

                            created, staged)

                let! resumed = entry.Completion

                lock parkedGate (fun () ->
                    match parked.TryGetValue sessionId with
                    | true, current when obj.ReferenceEquals(current, entry) -> parked.Remove sessionId |> ignore
                    | _ -> ())

                return resumed
            }

        member this.ResumeParked(sessionId: string) : bool =
            lock parkedGate (fun () ->
                match parked.TryGetValue sessionId with
                | true, entry ->
                    entry.TryResume()
                    parked.Remove sessionId |> ignore
                    true
                | false, _ -> false)

        member this.CancelParked(sessionId: string) : unit =
            lock parkedGate (fun () ->
                match parked.TryGetValue sessionId with
                | true, entry ->
                    entry.TryCancel()
                    parked.Remove sessionId |> ignore
                | false, _ -> ()

                pendingOffer.Remove sessionId |> ignore
                // Park cancel/timeout leaves the logical Blogger idle, not disposed.
                // Seal is durable: park cancel clears flight; the next entry's
                // durable check re-blocks when still sealed. Drain slot is preserved.
                lock SharedState.BloggerFlightGate (fun () -> SharedState.BloggerFlights.Remove sessionId |> ignore))

        member this.HasParked(sessionId: string) : bool =
            lock parkedGate (fun () -> parked.ContainsKey sessionId)

        // Physical flight ownership (PR7 knife 1): entry present = single-flight request.
        member this.HasFlight(sessionId: string) : bool =
            lock SharedState.BloggerFlightGate (fun () -> SharedState.BloggerFlights.ContainsKey sessionId)

        member this.TryGetFlight(sessionId: string) : BloggerRequestContext option =
            lock SharedState.BloggerFlightGate (fun () ->
                match SharedState.BloggerFlights.TryGetValue sessionId with
                | true, ctx -> Some ctx
                | false, _ -> None)

        member this.SetCurrentRequest(sessionId: string, context: BloggerRequestContext) : unit =
            lock SharedState.BloggerFlightGate (fun () -> SharedState.BloggerFlights.[sessionId] <- context)

        member this.TryPeekCurrentRequest(sessionId: string) : BloggerRequestContext option =
            lock SharedState.BloggerFlightGate (fun () ->
                match SharedState.BloggerFlights.TryGetValue sessionId with
                | true, ctx -> Some ctx
                | false, _ -> None)

        member this.ClearCurrentRequest(sessionId: string) : unit =
            lock SharedState.BloggerFlightGate (fun () -> SharedState.BloggerFlights.Remove sessionId |> ignore)

        member this.SetPendingOffer(sessionId: string, context: BloggerRequestContext) : bool =
            lock parkedGate (fun () ->
                pendingOffer.[sessionId] <- context

                match parked.TryGetValue sessionId with
                | true, entry ->
                    entry.TryResume()
                    parked.Remove sessionId |> ignore
                    true
                | false, _ -> false)

        member this.TryTakePendingOffer(sessionId: string) : BloggerRequestContext option =
            lock parkedGate (fun () ->
                match pendingOffer.TryGetValue sessionId with
                | true, context ->
                    pendingOffer.Remove sessionId |> ignore
                    Some context
                | false, _ -> None)

        member this.GetDrainWindow(sessionId: string) : DrainWindow =
            lock parkedGate (fun () -> this.GetDrainWindowUnlocked sessionId)

        member this.SetDrainWindow(sessionId: string, window: DrainWindow) : unit =
            lock parkedGate (fun () -> drainWindows.[sessionId] <- window)

        member this.IsDrainOpen(sessionId: string) : bool =
            lock parkedGate (fun () ->
                match this.GetDrainWindowUnlocked sessionId with
                | DrainWindow.Open _ -> true
                | DrainWindow.Closed -> false)

    member private _.GetDrainWindowUnlocked(sessionId: string) : DrainWindow =
        match drainWindows.TryGetValue sessionId with
        | true, window -> window
        | false, _ -> DrainWindow.Closed

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
        let linkedBloggerKeys =
            match this.Companions.TryGetValue sessionId with
            | true, companion ->
                match companion.BloggerSession with
                | Some bloggerId -> [ SessionId.value bloggerId ]
                | None -> []
            | false, _ ->
                // sessionId may itself be a Blogger child being deleted.
                [ sessionId ]

        match this.Companions.TryGetValue sessionId with
        | true, companion ->
            this.Companions.Remove sessionId |> ignore
            (companion :> IDisposable).Dispose()
        | false, _ -> ()

        this.OwnedSessions.Remove sessionId |> ignore
        this.UserMessageBindings.Remove sessionId |> ignore
        this.SessionParents.Remove sessionId |> ignore
        this.SessionDirectories.Remove sessionId |> ignore
        this.VerdictSessions.Remove sessionId |> ignore
        this.AbortedSessions.Remove sessionId |> ignore
        this.RecoveryArming.Remove sessionId |> ignore
        this.LoopSensor.DropSession(SessionId.create sessionId)
        // HOST-004 Q-10: a deleted session's idle permits die forever.
        this.Quiescence.DropSession(SessionId.create sessionId)
        // SessionDeleted: drop join-interrupt waiters + one-shot user-message latch.
        this.JoinInterrupts.ClearSession(SessionId.create sessionId)

        // Always cancel the deleted id; also cancel linked Blogger keys.
        let cancelKeys = (sessionId :: linkedBloggerKeys) |> List.distinct

        for key in cancelKeys do
            (this :> IParkedTransformHost).CancelParked key

            lock SharedState.BloggerFlightGate (fun () -> SharedState.BloggerFlights.Remove key |> ignore)

            lock parkedGate (fun () -> drainWindows.Remove key |> ignore)

            this.AttemptPlans.Keys
            |> Seq.filter (fun planKey -> planKey.StartsWith(key + "\u001f", StringComparison.Ordinal))
            |> Seq.toList
            |> List.iter (fun planKey -> this.AttemptPlans.Remove planKey |> ignore)

    member this.Dispose() =
        if not disposed then
            disposed <- true
            subscription |> Option.iter (fun active -> active.Dispose())
            subscription <- None

            // ENFORCER-162: plugin dispose cancels every parked waiter. The
            // resolved `false` releases each suspended transform so the Host's
            // step loop can finish its current request cycle.
            lock parkedGate (fun () ->
                for entry in parked.Values |> Seq.toList do
                    entry.TryCancel()

                parked.Clear()
                pendingOffer.Clear()
                // BloggerFlights are SharedState — do not clear on one instance dispose.
                drainWindows.Clear())

            lock toolRuntimeGate (fun () ->
                toolRuntime |> Option.iter (fun owner -> owner.Dispose())
                toolRuntime <- None)

            for companion in this.Companions.Values |> Seq.toList do
                (companion :> IDisposable).Dispose()

            this.Companions.Clear()
            studentTeacherRuntime |> Option.iter (fun st -> st.Dispose())
            studentTeacherRuntime <- None
            SharedAgentJournal.release journal
            SharedTerminalBus.release sharedTerminalKey sharedTerminalPort
            sharedTerminalKey <- None
            sharedTerminalPort <- None

    interface IDisposable with
        member this.Dispose() = this.Dispose()
