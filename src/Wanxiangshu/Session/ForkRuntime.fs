namespace Wanxiangshu.Session

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Agent
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// Runtime for managing child agent runs and PTY sessions.
///
/// Uses a Map<AgentId, ChildRun> for agent tracking (replacing the previous
/// Dictionary<AgentId, AgentRecord> + Queue pattern) and keeps a simple
/// completion mailbox for Join().
///
/// All public members are thread-safe (lockObj synchronizes map/mailbox access).
type ForkRuntime
    (
        ?runner: string -> AgentRole -> string option -> Task<AgentCompletionOutcome>,
        ?listener: RunCompletion -> unit,
        ?cleanup: string -> unit,
        ?publishToMailbox: bool
    ) =

    let childRunner =
        defaultArg runner (fun agentId role prompt ->
            Task.FromResult(AgentCompletion.ofSimpleText agentId "run-local" role (defaultArg prompt "ok")))

    let terminalListener = defaultArg listener ignore
    let cleanupPort = defaultArg cleanup ignore
    let publishCompletion = defaultArg publishToMailbox true

    let mutable agents: Map<string, ChildRun> = Map.empty
    let mutable ptys: Map<string, PtyRecord> = Map.empty
    let lockObj = obj ()

    let mailbox =
        CompletionMailbox(
            lockObj,
            fun () ->
                agents |> Map.exists (fun _ run -> ChildRun.isActive run)
                || not (Map.isEmpty ptys)
        )

    /// Start a new child run: create the ChildRun and return a thunk that
    /// executes the work and posts the completion to the mailbox.
    let startRun
        (agentId: string)
        (agentName: string)
        (role: AgentRole)
        (promptOpt: string option)
        (workOpt: (unit -> Task<AgentCompletionOutcome>) option)
        =
        let promptVal = defaultArg promptOpt ""
        let runId = "run-" + Guid.NewGuid().ToString("N").Substring(0, 8)
        let childRun = ChildRun.create agentId runId agentName role promptVal

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

                let flow = ChildRunProgram.run childRun work

                let! result =
                    AgentProgram.runAgentFlow
                        { SessionId = agentId
                          AgentName = agentName }
                        childRun.Cancellation.Token
                        flow

                let completion =
                    match result with
                    | Ok value -> value
                    | Error error ->
                        match error with
                        | AgentError.ParentCancelled -> ChildRun.makeAborted childRun "parent cancelled"
                        | AgentError.InvalidFork message
                        | AgentError.HostFailure message
                        | AgentError.SessionDead message -> ChildRun.makeFailed childRun message

                // Set the single-assignment completion cell exactly once.
                childRun.Completion.TrySet(completion) |> ignore

                if publishCompletion then
                    mailbox.Publish completion

                // Notify the external listener (used for tests and PTY sender).
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
        (
            agentId: string,
            role: AgentRole,
            agent: string,
            ?prompt: string,
            ?runWork: unit -> Task<AgentCompletionOutcome>
        ) : ForkResult =
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
    // Public API — Join
    // -----------------------------------------------------------------------

    /// Await the next available completion. Returns:
    ///   - Ok(RunCompletion) when a completion is available
    ///   - Error(NothingToJoin) when no agent or PTY is active
    ///   - Error(Cancelled) when the runtime has been cancelled
    member _.Join() : Task<Result<RunCompletion, ForkError>> = mailbox.Join()

    // -----------------------------------------------------------------------
    // Public API — PublishCompletion (external completions, e.g. PTY)
    // -----------------------------------------------------------------------

    /// Publish a completion that originated outside this runtime (e.g. PTY).
    /// Only accepted if the owning agent/PTY is known to this runtime.
    member _.PublishCompletion(completion: RunCompletion) : unit =
        let owned =
            lock lockObj (fun () ->
                let known =
                    ptys |> Map.containsKey completion.RunId
                    || agents |> Map.containsKey completion.AgentId

                if known then
                    match agents |> Map.tryFind completion.AgentId with
                    | Some run when not run.Completion.IsCompleted -> run.Completion.TrySet(completion) |> ignore
                    | _ -> ()

                known)

        if owned then
            mailbox.Publish completion

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

    member _.Restore(agentId: string, role: AgentRole, agent: string) : unit =
        let agentName = agent.Trim()

        lock lockObj (fun () ->
            if not (Map.containsKey agentId agents) then
                agents <- ForkRecovery.restore agentId agentName role agents)

    member _.MarkInterrupted(agentId: string, reason: string) : unit =
        lock lockObj (fun () -> agents <- ForkRecovery.markInterrupted agentId reason agents)

    member _.BindChildSession(agentId: string, childSessionId: SessionId) : unit =
        lock lockObj (fun () -> agents <- ForkRecovery.bindChildSession agentId childSessionId agents)

    /// Internal targeted completion handle. Model-visible join remains join-any.
    member _.AwaitAgent(agentId: string) : Task<Result<RunCompletion, string>> =
        let completion =
            lock lockObj (fun () -> agents |> Map.tryFind agentId |> Option.map (fun run -> run.Completion.Await))

        match completion with
        | None -> Task.FromResult(Error(sprintf "Unknown agent id: %s" agentId))
        | Some pending ->
            task {
                let! value = pending
                return Ok value
            }

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
