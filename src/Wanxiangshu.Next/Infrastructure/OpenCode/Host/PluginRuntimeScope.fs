namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session

/// Session-scoped resource owner implemented by the tool runtime without
/// exposing its concrete dictionaries to the plugin composition root.
type ISessionRuntimeOwner =
    inherit IDisposable
    abstract DisposeSession: string -> unit
    abstract DisposeExecutorRuntime: string -> unit

/// Explicit lifetime root for one plugin instance. Collections here are either
/// physical resources, display caches, or bounded per-call deduplication.
type PluginRuntimeScope(journal: AgentJournal option) =
    let toolRuntimeGate = obj ()
    let mutable toolRuntime: ISessionRuntimeOwner option = None
    let mutable subscription: IDisposable option = None
    let mutable sharedTerminalKey: string option = None
    let mutable sharedTerminalPort: Events.HostEventPort option = None
    let mutable disposed = false

    /// ENFORCER-160/162: parked continuation transforms, keyed by session id.
    ///
    /// At most one parked transform per session (a session's step loop is
    /// serial, so two parks for one session cannot race in practice — the
    /// dictionary entry is the guard that makes the invariant structural).
    let parkedGate = obj ()
    let parked = Dictionary<string, ParkedTransform>()
    // ENFORCER-047/050: two physical slots — never share one dictionary.
    // CurrentRequest = InFlight cycle authority; PendingOffer = Parked next material.
    let currentRequest = Dictionary<string, BloggerRequestContext>()
    let pendingOffer = Dictionary<string, BloggerRequestContext>()
    let bloggerRuntime = Dictionary<string, BloggerRuntimeCell>()

    /// HOST-006: the first compaction setting the config hook could not establish.
    ///
    /// Recorded rather than thrown, because HOST-006's verdict needs both halves — the
    /// settings and the first turn's observation. Throwing at config time would report
    /// the symptom before the probe could say whether anything actually compacted.
    let mutable compactionSettingGap: Wanxiangshu.Next.Domain.CompactionSetting option =
        None

    /// HOST-006 startup probe latch, with its own gate.
    ///
    /// Not sharing `toolRuntimeGate`: two unrelated invariants behind one lock read as
    /// if they were related, and the next person to touch either has to prove they are
    /// not.
    let startupProbeGate = obj ()
    let mutable startupProbeDone = false

    /// PROMPT-011 post-init recovery gate.
    ///
    /// Created empty; `AttachRecoveryGate` seeds it with the journal and snapshot
    /// port once both exist (after `createHost`). Attaching is pure bookkeeping —
    /// no SDK call happens until the first real Host event calls `EnsureRecoveryDone`.
    let mutable recoveryGate: PromptRecovery.RecoveryGate option = None
    let mutable bloggerRecoveryGate: BloggerCrashRecovery.RecoveryGate option = None
    let recoveryGateLock = obj ()

    member _.Journal = journal
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
    member val ManagerGuardNudges = HashSet<string>()
    member val AbortedSessions = HashSet<string>()
    member val RecoveryArming = Dictionary<string, SlotArming>()
    member val AttemptPlans = Dictionary<string, AttemptPlan>()

    member this.AttachRecoveryGate(gate: PromptRecovery.RecoveryGate) =
        lock recoveryGateLock (fun () -> recoveryGate <- Some gate)

    member this.AttachBloggerRecoveryGate(gate: BloggerCrashRecovery.RecoveryGate) =
        lock recoveryGateLock (fun () -> bloggerRecoveryGate <- Some gate)

    /// PROMPT-011 + C5: await prompt claim recovery AND blogger crash-window pass.
    ///
    /// Safe to call from any event entry point: first caller starts each pass,
    /// later callers await the same tasks. A journal-less or snapshot-less scope
    /// has nothing to reconcile and completes immediately.
    member this.EnsureRecoveryDone() : System.Threading.Tasks.Task =
        task {
            let prompt =
                lock recoveryGateLock (fun () -> recoveryGate)
                |> Option.map (fun gate -> gate.EnsureDone())
                |> Option.defaultValue (AsyncSupport.completedTask ())

            let blogger =
                lock recoveryGateLock (fun () -> bloggerRecoveryGate)
                |> Option.map (fun gate -> gate.EnsureDone())
                |> Option.defaultValue (AsyncSupport.completedTask ())

            do! prompt
            do! blogger
        }
        :> System.Threading.Tasks.Task

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

                currentRequest.Remove sessionId |> ignore
                pendingOffer.Remove sessionId |> ignore
                // Park cancel/timeout leaves the logical Blogger idle, not disposed.
                match bloggerRuntime.TryGetValue sessionId with
                | true, cell when cell.State = BloggerRuntimeState.Disposed -> bloggerRuntime.[sessionId] <- cell
                | _ -> bloggerRuntime.[sessionId] <- BloggerRuntime.empty)

        member this.HasParked(sessionId: string) : bool =
            lock parkedGate (fun () -> parked.ContainsKey sessionId)

        member this.SetCurrentRequest(sessionId: string, context: BloggerRequestContext) : unit =
            lock parkedGate (fun () -> currentRequest.[sessionId] <- context)

        member this.TryPeekCurrentRequest(sessionId: string) : BloggerRequestContext option =
            lock parkedGate (fun () ->
                match currentRequest.TryGetValue sessionId with
                | true, context -> Some context
                | false, _ -> None)

        member this.ClearCurrentRequest(sessionId: string) : unit =
            lock parkedGate (fun () -> currentRequest.Remove sessionId |> ignore)

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

        member this.GetBloggerRuntime(sessionId: string) : BloggerRuntimeCell =
            lock parkedGate (fun () -> this.GetBloggerRuntimeUnlocked sessionId)

        member this.SetBloggerRuntime(sessionId: string, cell: BloggerRuntimeCell) : unit =
            lock parkedGate (fun () -> bloggerRuntime.[sessionId] <- cell)

    member private _.GetBloggerRuntimeUnlocked(sessionId: string) : BloggerRuntimeCell =
        match bloggerRuntime.TryGetValue sessionId with
        | true, cell -> cell
        | false, _ -> BloggerRuntime.empty

    /// HOST-006 prevention layer: the config hook's finding.
    ///
    /// Written once at config time, read once by the startup probe. Not a collection
    /// because there is one verdict per plugin instance — the settings are
    /// instance-global (`config/config.ts:607`), not per session.
    member _.RecordCompactionSettingGap(gap: Wanxiangshu.Next.Domain.CompactionSetting option) =
        compactionSettingGap <- gap

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
    member _.CompactionProbePending =
        lock startupProbeGate (fun () -> not startupProbeDone)

    member _.AttachToolRuntime(owner: ISessionRuntimeOwner) =
        lock toolRuntimeGate (fun () -> toolRuntime <- Some owner)

    member _.TrackSubscription(value: IDisposable option) = subscription <- value

    member _.AttachSharedTerminal(key: string option, port: Events.HostEventPort option) =
        sharedTerminalKey <- key
        sharedTerminalPort <- port

    member _.DisposeExecutorRuntime(sessionId: string) =
        lock toolRuntimeGate (fun () ->
            toolRuntime |> Option.iter (fun owner -> owner.DisposeExecutorRuntime sessionId))

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

        // Always cancel the deleted id; also cancel linked Blogger keys.
        let cancelKeys =
            (sessionId :: linkedBloggerKeys) |> List.distinct

        for key in cancelKeys do
            (this :> IParkedTransformHost).CancelParked key

            lock parkedGate (fun () ->
                bloggerRuntime.[key] <- BloggerRuntime.onDispose BloggerRuntime.empty
                bloggerRuntime.Remove key |> ignore)

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
                currentRequest.Clear()
                pendingOffer.Clear()
                bloggerRuntime.Clear())

            lock toolRuntimeGate (fun () ->
                toolRuntime |> Option.iter (fun owner -> owner.Dispose())
                toolRuntime <- None)

            for companion in this.Companions.Values |> Seq.toList do
                (companion :> IDisposable).Dispose()

            this.Companions.Clear()
            SharedAgentJournal.release journal
            SharedTerminalBus.release sharedTerminalKey sharedTerminalPort
            sharedTerminalKey <- None
            sharedTerminalPort <- None

    interface IDisposable with
        member this.Dispose() = this.Dispose()
