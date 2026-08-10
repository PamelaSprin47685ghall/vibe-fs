namespace Wanxiangshu.Session

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Agent
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Process

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
                let work (ct: CancellationToken) =
                    task {
                        try
                            match workOpt with
                            | Some w -> return! w ()
                            | None -> return! childRunner agentId role promptOpt
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
                    childRun.Completion.TrySet(completion) |> ignore
                    // GREEN-5: agent mailbox is wake-only; no payload publish.
                    mailbox.PulseAgentHandle(AgentHandleId.create agentId)

                    try
                        terminalListener completion
                    with _ ->
                        ()
            }

        childRun, runTask

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
            if mailbox.IsCancelled then
                ForkResult.NotFound agentId
            else
                match agents |> Map.tryFind agentId with
                | Some run when ChildRun.isActive run ->
                    // Busy agent: nudge only, no new run created.
                    ForkResult.Nudged agentId

                | _ ->
                    // New agent OR idle existing agent with completed run:
                    // replace the old ChildRun and start a fresh run.
                    let isNew = not (agents |> Map.containsKey agentId)
                    let childRun, runTask = startRun agentId agentName role prompt runWork
                    agents <- agents |> Map.add agentId childRun
                    runTask () |> ignore

                    if isNew then
                        ForkResult.Created agentId
                    else
                        ForkResult.Nudged agentId)

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
        | Some pending ->
            match timeoutMs with
            | None ->
                task {
                    let! value = pending
                    return Ok value
                }
            | Some ms when ms <= 0 -> Task.FromResult(Error(sprintf "await agent timed out: %s" agentId))
            | Some ms ->
                task {
                    let! completedFirst = PtyTiming.raceExit (pending :> Task) ms

                    if completedFirst then
                        let! value = pending
                        return Ok value
                    else
                        return Error(sprintf "await agent timed out: %s" agentId)
                }

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
            let ids =
                lock lockObj (fun () ->
                    for run in agents |> Map.toSeq |> Seq.map snd do
                        ChildRun.cancel run

                    ptys <- Map.empty
                    agents |> Map.toSeq |> Seq.map fst |> Seq.toList)

            for id in ids do
                try
                    cleanupPort id
                with _ ->
                    ()

    member this.Close() : unit = this.Cancel()
