namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Journal
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

    member _.Journal = journal
    member val SessionDirectories = Dictionary<string, string>()
    member val OwnedSessions = HashSet<string>()
    member val UserMessageBindings = Dictionary<string, PhysicalUserMessageId>()
    member val SessionParents = Dictionary<string, string>()
    member val Companions = Dictionary<string, CompanionHost>()
    member val CompanionGate = obj ()
    member val VerdictSessions = HashSet<string>()
    member val NudgeSent = HashSet<string>()
    member val ManagerGuardNudges = HashSet<string>()
    member val AbortedSessions = HashSet<string>()
    member val RecoveryArming = Dictionary<string, SlotArming>()
    member val AttemptPlans = Dictionary<string, AttemptPlan>()

    member _.ArmRecovery(sessionId: SessionId) =
        RecoveryArming.[SessionId.value sessionId] <- RecoverySlot.afterFailureAdvance

    member _.TryRecoveryArming(sessionId: SessionId) =
        match RecoveryArming.TryGetValue(SessionId.value sessionId) with
        | true, arming -> Some arming
        | false, _ -> None

    member _.RecordAttemptPlan(sessionId: SessionId) (providerRun: ProviderRunIdentity) (plan: AttemptPlan) =
        AttemptPlans.[SessionId.value sessionId + "\u001f" + ProviderRunIdentity.value providerRun] <- plan

    member _.TryAttemptPlan(sessionId: SessionId) (providerRun: ProviderRunIdentity) =
        let key = SessionId.value sessionId + "\u001f" + ProviderRunIdentity.value providerRun

        match AttemptPlans.TryGetValue(key) with
        | true, plan -> Some plan
        | false, _ -> None

    member _.ClearRecovery(sessionId: SessionId) =
        RecoveryArming.Remove(SessionId.value sessionId) |> ignore

    member _.ClearAttemptPlan(sessionId: SessionId) (providerRun: ProviderRunIdentity) =
        AttemptPlans.Remove(SessionId.value sessionId + "\u001f" + ProviderRunIdentity.value providerRun) |> ignore

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
        this.AttemptPlans.Keys
        |> Seq.filter (fun key -> key.StartsWith(sessionId + "\u001f", StringComparison.Ordinal))
        |> Seq.toList
        |> List.iter (fun key -> this.AttemptPlans.Remove key |> ignore)

    member this.Dispose() =
        if not disposed then
            disposed <- true
            subscription |> Option.iter (fun active -> active.Dispose())
            subscription <- None

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
