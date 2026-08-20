namespace Wanxiangshu.Execution.Delegation.Fork.Host

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Context.Trace
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
open Wanxiangshu.Mission.Obligation.Todo

/// Bridges real child sessions to the existing completion mailbox.
/// Fork / Reuse / Pty operations live in extension files (semantic split).
type HostForkRuntime
    (
        parentId: SessionId,
        sessions: ISessionHostPort,
        childWorkRecordForRun: SessionId -> MagicTodoLwr.BoundedRange -> ProviderRunIdentity -> Task<string option>,
        ?journal: AgentJournal,
        ?onChildCreated: string -> Role -> SessionId -> unit,
        ?onChildCreatedDir: string -> SessionId -> string option -> unit,
        ?ptyPort: PtyPort,
        ?directoryFor: string -> string option,
        ?onRunStarted: SessionId -> Role -> string option -> unit,
        ?parentWorkRecordFor: SessionId -> Task<string option>,
        ?childWorkRecordFor: SessionId -> Task<string option>,
        ?handoff: ReusableHandoffPort,
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
        ?ownership: HandleOwnership,
        /// Injectable wall clock (PtyTiming.nodeClockPort at Host/Session composition).
        ?clock: IClockPort
    ) as this =
    let clockPort = defaultArg clock (PtyTiming.nodeClockPort ())
    let runtime = ForkRuntime(clock = clockPort)
    // DSL-MUTABLE: resource — live child session registry by agent id
    let children = Dictionary<string, SessionId>()
    // DSL-MUTABLE: resource — process-owned agent handle set
    let processOwnedAgents = HashSet<string>()
    // DSL-MUTABLE: resource — dormant /continue child registry by agent id
    let dormantChildren = Dictionary<string, SessionId>()
    // DSL-MUTABLE: resource — pending host run registry by agent id
    let pendingRuns = Dictionary<string, PendingHostRun>()
    // DSL-MUTABLE: resource — PTY run id set owned by this runtime
    let ptyRuns = HashSet<string>()
    /// Provider TerminalName → PtyId. Occupied until Join delivers closure.
    // DSL-MUTABLE: resource — terminal name to PtyId map
    let terminalByName = Dictionary<string, string>()
    let ptyCompletionObservers = ResizeArray<PtyJoinItem -> unit>()
    let gate = obj ()
    let cancelGate = obj ()
    let ownedWorkGate = obj ()
    // DSL-MUTABLE: single-flight — duplicate joins fail before waiting
    let mutable joinInFlight = false
    // DSL-MUTABLE: resource — one parent-cancel drain owns durable abandon + physical teardown.
    let mutable cancelDrainTask: Task option = None
    // DSL-MUTABLE: resource — terminal/failure callback admission latch.
    let mutable acceptingOwnedWork = true
    // DSL-MUTABLE: resource — in-flight runtime-owned callback count.
    let mutable ownedWorkCount = 0
    // DSL-MUTABLE: single-flight — shared waiter for callback drain.
    let mutable ownedWorkDrainWaiter: TaskCompletionSource<unit> option = None
    // DSL-MUTABLE: resource — first runtime-owned callback failure for shutdown propagation.
    let mutable ownedWorkFailure: exn option = None

    let finishOwnedWork () =
        lock ownedWorkGate (fun () ->
            ownedWorkCount <- ownedWorkCount - 1

            if not acceptingOwnedWork && ownedWorkCount = 0 then
                ownedWorkDrainWaiter
                |> Option.iter (fun waiter -> AsyncSupport.trySetResult waiter () |> ignore))

    let recordOwnedWorkFailure (failure: exn) =
        lock ownedWorkGate (fun () -> ownedWorkFailure <- Option.orElse ownedWorkFailure (Some failure))

    let captureOwnedWorkFailure (work: unit -> Task) : Task<exn option> =
        task {
            try
                do! work ()
                return None
            with ex ->
                return Some ex
        }

    let observeOwnedWork (work: unit -> Task) : Task =
        task {
            let! failure = captureOwnedWorkFailure work
            failure |> Option.iter recordOwnedWorkFailure
            finishOwnedWork ()
        }
        :> Task

    let startOwnedWork (work: unit -> Task) : Task =
        let admitted =
            lock ownedWorkGate (fun () ->
                if not acceptingOwnedWork then
                    false
                else
                    ownedWorkCount <- ownedWorkCount + 1
                    true)

        if admitted then
            observeOwnedWork work
        else
            Task.FromResult(()) :> Task

    let ownedWorkDrainTask () : Task =
        match ownedWorkDrainWaiter with
        | Some waiter -> waiter.Task :> Task
        | None ->
            let waiter =
                TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

            ownedWorkDrainWaiter <- Some waiter
            waiter.Task :> Task

    let stopOwnedWorkAndDrain () : Task =
        let waiting =
            lock ownedWorkGate (fun () ->
                acceptingOwnedWork <- false

                if ownedWorkCount = 0 then
                    Task.FromResult(()) :> Task
                else
                    ownedWorkDrainTask ())

        task {
            do! waiting

            match lock ownedWorkGate (fun () -> ownedWorkFailure) with
            | Some failure -> return raise failure
            | None -> return ()
        }
        :> Task

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

    let childWorkRecordOfRun = childWorkRecordForRun

    let handoffPort = handoff

    let xTraceHead (sessionId: SessionId) : int64 =
        match journal with
        | None -> 0L
        | Some durable ->
            AgentJournal.snapshot durable
            |> fun snapshot -> AgentProjection.tryFind sessionId snapshot.AgentProjections
            |> Option.bind (fun session -> session.XTrace)
            |> Option.map XTraceProjection.head
            |> Option.defaultValue 0L

    let workRecordForOutcome (run: PendingHostRun) (outcome: TerminalOutcome) =
        HostForkRunLifecycle.workRecordForOutcome childWorkRecordOfRun xTraceHead run outcome

    let cancelSignals = defaultArg cancelSignals (fun _ -> ())

    let ptyPortInstance = defaultArg ptyPort (PtyBackend.createPort ())
    let parentKey = SessionId.value parentId
    let handleOwnership = defaultArg ownership HandleOwnership.DurableParentHandle

    let sendChildPrompt =
        HostForkRunLifecycle.childPromptSender sessions parentId journal directoryOf

    let sendBusyNudge = HostForkBusyNudge.sender sessions parentId journal directoryOf

    let parentAbortToken = Pty.registerParentAbort parentKey (fun () -> this.Cancel())

    let notifyPtyObserver observer item =
        try
            observer item
        with _ ->
            ()

    let notifyPtyObservers item =
        let observers = lock gate (fun () -> ptyCompletionObservers |> Seq.toList)

        for observer in observers do
            notifyPtyObserver observer item

    do
        ptyPortInstance.AddMailboxSender(fun item ->
            let id = PtyJoinItem.ptyId item
            let owned = lock gate (fun () -> ptyRuns.Contains id)

            if owned then
                // A PtyPort can be shared by multiple runtimes. Its sender fan-out
                // must not turn another runtime's exit into this runtime's join.
                runtime.PublishPtyCompletion item
                runtime.UnregisterPty id
                notifyPtyObservers item)
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
        let pendingRunForAgent () =
            lock gate (fun () ->
                match pendingRuns.TryGetValue agentId with
                | true, run -> Some run
                | false, _ -> None)

        let failPendingRun error =
            match pendingRunForAgent () with
            | Some run -> this.FailRun(run, error)
            | None -> Task.FromResult(()) :> Task

        let deliverDeferredPrompt
            (pending:
                {| ChildId: SessionId
                   AgentName: string
                   Prompt: string |})
            =
            task {
                let! sent =
                    HostForkAgentOwner.sendFirstPromptObserved
                        this.Sessions
                        this.Journal
                        pending.ChildId
                        pending.AgentName
                        (this.DirectoryOf agentId)
                        pending.Prompt
                        (fun physical ->
                            pendingRunForAgent ()
                            |> Option.iter (fun run -> HostForkRunLifecycle.bindAuthorityRoot run physical))
                        failPendingRun

                return
                    match sent with
                    | Ok _ ->
                        lock gate (fun () -> deferredFirstPrompts.Remove agentId |> ignore)
                        Ok()
                    | Error err -> Error err
            }

        task {
            let pendingOpt =
                lock gate (fun () ->
                    match deferredFirstPrompts.TryGetValue agentId with
                    | true, pending -> Some pending
                    | false, _ -> None)

            match pendingOpt with
            | None -> return Ok()
            | Some pending -> return! deliverDeferredPrompt pending
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
    member internal _.ChildWorkRecordOfRun = childWorkRecordOfRun
    member internal _.XTraceHead = xTraceHead
    member internal _.HandoffPort = handoffPort

    member internal _.PrepareHandoff(route: DelegationHandoffRoute) : Task<Result<PreparedDelegationHandoff, string>> =
        match handoffPort with
        | Some port ->
            task {
                let! prepared = port.Prepare parentId route
                return Ok prepared
            }
        | None -> Task.FromResult(Error "reusable delegation handoff capability is unavailable")

    member internal _.TrackOwnedWork(work: unit -> Task) = startOwnedWork work |> ignore
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
        // Terminal delivery is a synchronous Host callback. The runtime owns the
        // async tail so shutdown can drain it before releasing the Journal.
        startOwnedWork (fun () ->
            task {
                let! workRecord = workRecordForOutcome run outcome
                do! HostForkRunLifecycle.complete gate pendingRuns journal parentId sessions handoffPort run outcome workRecord
            }
            :> Task)
        |> ignore

    member this.InstallRun
        (agentId: string, childId: SessionId, role: Role, ?preparedHandoff: PreparedDelegationHandoff)
        =
        lock gate (fun () -> processOwnedAgents.Add agentId |> ignore)

        let run =
            HostForkRunLifecycle.installRun
                gate
                pendingRuns
                journal
                parentId
                sessions
                childWorkRecordOfRun
                xTraceHead
                (fun work -> startOwnedWork work |> ignore)
                handoffPort
                preparedHandoff
                agentId
                childId
                role

        runtime.BindChildSession(agentId, childId)
        runStarted childId role (directoryOf agentId)
        run

    member this.FailRun(run: PendingHostRun, error: string) : Task =
        startOwnedWork (fun () ->
            HostForkRunLifecycle.failRun gate pendingRuns journal parentId sessions handoffPort run error)

    member this.MarkReady(run: PendingHostRun) =
        // markReady is intentionally a no-op; do not perform an unnecessary WorkRecord read.
        HostForkRunLifecycle.markReady gate pendingRuns journal parentId sessions run None

    member private _.WorkRecordFromCompletion(completion: RunCompletion) : Result<string, string> =
        match completion.Outcome with
        | AgentCompleted payload when not (String.IsNullOrWhiteSpace payload.WorkRecord) -> Ok payload.WorkRecord
        | AgentCompleted _ -> Error "reusable fork completed without bounded delta WorkRecord"
        | AgentFailed payload -> Error payload.Message
        | AgentAbandoned(_, reason) -> Error reason

    member internal this.AwaitCurrentWorkRecord(agentId: string) : Task<Result<string, string>> =
        taskResult {
            let! completion = runtime.AwaitAgent agentId
            return! this.WorkRecordFromCompletion completion
        }

    member this.CancelAndDrain() : Task =
        lock cancelGate (fun () ->
            match cancelDrainTask with
            | Some drain -> drain
            | None ->
                let drain =
                    task {
                        // Close terminal/failure callback admission first and let
                        // callbacks that already observed a terminal settle their
                        // durable completion before parent-cancel claims leftovers.
                        do! stopOwnedWorkAndDrain ()

                        do!
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
                    }
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
