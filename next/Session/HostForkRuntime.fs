namespace Wanxiangshu.Next.Session

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal

type private PendingHostRun =
    { Token: obj
      AgentId: string
      ChildId: SessionId
      Source: TaskCompletionSource<Result<string, string>>
      OutputWatermark: int option
      FallbackOutputCount: int
      mutable Subscription: IDisposable option
      mutable Ready: bool
      mutable Finished: bool }

/// Bridges real child sessions to the existing completion mailbox.
type HostForkRuntime(parentId: SessionId, sessions: ISessionHostPort, ?journal: AgentJournal) =
    let runtime = ForkRuntime()
    let children = Dictionary<string, SessionId>()
    let pendingRuns = Dictionary<string, PendingHostRun>()
    let gate = obj ()

    let completionSource () : TaskCompletionSource<Result<string, string>> =
        TaskCompletionSource<Result<string, string>>(TaskCreationOptions.RunContinuationsAsynchronously)

    let outputSince (run: PendingHostRun) =
        let all = sessions.GetSessionOutput run.ChildId
        let start = max 0 (min run.FallbackOutputCount all.Length)
        let output = all |> List.skip start

        output
        |> List.filter (fun line -> not (line.StartsWith("Prompt: ")) && not (line.StartsWith("ChildPrompt: ")))
        |> String.concat "\n"

    let complete (run: PendingHostRun) (outcome: TerminalOutcome) =
        let subscriptionToDispose =
            lock gate (fun () ->
                match pendingRuns.TryGetValue run.AgentId with
                | true, current when obj.ReferenceEquals(current.Token, run.Token) && run.Ready && not run.Finished ->
                    run.Finished <- true
                    pendingRuns.Remove run.AgentId |> ignore
                    run.Subscription
                | _ -> None)

        subscriptionToDispose
        |> Option.iter (fun subscription -> subscription.Dispose())

        match outcome with
        | Completed _ -> run.Source.SetResult(Ok(outputSince run))
        | Aborted reason -> run.Source.SetResult(Error reason)
        | Failed error -> run.Source.SetResult(Error error)

    let installRun (agentId: string) (childId: SessionId) =
        let run =
            { Token = obj ()
              AgentId = agentId
              ChildId = childId
              Source = completionSource ()
              OutputWatermark = None
              FallbackOutputCount = sessions.GetSessionOutput childId |> List.length
              Subscription = None
              Ready = false
              Finished = false }

        lock gate (fun () -> pendingRuns.[agentId] <- run)

        let subscription =
            sessions.SubscribeTerminal(childId, (fun _ outcome -> complete run outcome))

        let disposeImmediately =
            lock gate (fun () ->
                run.Subscription <- Some subscription
                run.Finished)

        if disposeImmediately then
            subscription.Dispose()

        run

    let failRun (run: PendingHostRun) (error: string) =
        lock gate (fun () -> run.Ready <- true)
        complete run (Failed error)

    let markReady (run: PendingHostRun) = lock gate (fun () -> run.Ready <- true)

    member _.Fork(agentId: string, role: AgentRole, prompt: string) : Task<Result<ForkResult, string>> =
        task {
            let existing =
                lock gate (fun () ->
                    match children.TryGetValue agentId with
                    | true, childId -> Some childId
                    | false, _ -> None)

            match existing with
            | Some childId ->
                let run = installRun agentId childId
                let result = runtime.Fork(agentId, role, runWork = (fun () -> run.Source.Task))

                match result with
                | ForkResult.Nudged _ ->
                    markReady run

                    let! sent =
                        sessions.SendChildPromptFireAndForget(parentId, childId, prompt, { Model = None; Agent = None })

                    match sent with
                    | Ok() -> return Ok result
                    | Error err ->
                        failRun run err
                        return Error err
                | _ ->
                    failRun run (sprintf "Unknown agent id: %s" agentId)
                    return Error(sprintf "Unknown agent id: %s" agentId)
            | None ->
                let! childResult =
                    sessions.CreateChildSession(
                        parentId,
                        { Title = Some agentId
                          Agent = Some(role.ToString().ToLowerInvariant()) }
                    )

                match childResult with
                | Error err -> return Error err
                | Ok childId ->
                    let linkageResult =
                        match journal with
                        | None -> Ok()
                        | Some journal ->
                            let fact =
                                AgentFact.AgentLinked
                                    {| ParentId = parentId
                                       ChildId = ChildId.create (SessionId.value childId)
                                       TargetAgent = agentId |}

                            match AgentJournal.appendAgent (StreamId.Session parentId) None fact journal with
                            | Ok _ -> Ok()
                            | Error failure -> Error(sprintf "Failed to persist AgentLinked: %A" failure.Failure)

                    match linkageResult with
                    | Error err ->
                        let! _ = sessions.AbortSession childId
                        return Error err
                    | Ok() ->
                        let run = installRun agentId childId

                        lock gate (fun () -> children.[agentId] <- childId)

                        let result = runtime.Fork(agentId, role, runWork = (fun () -> run.Source.Task))
                        markReady run

                        let! sent =
                            sessions.SendPrompt(
                                childId,
                                prompt,
                                { Model = None
                                  Agent = Some(role.ToString().ToLowerInvariant()) }
                            )

                        match sent with
                        | Ok _ -> return Ok result
                        | Error err ->
                            failRun run err
                            return Error err
        }

    member _.Reuse(agentId: string, prompt: string) : Task<Result<ForkResult, string>> =
        task {
            let existing =
                lock gate (fun () ->
                    match children.TryGetValue agentId with
                    | true, childId -> Some childId
                    | false, _ -> None)

            match existing with
            | None -> return Error(sprintf "Unknown agent id: %s" agentId)
            | Some childId ->
                let roleOpt =
                    runtime.List()
                    |> fst
                    |> List.tryFind (fun agent -> agent.AgentId = agentId)
                    |> Option.map (fun agent -> agent.Role)

                let result =
                    match roleOpt with
                    | Some role ->
                        let run = installRun agentId childId
                        let result = runtime.Fork(agentId, role, runWork = (fun () -> run.Source.Task))
                        Some(run, result)
                    | None -> None

                match result with
                | Some(run, ForkResult.Nudged _) ->
                    markReady run

                    let! sent =
                        sessions.SendChildPromptFireAndForget(parentId, childId, prompt, { Model = None; Agent = None })

                    match sent with
                    | Ok() -> return Ok(ForkResult.Nudged agentId)
                    | Error err ->
                        failRun run err
                        return Error err
                | Some(run, result) ->
                    failRun run (sprintf "Unknown agent id: %s" agentId)
                    return Error(sprintf "Unknown agent id: %s" agentId)
                | None -> return Error(sprintf "Unknown agent id: %s" agentId)
        }

    member _.Join() : Task<Result<RunCompletion, ForkError>> = runtime.Join()

    member _.List() = runtime.List()

    member _.Cancel() =
        runtime.Cancel()

        let childIds = lock gate (fun () -> children.Values |> Seq.distinct |> Seq.toList)

        childIds |> List.iter (fun childId -> sessions.AbortSession childId |> ignore)
