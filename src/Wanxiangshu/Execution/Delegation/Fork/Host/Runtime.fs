namespace Wanxiangshu.Execution.Delegation.Fork.Host

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Microsoft.FSharp.Control
open Wanxiangshu.Execution.Delegation.Fork.ChildRecovery
open Wanxiangshu.Execution.Session.Recovery.SessionRecovery
open Wanxiangshu.OpenCode
open Wanxiangshu.Host
open Wanxiangshu.Process
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Participant.Persona.AgentRoleIdentity

/// Bridges real child sessions to the existing completion mailbox.
/// Fork / Reuse / Pty operations live in extension files (semantic split).
type HostForkRuntime
    (
        parentId: SessionId,
        sessions: ISessionHostPort,
        ?journal: AgentJournal,
        ?onChildCreated: string -> Role -> SessionId -> unit,
        ?onChildCreatedDir: string -> SessionId -> string option -> unit,
        ?ptyPort: PtyPort,
        ?directoryFor: string -> string option,
        ?onRunStarted: SessionId -> Role -> string option -> unit,
        ?parentWorkRecordFor: SessionId -> Task<string option>,
        ?childWorkRecordFor: SessionId -> Task<string option>,
        ?sessionSnapshot: ISessionSnapshotPort,
        ?cancelSignals: SessionId seq -> unit,
        /// REVIEW-007: a Manager's own review fork opens a barrier for the forked
        /// Reviewer. The Orchestrator's runtime keeps this off — it opens barriers
        /// itself (ORCH-006) — so exactly one writer owns each barrier.
        ?managerOpensReviewBarrier: bool,
        /// REVIEW-007: the Git tree hash of a forked Reviewer's directory, used to
        /// open the barrier. `None` for a directory with no readable tree: the
        /// Reviewer's verdict then fails closed under REVIEW-008, which is the
        /// correct outcome for a review without a tree.
        ?treeHashFor: string -> GitTreeHash option,
        /// GLORY-002 / SURFACE-006: ownership of every handle this runtime forks.
        /// The hidden Finality workflow passes `HostOwnedHidden` so its Reviewer
        /// never enters the Manager's list/join/guard or parent recovery.
        ?ownership: Fact.HandleOwnership,
        /// Injectable wall clock (PtyTiming.nodeClockPort at Host/Session composition).
        ?clock: IClockPort,
        /// Join one-shot deadline port (G4R-CE) — Host may inject; default Node timer.
        ?timerPort: ITimerPort
    ) as this =
    let clockPort = defaultArg clock (PtyTiming.nodeClockPort ())
    let timers = defaultArg timerPort (PtyTiming.nodeTimerPort ())
    let runtime = ForkRuntime(clock = clockPort, timerPort = timers)
    let children = Dictionary<string, SessionId>()
    // Join visibility is process ownership, not liveness. A run may already have
    // settled and left the live list while its completion is still waiting for join.
    let processOwnedAgents = HashSet<string>()
    // CRASH-018: explicit /continue discoveries are addressable for reuse but
    // are not owned by this process until a new charge actually reopens them.
    let dormantChildren = Dictionary<string, SessionId>()
    let pendingRuns = Dictionary<string, PendingHostRun>()
    let ptyRuns = HashSet<string>()
    /// Provider TerminalName → PtyId. Occupied until Join delivers closure.
    let terminalByName = Dictionary<string, string>()
    let ptyCompletionObservers = ResizeArray<PtyJoinItem -> unit>()
    let gate = obj ()
    let cancelGate = obj ()
    // DSL-MUTABLE: single-flight — duplicate joins fail before waiting
    let mutable joinInFlight = false
    // DSL-MUTABLE: resource — one parent-cancel drain owns durable abandon + physical teardown.
    let mutable cancelDrainTask: Task option = None

    // DSL-MUTABLE: resource — first prompts deferred until the review barrier
    // has durably opened (GLORY-040: barrier before assignment).
    let deferredFirstPrompts =
        Dictionary<
            string,
            {| ChildId: SessionId
               AgentName: string
               Prompt: string |}
         >()

    let directoryOf = defaultArg directoryFor (fun _ -> None)
    let childCreated = defaultArg onChildCreated (fun _ _ _ -> ())
    let childCreatedDir = defaultArg onChildCreatedDir (fun _ _ _ -> ())
    let runStarted = defaultArg onRunStarted (fun _ _ _ -> ())

    let parentWorkRecordOf =
        defaultArg parentWorkRecordFor (fun _ -> Task.FromResult None)

    let childWorkRecordOf =
        defaultArg childWorkRecordFor (fun _ -> Task.FromResult None)

    let convergedFissionWorkRecordOf (sessionId: SessionId) =
        task {
            match journal with
            | None -> return None
            | Some durable ->
                match
                    FissionProjection.tryLatestForOwner
                        sessionId
                        (AgentJournal.snapshot durable).AgentProjections.Fission
                with
                | Some { Terminal = FissionGroupTerminal.Converged(_, _, aggregateRef, aggregateDigest) } ->
                    match! durable.Writer.BlobWriter.Read aggregateRef with
                    | Ok text when HostDigest.sha256Hex text = BlobDigest.value aggregateDigest -> return Some text
                    | _ -> return None
                | _ -> return None
        }

    let cancelSignals = defaultArg cancelSignals (fun _ -> ())

    let ptyPortInstance = defaultArg ptyPort (PtyBackend.createPort ())
    let parentKey = SessionId.value parentId
    let handleOwnership = defaultArg ownership Fact.HandleOwnership.DurableParentHandle

    let sendChildPrompt =
        HostForkRunLifecycle.childPromptSender sessions parentId journal directoryOf

    let sendBusyNudge = HostForkBusyNudge.sender sessions parentId journal directoryOf

    let parentAbortToken = Pty.registerParentAbort parentKey (fun () -> this.Cancel())

    do
        ptyPortInstance.AddMailboxSender(fun item ->
            let id = PtyJoinItem.ptyId item
            let owned = lock gate (fun () -> ptyRuns.Contains id)

            if owned then
                // A PtyPort can be shared by multiple runtimes. Its sender fan-out
                // must not turn another runtime's exit into this runtime's join.
                runtime.PublishPtyCompletion item
                runtime.UnregisterPty id

                let observers = lock gate (fun () -> ptyCompletionObservers |> Seq.toList)

                for observer in observers do
                    try
                        observer item
                    with _ ->
                        ())
    // Cross-process recovery is not wired into ordinary HostForkRuntime lifecycle.
    // HostForkRestart remains a detached algorithm library for explicit resume flows.

    member internal _.Runtime = runtime
    member internal _.Children = children
    member internal _.DormantChildren = dormantChildren
    member internal _.PendingRuns = pendingRuns
    member internal _.PtyRuns = ptyRuns

    /// Fission admission snapshot: existing external work belongs to the logical
    /// owner and becomes broadcast completion sources. Snapshot identities only;
    /// no completion is consumed here.
    member _.SnapshotOutstandingAgentRuns() =
        lock gate (fun () ->
            pendingRuns.Values
            |> Seq.filter (fun run -> not run.Finished)
            |> Seq.map (fun run -> run.AgentId, run.ChildId)
            |> Seq.toList)

    member _.SnapshotOutstandingPtyRuns() =
        lock gate (fun () -> ptyRuns |> Seq.toList)

    member _.SubscribePtyCompletion(listener: PtyJoinItem -> unit) : IDisposable =
        lock gate (fun () -> ptyCompletionObservers.Add listener)

        { new IDisposable with
            member _.Dispose() =
                lock gate (fun () -> ptyCompletionObservers.Remove listener |> ignore) }

    member internal _.HandleOwnership = handleOwnership
    member internal _.DeferredFirstPrompts = deferredFirstPrompts
    member internal _.Clock = clockPort
    member internal _.Timers = timers
    /// Wall-clock read for Session extension modules (avoids raw DateTimeOffset stamps).
    member internal _.Now() = clockPort.UtcNow()

    /// GLORY-045: re-enlist a still-ungraduated historical Reviewer into this
    /// runtime before Fork, so Fork's existing-child path reuses the SAME Host
    /// session (X/Y context preserved) instead of creating a second one.
    member internal _.AdoptChild(agentId: string, childId: SessionId) : unit =
        lock gate (fun () ->
            children.[agentId] <- childId
            processOwnedAgents.Add agentId |> ignore)

    /// GLORY-040: deliver a first prompt that was deferred until its review
    /// barrier had durably opened. Idempotent per agent id: a second call with
    /// nothing pending is a no-op success.
    member this.SendDeferredFirstPrompt(agentId: string) : Task<Result<unit, string>> =
        task {
            let pendingOpt =
                lock gate (fun () ->
                    match deferredFirstPrompts.TryGetValue agentId with
                    | true, pending -> Some pending
                    | false, _ -> None)

            match pendingOpt with
            | None -> return Ok()
            | Some pending ->
                let! sent =
                    HostForkAgentOwner.sendFirstPrompt
                        this.Sessions
                        this.Journal
                        pending.ChildId
                        pending.AgentName
                        (this.DirectoryOf agentId)
                        pending.Prompt

                match sent with
                | Ok _ ->
                    lock gate (fun () -> deferredFirstPrompts.Remove agentId |> ignore)
                    return Ok()
                | Error err -> return Error err
        }

    /// Drop a deferSend preamble so process-review assignment can go out as
    /// AgentOwnerRoot instead of a busy nudge against a session with no profile.
    member _.DiscardDeferredFirstPrompt(agentId: string) : unit =
        lock gate (fun () -> deferredFirstPrompts.Remove agentId |> ignore)

    member internal _.Gate = gate
    member internal _.TerminalByName = terminalByName
    member internal _.Sessions = sessions
    member internal _.Journal = journal
    member internal _.SessionSnapshot = sessionSnapshot
    member internal _.ParentId = parentId
    member internal _.ParentKey = parentKey

    /// EXEC-017/018: one join waiter owns the runtime wake channels at a time.
    /// Join workflow (HostForkJoin) acquires/releases through these, so the
    /// latch stays with the state it guards.
    member internal _.TryAcquireJoin() : bool =
        lock gate (fun () ->
            if joinInFlight then
                false
            else
                joinInFlight <- true
                true)

    member internal _.ReleaseJoin() =
        lock gate (fun () -> joinInFlight <- false)

    member internal _.PtyPort = ptyPortInstance
    member internal _.DirectoryOf = directoryOf
    member internal _.RunStarted = runStarted
    member internal _.ChildCreated = childCreated
    member internal _.ChildCreatedDir = childCreatedDir
    member internal _.ParentWorkRecordOf = parentWorkRecordOf
    member internal _.ChildWorkRecordOf = childWorkRecordOf
    member internal _.SendChildPrompt = sendChildPrompt
    member internal _.SendBusyNudge = sendBusyNudge
    member internal _.ParentAbortToken = parentAbortToken

    member internal _.ManagerOpensReviewBarrier =
        defaultArg managerOpensReviewBarrier false

    member internal _.TreeHashFor = defaultArg treeHashFor (fun _ -> None)

    /// EXEC-009: retired OR abandoned ids must never re-fork under the same handle.
    member _.IsRetiredHandle(agentId: string) =
        journal
        |> Option.map (fun durable ->
            let projection = AgentJournal.handleProjection durable parentId
            let handle = HandleController.agentHandle agentId

            HandleProjection.isRetired handle projection
            || HandleProjection.isAbandoned handle projection)

    member this.Complete(run: PendingHostRun, outcome: TerminalOutcome) =
        // Terminal delivery is a synchronous Host callback. Fetch the canonical
        // child WorkRecord asynchronously, then hand the materialized value to the
        // lifecycle completion path in-order.
        task {
            let! workRecord =
                match outcome with
                | TerminalOutcome.Completed _ ->
                    task {
                        match! convergedFissionWorkRecordOf run.ChildId with
                        | Some aggregate -> return Some aggregate
                        | None -> return! childWorkRecordOf run.ChildId
                    }
                | _ -> Task.FromResult None

            do! HostForkRunLifecycle.complete gate pendingRuns journal parentId sessions run outcome workRecord
        }
        |> ignore

    member this.InstallRun(agentId: string, childId: SessionId, role: Role) =
        lock gate (fun () -> processOwnedAgents.Add agentId |> ignore)

        let run =
            HostForkRunLifecycle.installRun
                gate
                pendingRuns
                journal
                parentId
                sessions
                childWorkRecordOf
                agentId
                childId
                role

        runtime.BindChildSession(agentId, childId)
        runStarted childId role (directoryOf agentId)
        run

    member this.FailRun(run: PendingHostRun, error: string) =
        HostForkRunLifecycle.failRun gate pendingRuns journal parentId sessions run error

    member this.MarkReady(run: PendingHostRun) =
        // markReady is intentionally a no-op; do not perform an unnecessary WorkRecord read.
        HostForkRunLifecycle.markReady gate pendingRuns journal parentId sessions run None

    member this.CancelAndDrain() : Task =
        lock cancelGate (fun () ->
            match cancelDrainTask with
            | Some drain -> drain
            | None ->
                let drain =
                    HostForkChildDispatch.cancelParent
                        cancelSignals
                        // GREEN-4: no second recovery ownership; cancel does not start restore.
                        (fun () -> Task.FromResult(()))
                        runtime
                        ptyPortInstance
                        parentKey
                        parentAbortToken
                        gate
                        pendingRuns
                        children
                        sessions
                        journal
                        (journal
                         |> Option.map (fun durable -> AgentJournal.handleProjection durable parentId))
                        parentId
                        (fun run -> HostForkRunLifecycle.settleParentCancelled gate pendingRuns run)
                        (clockPort.UtcNow())
                    :> Task

                cancelDrainTask <- Some drain
                drain)

    member this.Cancel() : unit = this.CancelAndDrain() |> ignore

    member _.List() = runtime.List()

    member _.TryFindAgent(agentId: string) =
        runtime.List() |> fst |> List.tryFind (fun a -> a.AgentId = agentId)

    member internal _.OwnsAgent(agentId: string) =
        lock gate (fun () -> processOwnedAgents.Contains agentId)

    /// CRASH-018: explicit /continue may discover a physically surviving child.
    /// It stays dormant: addressable by a later explicit reuse, but excluded from
    /// this process's cancellation/teardown ownership until that reuse begins.
    member _.AdoptExisting(agentId: string, childId: SessionId, role: Role, agent: string) : unit =
        lock gate (fun () -> dormantChildren.[agentId] <- childId)
        runtime.Restore(agentId, role, agent)
        runtime.BindChildSession(agentId, childId)

    member internal _.TryReusableChild(agentId: string) : (SessionId * bool) option =
        lock gate (fun () ->
            match children.TryGetValue agentId, dormantChildren.TryGetValue agentId with
            | (true, childId), _ -> Some(childId, false)
            | _, (true, childId) -> Some(childId, true)
            | _ -> None)

    member internal _.ActivateDormantChild(agentId: string, childId: SessionId, role: Role) : unit =
        lock gate (fun () ->
            dormantChildren.Remove agentId |> ignore
            children.[agentId] <- childId
            processOwnedAgents.Add agentId |> ignore)

        childCreated agentId role childId

    member internal this.ActivateDormantChildIfNeeded
        (wasDormant: bool, agentId: string, childId: SessionId, role: Role)
        =
        if wasDormant then
            this.ActivateDormantChild(agentId, childId, role)

    /// The Host child session a forked agent id drives.
    ///
    /// ORCH-006 needs it right after a fork, to record `ManagerJobCreated`. The map is
    /// the same one restart recovery repopulates from `HandleLinked.ChildSessionId`, so
    /// a resumed job reads the session the Host actually issued rather than one derived
    /// from the agent id.
    member _.TryChildSession(agentId: string) : SessionId option =
        lock gate (fun () ->
            match children.TryGetValue agentId with
            | true, childId -> Some childId
            | false, _ -> None)

    member _.PendingRunCount = lock gate (fun () -> pendingRuns.Count)
    member _.PendingCompletionCount = runtime.PendingCompletionCount
    member _.IsCancelled = runtime.IsCancelled
