namespace Wanxiangshu.Next.Session

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal

/// Bridges real child sessions to the existing completion mailbox.
type HostForkRuntime(parentId: SessionId, sessions: ISessionHostPort, ?journal: AgentJournal) =
    let runtime = ForkRuntime()
    let children = Dictionary<string, SessionId>()
    let subscriptions = Dictionary<string, IDisposable>()
    let gate = obj ()

    let completionSource () : TaskCompletionSource<Result<string, string>> =
        TaskCompletionSource<Result<string, string>>(TaskCreationOptions.RunContinuationsAsynchronously)

    let finish
        (agentId: string)
        (childId: SessionId)
        (source: TaskCompletionSource<Result<string, string>>)
        (outcome: TerminalOutcome)
        =
        let output () =
            sessions.GetSessionOutput childId |> String.concat "\n"

        match outcome with
        | Completed _ -> source.SetResult(Ok(output ()))
        | Aborted reason -> source.SetResult(Error reason)
        | Failed error -> source.SetResult(Error error)

        lock gate (fun () ->
            match subscriptions.TryGetValue agentId with
            | true, subscription ->
                subscriptions.Remove agentId |> ignore
                subscription.Dispose()
            | false, _ -> ())

    member _.Fork(agentId: string, role: AgentRole, prompt: string) : Task<Result<ForkResult, string>> =
        task {
            let existing =
                lock gate (fun () ->
                    match children.TryGetValue agentId with
                    | true, childId -> Some childId
                    | false, _ -> None)

            match existing with
            | Some childId ->
                let source = completionSource ()
                let result = runtime.Fork(agentId, role, runWork = (fun () -> source.Task))

                match result with
                | ForkResult.Nudged _ ->
                    let! sent =
                        sessions.SendChildPromptFireAndForget(parentId, childId, prompt, { Model = None; Agent = None })

                    match sent with
                    | Ok() -> return Ok result
                    | Error err ->
                        source.SetResult(Error err)
                        return Error err
                | _ -> return Ok result
            | None ->
                let! childResult =
                    sessions.CreateChildSession(
                        parentId,
                        { Title = Some agentId
                          Agent = Some(role.ToString()) }
                    )

                match childResult with
                | Error err -> return Error err
                | Ok childId ->
                    let source = completionSource ()

                    let subscription =
                        sessions.SubscribeTerminal(childId, (fun _ outcome -> finish agentId childId source outcome))

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
                        subscription.Dispose()
                        let! _ = sessions.AbortSession childId
                        return Error err
                    | Ok() ->
                        lock gate (fun () ->
                            children.[agentId] <- childId
                            subscriptions.[agentId] <- subscription)

                        let result = runtime.Fork(agentId, role, runWork = (fun () -> source.Task))
                        let! sent = sessions.SendPrompt(childId, prompt, { Model = None; Agent = None })

                        match sent with
                        | Ok _ -> return Ok result
                        | Error err ->
                            source.SetResult(Error err)
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
                let source = completionSource ()

                let roleOpt =
                    runtime.List()
                    |> fst
                    |> List.tryFind (fun agent -> agent.AgentId = agentId)
                    |> Option.map (fun agent -> agent.Role)

                let result =
                    match roleOpt with
                    | Some role -> runtime.Fork(agentId, role, runWork = (fun () -> source.Task))
                    | None -> ForkResult.NotFound agentId

                match result with
                | ForkResult.Nudged _ ->
                    let! sent =
                        sessions.SendChildPromptFireAndForget(parentId, childId, prompt, { Model = None; Agent = None })

                    match sent with
                    | Ok() -> return Ok result
                    | Error err ->
                        source.SetResult(Error err)
                        return Error err
                | ForkResult.NotFound _ -> return Error(sprintf "Unknown agent id: %s" agentId)
                | ForkResult.Created _ -> return Error(sprintf "Unknown agent id: %s" agentId)
        }

    member _.Join() : Task<Result<RunCompletion, ForkError>> = runtime.Join()

    member _.List() = runtime.List()

    member _.Cancel() =
        runtime.Cancel()
        sessions.AbortSession(parentId) |> ignore
