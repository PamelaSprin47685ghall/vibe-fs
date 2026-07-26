namespace Wanxiangshu.Next.Session

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session.AgentRoleHelpers

/// Bridges real child sessions to the existing completion mailbox.
type HostForkRuntime
    (
        parentId: SessionId,
        sessions: ISessionHostPort,
        ?journal: AgentJournal,
        ?onChildCreated: string -> AgentRole -> SessionId -> unit,
        ?modelResolver: ModelResolver.ModelConfig,
        ?ptyPort: PtyPort
    ) as this =
    let runtime = ForkRuntime()
    let children = Dictionary<string, SessionId>()
    let pendingRuns = Dictionary<string, PendingHostRun>()
    let ptyRuns = HashSet<string>()
    let gate = obj ()
    let childCreated = defaultArg onChildCreated (fun _ _ _ -> ())
    let ptyPort = defaultArg ptyPort (PtyPort())
    let parentKey = SessionId.value parentId
    let parentAbortToken = Pty.registerParentAbort parentKey (fun () -> this.Cancel())

    do
        ptyPort.AddMailboxSender(fun completion ->
            lock gate (fun () -> ptyRuns.Add completion.RunId |> ignore)
            runtime.UnregisterPty completion.RunId
            runtime.PublishCompletion completion)

    let restoreChildren () =
        match journal with
        | None -> ()
        | Some journal ->
            let snapshot = AgentJournal.snapshot journal

            match Map.tryFind parentId snapshot.AgentProjections.Sessions with
            | Some session when session.Linkage.IsSome ->
                let linkage = session.Linkage.Value

                for KeyValue(childId, agentId) in linkage.LinkedChildren do
                    let role = linkage.LinkedRoles |> Map.tryFind childId |> Option.bind roleOfString

                    match role with
                    | Some role ->
                        let childSessionId = SessionId.create (ChildId.value childId)
                        children.[agentId] <- childSessionId
                        runtime.Restore(agentId, role)
                    | None -> ()
            | _ -> ()

    do restoreChildren ()

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
              Source = HostPendingRun.completionSource ()
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
                let activeRun =
                    lock gate (fun () ->
                        match pendingRuns.TryGetValue agentId with
                        | true, run -> Some run
                        | false, _ -> None)

                match activeRun with
                | Some _ ->
                    let! sent =
                        sessions.SendChildPromptFireAndForget(
                            parentId,
                            childId,
                            prompt,
                            { Model = HostPendingRun.resolveModel modelResolver journal childId
                              Agent = Some(role.ToString().ToLowerInvariant()) }
                        )

                    match sent with
                    | Ok() -> return Ok(ForkResult.Nudged agentId)
                    | Error err -> return Error err
                | None ->
                    let run = installRun agentId childId
                    let result = runtime.Fork(agentId, role, runWork = (fun () -> run.Source.Task))
                    markReady run

                    let! sent =
                        sessions.SendChildPromptFireAndForget(
                            parentId,
                            childId,
                            prompt,
                            { Model = HostPendingRun.resolveModel modelResolver journal childId
                              Agent = Some(role.ToString().ToLowerInvariant()) }
                        )

                    match sent, result with
                    | Ok(), ForkResult.Nudged _ -> return Ok result
                    | Ok(), _ ->
                        failRun run "Existing agent did not accept a new run"
                        return Error "Existing agent did not accept a new run"
                    | Error err, _ ->
                        failRun run err
                        return Error err
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
                                       TargetAgent = agentId
                                       Role = Some(role.ToString()) |}

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
                        childCreated agentId role childId
                        let result = runtime.Fork(agentId, role, runWork = (fun () -> run.Source.Task))
                        markReady run

                        let! sent =
                            sessions.SendPrompt(
                                childId,
                                prompt,
                                { Model = HostPendingRun.resolveModel modelResolver journal childId
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

                match roleOpt with
                | None -> return Error(sprintf "Unknown agent id: %s" agentId)
                | Some role ->
                    let activeRun =
                        lock gate (fun () ->
                            match pendingRuns.TryGetValue agentId with
                            | true, run -> Some run
                            | false, _ -> None)

                    match activeRun with
                    | Some _ ->
                        // The prompt must carry the role: after a host restart
                        // OpenCode resolves an agent-less child prompt to the
                        // default build agent, not the session's original role.
                        let! sent =
                            sessions.SendChildPromptFireAndForget(
                                parentId,
                                childId,
                                prompt,
                                { Model = HostPendingRun.resolveModel modelResolver journal childId
                                  Agent = Some(role.ToString().ToLowerInvariant()) }
                            )

                        match sent with
                        | Ok() -> return Ok(ForkResult.Nudged agentId)
                        | Error err -> return Error err
                    | None ->
                        let run = installRun agentId childId
                        let result = runtime.Fork(agentId, role, runWork = (fun () -> run.Source.Task))
                        markReady run

                        let! sent =
                            sessions.SendChildPromptFireAndForget(
                                parentId,
                                childId,
                                prompt,
                                { Model = HostPendingRun.resolveModel modelResolver journal childId
                                  Agent = Some(role.ToString().ToLowerInvariant()) }
                            )

                        match sent, result with
                        | Ok(), ForkResult.Nudged _ -> return Ok result
                        | Ok(), _ ->
                            failRun run "Existing agent did not accept a new run"
                            return Error "Existing agent did not accept a new run"
                        | Error err, _ ->
                            failRun run err
                            return Error err
        }

    member _.ForkPty(command: string) : Task<Result<PtyId, string>> =
        task {
            if String.IsNullOrWhiteSpace command then
                return Error "PTY command is required"
            else
                try
                    let id = Pty.newId ()
                    lock gate (fun () -> ptyRuns.Add id.Value |> ignore)
                    ptyPort.Fork(command, ptyId = id) |> ignore

                    if ptyPort.Exists id then
                        runtime.RegisterPty
                            { PtyId = id.Value
                              AgentId = id.Value
                              Command = command
                              StartedAt = DateTimeOffset.UtcNow }

                    return Ok id
                with ex ->
                    return Error ex.Message
        }

    member _.TryPty(id: string) =
        let candidate = PtyId.Create id
        if ptyPort.Exists candidate then Some candidate else None

    member _.SendPty(id: PtyId, prompt: string, signal: PtySignal option) : Task<Result<PtyId, string>> =
        task {
            if not (ptyPort.Exists id) then
                return Error(sprintf "Unknown PTY id: %s" id.Value)
            else
                match signal with
                | Some value -> ptyPort.Send(id, PtyCommand.Signal value)
                | None when String.IsNullOrEmpty prompt -> ptyPort.Send(id, PtyCommand.Read)
                | None -> ptyPort.Send(id, PtyCommand.Write(Pty.bytes prompt))

                return Ok id
        }

    member _.IsPtyCompletion(runId: string) =
        lock gate (fun () -> ptyRuns.Contains runId)

    member _.PtyPort = ptyPort
    member _.Join() : Task<Result<RunCompletion, ForkError>> = runtime.Join()
    member _.List() = runtime.List()
    member _.PendingRunCount = lock gate (fun () -> pendingRuns.Count)
    member _.PendingCompletionCount = runtime.PendingCompletionCount

    member _.Cancel() =
        ptyPort.CloseAll()
        runtime.Cancel()
        Pty.unregisterParentAbort parentKey parentAbortToken
        let childIds = lock gate (fun () -> children.Values |> Seq.distinct |> Seq.toList)
        childIds |> List.iter (fun childId -> sessions.AbortSession childId |> ignore)
