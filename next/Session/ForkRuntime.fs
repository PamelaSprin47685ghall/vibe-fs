namespace Wanxiangshu.Next.Session

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Threading.Channels
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
type ForkError =
    | Busy
    | Empty
    | NotFound

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

type ForkRuntime() =
    let mailbox = Channel.CreateUnbounded<RunCompletion>()
    let agents = ConcurrentDictionary<string, AgentRecord>()
    let ptys = ConcurrentDictionary<string, PtyRecord>()
    let pendingCompletions = List<RunCompletion>()
    let lockObj = obj ()

    let drainMailbox () =
        let mutable item = Unchecked.defaultof<RunCompletion>

        while mailbox.Reader.TryRead(&item) do
            pendingCompletions.Add(item)

    member _.Fork
        (agentId: string, role: AgentRole, runWork: unit -> Task<Result<string, string>>)
        : Result<string, ForkError> =
        let runId = "run-" + Guid.NewGuid().ToString("N").Substring(0, 8)
        let mutable canStart = false
        let mutable err = None

        lock lockObj (fun () ->
            match agents.TryGetValue(agentId) with
            | true, rec' when rec'.Status = AgentStatus.Busy -> err <- Some ForkError.Busy
            | true, rec' ->
                agents.[agentId] <-
                    { rec' with
                        Status = AgentStatus.Busy
                        Role = role
                        CurrentRunId = Some runId }

                canStart <- true
            | false, _ ->
                agents.[agentId] <-
                    { AgentId = agentId
                      Role = role
                      Status = AgentStatus.Busy
                      CurrentRunId = Some runId }

                canStart <- true)

        match err with
        | Some e -> Error e
        | None ->
            Task.Run(fun () ->
                (task {
                    let! outcome =
                        task {
                            try
                                return! runWork ()
                            with ex ->
                                return Error ex.Message
                        }

                    let completion =
                        { RunId = runId
                          AgentId = agentId
                          Role = role
                          Outcome = outcome
                          CompletedAt = DateTimeOffset.UtcNow }

                    // Enqueue completion BEFORE updating status to Idle
                    mailbox.Writer.TryWrite(completion) |> ignore

                    lock lockObj (fun () ->
                        match agents.TryGetValue(agentId) with
                        | true, rec' ->
                            agents.[agentId] <-
                                { rec' with
                                    Status = AgentStatus.Idle
                                    CurrentRunId = None }
                        | false, _ -> ())
                }
                :> Task))
            |> ignore

            Ok runId

    member _.Join() : Result<RunCompletion, ForkError> =
        lock lockObj (fun () ->
            drainMailbox ()

            if pendingCompletions.Count > 0 then
                let item = pendingCompletions.[0]
                pendingCompletions.RemoveAt(0)
                Ok item
            else
                Error ForkError.Empty)

    member _.Join(agentId: string) : Result<RunCompletion, ForkError> =
        lock lockObj (fun () ->
            drainMailbox ()
            let idx = pendingCompletions.FindIndex(fun c -> c.AgentId = agentId)

            if idx >= 0 then
                let item = pendingCompletions.[idx]
                pendingCompletions.RemoveAt(idx)
                Ok item
            else if agents.ContainsKey(agentId) then
                Error ForkError.Empty
            else
                Error ForkError.NotFound)

    member _.RegisterPty(pty: PtyRecord) : unit = ptys.[pty.PtyId] <- pty

    member _.UnregisterPty(ptyId: string) : unit = ptys.TryRemove(ptyId) |> ignore

    member _.List() : AgentRecord list * PtyRecord list =
        lock lockObj (fun () ->
            let agentList = agents.Values |> Seq.toList
            let ptyList = ptys.Values |> Seq.toList
            (agentList, ptyList))
