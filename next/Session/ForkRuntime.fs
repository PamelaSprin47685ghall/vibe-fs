namespace Wanxiangshu.Next.Session

open System
open System.Threading.Tasks

[<RequireQualifiedAccess>]
type AgentRole =
    | Manager
    | Coder
    | Inspector
    | Browser
    | Meditator
    | Reviewer
    | Advisor
    | Executor

[<RequireQualifiedAccess>]
type AgentStatus =
    | Idle
    | Busy
    | Closed

[<RequireQualifiedAccess>]
type ForkResult =
    | Created of agentId: string
    | Nudged of agentId: string
    | NotFound of agentId: string

type ForkResult with
    member this.AgentId =
        match this with
        | ForkResult.Created id
        | ForkResult.Nudged id
        | ForkResult.NotFound id -> id

[<RequireQualifiedAccess>]
type ForkError =
    | Empty
    | NotFound of agentId: string

type RunCompletion =
    { RunId: string
      AgentId: string
      Role: AgentRole
      Outcome: Result<string, string>
      CompletedAt: DateTimeOffset }

type PtyRecord =
    { PtyId: string
      AgentId: string
      Command: string
      StartedAt: DateTimeOffset }

type AgentRecord =
    { AgentId: string
      Role: AgentRole
      Status: AgentStatus
      CurrentRunId: string option }

type ForkRuntime
    (
        ?runner: string -> AgentRole -> string option -> Task<Result<string, string>>,
        ?listener: RunCompletion -> unit,
        ?cleanup: string -> unit
    ) =

    let childRunner =
        defaultArg runner (fun _ _ prompt -> Task.FromResult(Ok(defaultArg prompt "ok")))

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
        (workOpt: (unit -> Task<Result<string, string>>) option)
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
                            return Error ex.Message
                    }

                let completion =
                    { RunId = runId
                      AgentId = agentId
                      Role = role
                      Outcome = outcome
                      CompletedAt = DateTimeOffset.UtcNow }


                // 1. Invoke terminal listener (registered before firing prompt)
                onTerminal completion

                // 2. Deliver to an existing join before changing live status.
                lock lockObj (fun () ->
                    if waiters.Count > 0 then
                        waiters.Dequeue().SetResult(Ok completion)
                    else
                        mailbox.Enqueue(completion))

                // 3. Update status in live agent handle map
                lock lockObj (fun () ->
                    match agents.TryGetValue(agentId) with
                    | true, rec' when rec'.Status <> AgentStatus.Closed ->
                        agents.[agentId] <-
                            { rec' with
                                Status = AgentStatus.Idle
                                CurrentRunId = None }
                    | _ -> ())
            }

        let _ = runTask ()
        runId

    member this.Fork
        (agentId: string, role: AgentRole, ?prompt: string, ?runWork: unit -> Task<Result<string, string>>)
        : ForkResult =
        lock lockObj (fun () ->
            match agents.TryGetValue(agentId) with
            | true, rec' ->
                let runId = startRun agentId role prompt runWork

                agents.[agentId] <-
                    { rec' with
                        Role = role
                        Status = AgentStatus.Busy
                        CurrentRunId = Some runId }

                ForkResult.Nudged agentId
            | false, _ ->
                let runId = startRun agentId role prompt runWork

                agents.[agentId] <-
                    { AgentId = agentId
                      Role = role
                      Status = AgentStatus.Busy
                      CurrentRunId = Some runId }

                ForkResult.Created agentId)

    member this.Fork(agentId: string, prompt: string) : ForkResult =
        lock lockObj (fun () ->
            match agents.TryGetValue(agentId) with
            | true, rec' ->
                let runId = startRun agentId rec'.Role (Some prompt) None

                agents.[agentId] <-
                    { rec' with
                        Status = AgentStatus.Busy
                        CurrentRunId = Some runId }

                ForkResult.Nudged agentId
            | false, _ -> ForkResult.NotFound agentId)


    member _.Join() : Task<Result<RunCompletion, ForkError>> =
        lock lockObj (fun () ->
            if mailbox.Count > 0 then
                Task.FromResult(Ok(mailbox.Dequeue()))
            else
                let waiter = TaskCompletionSource<Result<RunCompletion, ForkError>>()
                waiters.Enqueue(waiter)
                waiter.Task)

    member _.RegisterPty(pty: PtyRecord) : unit =
        lock lockObj (fun () -> ptys.[pty.PtyId] <- pty)

    member _.UnregisterPty(ptyId: string) : unit =
        lock lockObj (fun () -> ptys.Remove(ptyId) |> ignore)

    member _.List() : AgentRecord list * PtyRecord list =
        lock lockObj (fun () ->
            let agentList = agents.Values |> Seq.toList
            let ptyList = ptys.Values |> Seq.toList
            (agentList, ptyList))

    member _.Cancel() : unit =
        let toClean =
            lock lockObj (fun () ->
                if isCancelled then
                    []
                else
                    isCancelled <- true
                    let agentIds = agents.Keys |> Seq.toList

                    for id in agentIds do
                        match agents.TryGetValue(id) with
                        | true, rec' ->
                            agents.[id] <-
                                { rec' with
                                    Status = AgentStatus.Closed
                                    CurrentRunId = None }
                        | _ -> ()

                    let ptyIds = ptys.Keys |> Seq.toList
                    ptys.Clear()
                    agentIds @ ptyIds)

        for id in toClean do
            try
                cleanupPort id
            with _ ->
                ()

    member this.Close() : unit = this.Cancel()
