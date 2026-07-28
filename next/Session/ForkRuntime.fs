namespace Wanxiangshu.Next.Session

open System
open System.Threading.Tasks

type ForkRuntime
    (
        ?runner: string -> AgentRole -> string option -> Task<AgentCompletionOutcome>,
        ?listener: RunCompletion -> unit,
        ?cleanup: string -> unit
    ) =

    let childRunner =
        defaultArg
            runner
            (fun agentId role prompt ->
                Task.FromResult(AgentCompletion.ofSimpleText agentId "run-local" role (defaultArg prompt "ok")))

    let terminalListener = defaultArg listener ignore
    let cleanupPort = defaultArg cleanup ignore

    let mailbox = System.Collections.Generic.Queue<RunCompletion>()

    let waiters =
        System.Collections.Generic.Queue<TaskCompletionSource<Result<RunCompletion, ForkError>>>()

    let agents = System.Collections.Generic.Dictionary<string, AgentRecord>()
    let ptys = System.Collections.Generic.Dictionary<string, PtyRecord>()
    let lockObj = obj ()
    let mutable isCancelled = false

    let startRun
        (agentId: string)
        (role: AgentRole)
        (promptOpt: string option)
        (workOpt: (unit -> Task<AgentCompletionOutcome>) option)
        =
        let runId = "run-" + Guid.NewGuid().ToString("N").Substring(0, 8)

        let onTerminal (completion: RunCompletion) =
            try
                terminalListener completion
            with _ ->
                ()

        let runTask () =
            task {
                let! outcome =
                    task {
                        try
                            match workOpt with
                            | Some w -> return! w ()
                            | None -> return! childRunner agentId role promptOpt
                        with ex ->
                            return AgentCompletion.ofSimpleError agentId runId role ex.Message
                    }

                let outcome = AgentCompletion.withRunIdentity agentId runId role outcome

                let completion =
                    { RunId = runId
                      AgentId = agentId
                      Role = role
                      Outcome = outcome
                      CompletedAt = DateTimeOffset.UtcNow }

                lock lockObj (fun () ->
                    if waiters.Count > 0 then
                        waiters.Dequeue().SetResult(Ok completion)
                    else
                        mailbox.Enqueue(completion)

                    let statusStr = Some(AgentCompletion.status completion.Outcome)

                    match agents.TryGetValue(agentId) with
                    | true, rec' when rec'.Status <> AgentStatus.Closed ->
                        agents.[agentId] <-
                            { rec' with
                                Status = AgentStatus.Idle
                                CurrentRunId = None
                                LastCompletionStatus = statusStr
                                HasPendingCompletion = true }
                    | _ -> ())

                onTerminal completion
            }

        let launch () = runTask () |> ignore
        runId, launch

    member this.Fork
        (agentId: string, role: AgentRole, ?prompt: string, ?runWork: unit -> Task<AgentCompletionOutcome>)
        : ForkResult =
        lock lockObj (fun () ->
            if isCancelled then
                ForkResult.NotFound agentId
            else
                match agents.TryGetValue agentId with
                | true, rec' when rec'.Status = AgentStatus.Busy ->
                    ForkResult.Nudged agentId
                | true, rec' ->
                    let runId, launch = startRun agentId role prompt runWork

                    agents.[agentId] <-
                        { rec' with
                            Role = role
                            Status = AgentStatus.Busy
                            CurrentRunId = Some runId
                            HasPendingCompletion = false }

                    launch ()
                    ForkResult.Nudged agentId
                | false, _ ->
                    let runId, launch = startRun agentId role prompt runWork

                    agents.[agentId] <-
                        { AgentId = agentId
                          Role = role
                          Status = AgentStatus.Busy
                          CurrentRunId = Some runId
                          LastCompletionStatus = None
                          HasPendingCompletion = false
                          ChildSessionId = None }

                    launch ()
                    ForkResult.Created agentId)

    member _.Join() : Task<Result<RunCompletion, ForkError>> =
        lock lockObj (fun () ->
            if mailbox.Count > 0 then
                Task.FromResult(Ok(mailbox.Dequeue()))
            elif isCancelled then
                Task.FromResult(Error ForkError.Cancelled)
            else
                let hasBusy =
                    agents.Values |> Seq.exists (fun record' -> record'.Status = AgentStatus.Busy)
                    || ptys.Count > 0

                if not hasBusy then
                    Task.FromResult(Error ForkError.NothingToJoin)
                else
                    let waiter = TaskCompletionSource<Result<RunCompletion, ForkError>>()
                    waiters.Enqueue(waiter)
                    waiter.Task)

    member _.PublishCompletion(completion: RunCompletion) : unit =
        lock lockObj (fun () ->
            if waiters.Count > 0 then
                waiters.Dequeue().SetResult(Ok completion)
            else
                mailbox.Enqueue completion

            match agents.TryGetValue completion.AgentId with
            | true, rec' when rec'.Status <> AgentStatus.Closed ->
                agents.[completion.AgentId] <-
                    { rec' with
                        Status = AgentStatus.Idle
                        CurrentRunId = None
                        LastCompletionStatus = Some(AgentCompletion.status completion.Outcome)
                        HasPendingCompletion = waiters.Count = 0 && mailbox.Count > 0 }
            | _ -> ())

    member _.RegisterPty(pty: PtyRecord) : unit =
        lock lockObj (fun () -> ptys.[pty.PtyId] <- pty)

    member _.Restore(agentId: string, role: AgentRole) : unit =
        lock lockObj (fun () ->
            if not (agents.ContainsKey agentId) then
                agents.[agentId] <-
                    { AgentId = agentId
                      Role = role
                      Status = AgentStatus.Idle
                      CurrentRunId = None
                      LastCompletionStatus = None
                      HasPendingCompletion = false
                      ChildSessionId = None })

    member _.MarkInterrupted(agentId: string, reason: string) : unit =
        lock lockObj (fun () ->
            match agents.TryGetValue agentId with
            | true, rec' ->
                agents.[agentId] <-
                    { rec' with
                        Status = AgentStatus.Interrupted
                        CurrentRunId = None
                        LastCompletionStatus = Some ("interrupted:" + reason)
                        HasPendingCompletion = false }
            | false, _ -> ())

    member _.BindChildSession(agentId: string, childSessionId: string) : unit =
        lock lockObj (fun () ->
            match agents.TryGetValue agentId with
            | true, rec' ->
                agents.[agentId] <-
                    { rec' with
                        ChildSessionId = Some childSessionId }
            | false, _ -> ())

    member _.UnregisterPty(ptyId: string) : unit =
        lock lockObj (fun () -> ptys.Remove(ptyId) |> ignore)

    member _.List() : AgentRecord list * PtyRecord list =
        lock lockObj (fun () ->
            let agentList = agents.Values |> Seq.toList
            let ptyList = ptys.Values |> Seq.toList
            (agentList, ptyList))

    member _.IsCancelled = lock lockObj (fun () -> isCancelled)

    member _.ActiveRunCount =
        lock lockObj (fun () ->
            agents.Values
            |> Seq.filter (fun record' -> record'.Status = AgentStatus.Busy)
            |> Seq.length)

    member _.PendingCompletionCount = lock lockObj (fun () -> mailbox.Count)

    member _.Cancel() : unit =
        let toClean, pendingWaiters =
            lock lockObj (fun () ->
                if isCancelled then
                    [], []
                else
                    isCancelled <- true
                    let agentIds = agents.Keys |> Seq.toList

                    for id in agentIds do
                        match agents.TryGetValue(id) with
                        | true, rec' ->
                            agents.[id] <-
                                { rec' with
                                    Status = AgentStatus.Closed
                                    CurrentRunId = None
                                    HasPendingCompletion = false }
                        | _ -> ()

                    let ptyIds = ptys.Keys |> Seq.toList
                    ptys.Clear()

                    let drainedWaiters =
                        [ while waiters.Count > 0 do
                              yield waiters.Dequeue() ]

                    agentIds @ ptyIds, drainedWaiters)

        for waiter in pendingWaiters do
            try
                waiter.SetResult(Error ForkError.Cancelled)
            with _ ->
                ()

        for id in toClean do
            try
                cleanupPort id
            with _ ->
                ()

    member this.Close() : unit = this.Cancel()
