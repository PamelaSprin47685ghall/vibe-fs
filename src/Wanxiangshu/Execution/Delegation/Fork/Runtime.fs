namespace Wanxiangshu.Execution.Delegation.Fork

open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Execution.Agent
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Process

module private ForkRuntimeControl =
    let runChildWork
        (workOpt: (unit -> Task<AgentCompletionOutcome>) option)
        (childRunner: string -> Role -> string option -> Task<AgentCompletionOutcome>)
        (agentId: string)
        (role: Role)
        (promptOpt: string option)
        =
        match workOpt with
        | Some work -> work ()
        | None -> childRunner agentId role promptOpt

    let publishAgentCompletion
        (mailbox: CompletionMailbox)
        (terminalListener: RunCompletion -> unit)
        (childRun: ChildRun)
        (agentId: string)
        (completion: RunCompletion)
        =
        childRun.Completion.TrySet(completion) |> ignore
        mailbox.PulseAgentHandle(AgentHandleId.create agentId)

        try
            terminalListener completion
        with _ ->
            ()

    let forkCreatedOrNudged (isNew: bool) (agentId: string) =
        if isNew then ForkResult.Created agentId else ForkResult.Nudged agentId

    let awaitPendingNoTimeout (pending: Task<RunCompletion>) =
        task {
            let! value = pending
            return Ok value
        }

    let awaitPendingWithTimeout (agentId: string) (pending: Task<RunCompletion>) (ms: int) =
        task {
            let! completedFirst = PtyTiming.raceExit (pending :> Task) ms

            if completedFirst then
                let! value = pending
                return Ok value
            else
                return Error(sprintf "await agent timed out: %s" agentId)
        }

    let awaitKnownAgent (agentId: string) (pending: Task<RunCompletion>) (timeoutMs: int option) =
        match timeoutMs with
        | None -> awaitPendingNoTimeout pending
        | Some ms when ms <= 0 -> Task.FromResult(Error(sprintf "await agent timed out: %s" agentId))
        | Some ms -> awaitPendingWithTimeout agentId pending ms

    let tryCleanupAgent (cleanupPort: string -> unit) (id: string) =
        try
            cleanupPort id
        with _ ->
            ()

    let cleanupAgentIds (cleanupPort: string -> unit) (ids: string list) =
        for id in ids do
            tryCleanupAgent cleanupPort id

/// Runtime for managing child agent runs and PTY sessions.
///
/// Uses a Map<AgentId, ChildRun> for agent tracking (replacing the previous
/// Dictionary<AgentId, AgentRecord> + Queue pattern) and dual-channel mailbox:
/// agent wake-only + PTY PtyJoinItem queue (GREEN-5).
///
/// All public members are thread-safe (lockObj synchronizes map/mailbox access).
type ForkRuntime
    (
        ?runner: string -> Role -> string option -> Task<AgentCompletionOutcome>,
        ?listener: RunCompletion -> unit,
        ?cleanup: string -> unit,
        /// Injectable wall clock (PtyTiming.nodeClockPort at Host/Session composition).
        ?clock: IClockPort,
        /// Join budget timer (G4R-CE) — default Node timer port.
        ?timerPort: ITimerPort
    ) =

    let childRunner =
        defaultArg runner (fun agentId role prompt ->
            Task.FromResult(AgentCompletion.ofSimpleText agentId "run-local" role (defaultArg prompt "ok")))

    let terminalListener = defaultArg listener ignore
    let cleanupPort = defaultArg cleanup ignore
    let clockPort = defaultArg clock (PtyTiming.nodeClockPort ())
    let timers = defaultArg timerPort (PtyTiming.nodeTimerPort ())

    // DSL-MUTABLE: resource — live child agent registry under lockObj
    let mutable agents: Map<string, ChildRun> = Map.empty
    // DSL-MUTABLE: resource — live pty registry under lockObj
    let mutable ptys: Map<string, PtyRecord> = Map.empty
    let lockObj = obj ()

    let mailbox =
        CompletionMailbox(
            lockObj,
            (fun () ->
                agents |> Map.exists (fun _ run -> ChildRun.isActive run)
                || not (Map.isEmpty ptys)),
            timerPort = timers,
            clockPort = clockPort
        )

    let cancelAllAgentsAndClearPtys () =
        lock lockObj (fun () ->
            for run in agents |> Map.toSeq |> Seq.map snd do
                ChildRun.cancel run

            ptys <- Map.empty
            agents |> Map.toSeq |> Seq.map fst |> Seq.toList)

    /// Start a new child run: create the ChildRun and return a thunk that
    /// executes the work. Agent completion settles the cell + pulses wake only;
    /// durable projection (Host path) is the join fact source.
    let startRun
        (agentId: string)
        (agentName: string)
        (role: Role)
        (promptOpt: string option)
        (workOpt: (unit -> Task<AgentCompletionOutcome>) option)
        =
        let promptVal = defaultArg promptOpt ""
        let runId = "run-" + Guid.NewGuid().ToString("N").Substring(0, 8)

        let childRun =
            ChildRun.create agentId runId agentName role promptVal (clockPort.UtcNow())

        let runTask () =
            task {
                let work (_ct: CancellationToken) =
                    task {
                        try
                            return!
                                ForkRuntimeControl.runChildWork workOpt childRunner agentId role promptOpt
                        with ex ->
                            return AgentCompletion.ofSimpleError agentId runId role ex.Message
                    }

                let! result = ChildRunProgram.run childRun work childRun.Cancellation.Token clockPort.UtcNow

                // P0-RECOVERY-JOIN-001: ParentCancelled → durable HandleAbandoned
                // (cancelChildren). Do not mint aborted cell / SetResult.
                // Join surfaces Abandoned via projection, not HandleCompleted(ABORTED).
                let completionOpt =
                    match result with
                    | Error AgentError.ParentCancelled -> None
                    | Ok value -> Some value
                    | Error(AgentError.InvalidFork message)
                    | Error(AgentError.HostFailure message)
                    | Error(AgentError.SessionDead message) ->
                        Some(ChildRun.makeFailed childRun message (clockPort.UtcNow()))

                match completionOpt with
                | None -> ()
                | Some completion ->
                    ForkRuntimeControl.publishAgentCompletion mailbox terminalListener childRun agentId completion
            }

        childRun, runTask

    let startReplacementRun
        (agentId: string)
        (agentName: string)
        (role: Role)
        (prompt: string option)
        (runWork: (unit -> Task<AgentCompletionOutcome>) option)
        =
        let isNew = not (agents |> Map.containsKey agentId)
        let childRun, runTask = startRun agentId agentName role prompt runWork
        agents <- agents |> Map.add agentId childRun
        runTask () |> ignore
        ForkRuntimeControl.forkCreatedOrNudged isNew agentId

    // -----------------------------------------------------------------------
    // Public API — Fork
    // -----------------------------------------------------------------------

    /// Fork a new run for the given agent. Returns:
    ///   - Created  for a brand-new agent
    ///   - Nudged   for an existing agent (busy or idle)
    ///   - NotFound if the runtime is cancelled
    member this.Fork
        (agentId: string, role: Role, agent: string, ?prompt: string, ?runWork: unit -> Task<AgentCompletionOutcome>)
        : ForkResult =
        // PROMPT-008: the managed agent name is required, never defaulted.
        // Defaulting to `fast-ROLE` invented a tier nobody selected, and the
        // invented name then flowed into the completion record and the Host send
        // boundary as if it had been chosen.
        let agentName = agent.Trim()

        lock lockObj (fun () ->
            let existing = agents |> Map.tryFind agentId

            if mailbox.IsCancelled then
                ForkResult.NotFound agentId
            elif existing |> Option.exists ChildRun.isActive then
                // Busy agent: nudge only, no new run created.
                ForkResult.Nudged agentId
            else
                // New agent OR idle existing agent with completed run:
                // replace the old ChildRun and start a fresh run.
                startReplacementRun agentId agentName role prompt runWork)

    // -----------------------------------------------------------------------
    // Public API — Join / signal / drain (EXEC-017 / EXEC-018 / GREEN-5)
    // -----------------------------------------------------------------------

    /// Compatibility single-result join (PTY mailbox only). Prefer WaitForSignal + drains.
    member _.Join(?timeoutMs: int) : Task<Result<RunCompletion, ForkError>> =
        match timeoutMs with
        | Some ms -> mailbox.Join(timeoutMs = ms)
        | None -> mailbox.Join()

    /// EXEC-018: wait for completion/cancel signal or typed local interrupt.
    member _.WaitForSignal(interrupt: Task<JoinInterruptReason>) : Task<MailboxWakeReason> =
        mailbox.WaitForSignal interrupt

    /// Wake on Publish/Pulse/Cancel only. Outer layer races journal + user interrupt.
    member _.WaitForWake() : Task<MailboxWakeReason> = mailbox.WaitForWake()

    /// Drop pending wake waiters (spurious CompletionMayBeAvailable). Safe: re-drain.
    member _.PulseWake() : unit = mailbox.PulseWake()

    /// Agent wake only — never carries completion payload (GREEN-5).
    member _.PulseAgentHandle(handle: AgentHandleId) : unit = mailbox.PulseAgentHandle handle

    /// PTY physical result (EXEC-015).
    member _.PublishPtyCompletion(item: PtyJoinItem) : unit =
        let id = PtyJoinItem.ptyId item

        let owned = lock lockObj (fun () -> ptys |> Map.containsKey id)

        if owned then
            mailbox.PublishPtyCompletion item

    /// Drain agent wake tokens (no payload). Callers re-read Journal.
    member _.DrainAgentWakes(maxCount: int) : AgentHandleId list = mailbox.DrainAgentWakes maxCount

    /// EXEC-018: bounded drain of PTY fact queue.
    member _.DrainPtyCompletions(maxCount: int) : PtyJoinItem list = mailbox.DrainPtyCompletions maxCount

    // -----------------------------------------------------------------------
    // Public API — PTY management
    // -----------------------------------------------------------------------

    member _.RegisterPty(pty: PtyRecord) : unit =
        lock lockObj (fun () -> ptys <- ptys |> Map.add pty.PtyId pty)

    member _.UnregisterPty(ptyId: string) : unit =
        lock lockObj (fun () -> ptys <- ptys |> Map.remove ptyId)

    // -----------------------------------------------------------------------
    // Public API — agent lifecycle for restart recovery
    // -----------------------------------------------------------------------

    member _.Restore(agentId: string, role: Role, agent: string) : unit =
        let agentName = agent.Trim()

        lock lockObj (fun () ->
            if not (Map.containsKey agentId agents) then
                agents <- ForkRecovery.restore agentId agentName role (clockPort.UtcNow()) agents)

    member internal _.Clock = clockPort

    member _.MarkInterrupted(agentId: string, reason: string) : unit =
        lock lockObj (fun () -> agents <- ForkRecovery.markInterrupted agentId reason agents)

    member _.BindChildSession(agentId: string, childSessionId: SessionId) : unit =
        lock lockObj (fun () -> agents <- ForkRecovery.bindChildSession agentId childSessionId agents)

    /// Internal targeted completion handle. Model-visible join remains join-any.
    /// Optional timeoutMs races the completion cell via PtyTiming.raceExit.
    member _.AwaitAgent(agentId: string, ?timeoutMs: int) : Task<Result<RunCompletion, string>> =
        let completion =
            lock lockObj (fun () -> agents |> Map.tryFind agentId |> Option.map (fun run -> run.Completion.Await))

        match completion with
        | None -> Task.FromResult(Error(sprintf "Unknown agent id: %s" agentId))
        | Some pending -> ForkRuntimeControl.awaitKnownAgent agentId pending timeoutMs

    /// Cancel one agent run without tearing down the whole runtime mailbox.
    member _.CancelAgent(agentId: string) : unit =
        lock lockObj (fun () ->
            match agents |> Map.tryFind agentId with
            | Some run -> ChildRun.cancel run
            | None -> ())

    // -----------------------------------------------------------------------
    // Public API — List
    // -----------------------------------------------------------------------

    /// Returns (agentRecords, ptyRecords) derived from the current state.
    member _.List() : AgentRecord list * PtyRecord list =
        lock lockObj (fun () ->
            let agentList =
                agents
                |> Map.toList
                |> List.map (fun (agentId, run) -> ChildRunProjection.toRecord mailbox.IsCancelled agentId run)

            let ptyList = ptys |> Map.toList |> List.map snd
            (agentList, ptyList))

    // -----------------------------------------------------------------------
    // Public API — status queries
    // -----------------------------------------------------------------------

    member _.IsCancelled = mailbox.IsCancelled

    member _.ActiveRunCount =
        lock lockObj (fun () -> agents |> Map.filter (fun _ run -> ChildRun.isActive run) |> Map.count)

    member _.PendingCompletionCount = mailbox.PendingCount
    member _.PendingPtyCount = mailbox.PendingPtyCount

    // -----------------------------------------------------------------------
    // Public API — Cancel / Close
    // -----------------------------------------------------------------------

    /// Cancel all active runs and PTYs, drain pending waiters.
    member _.Cancel() : unit =
        if mailbox.Cancel() then
            cancelAllAgentsAndClearPtys ()
            |> ForkRuntimeControl.cleanupAgentIds cleanupPort

    member this.Close() : unit = this.Cancel()
