namespace Wanxiangshu.Next.Session

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Kernel
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
        ?onChildCreatedDir: string -> SessionId -> string option -> unit,
        ?ptyPort: PtyPort,
        ?directoryFor: string -> string option,
        ?onRunStarted: SessionId -> AgentRole -> string option -> unit,
        ?parentWorkRecordFor: SessionId -> string option,
        ?childWorkRecordFor: SessionId -> string option,
        ?sessionSnapshot: ISessionSnapshotPort
    ) as this =
    let runtime = ForkRuntime()
    let children = Dictionary<string, SessionId>()
    let pendingRuns = Dictionary<string, PendingHostRun>()
    let ptyRuns = HashSet<string>()
    let gate = obj ()
    let childCreated = defaultArg onChildCreated (fun _ _ _ -> ())
    let childCreatedDir = defaultArg onChildCreatedDir (fun _ _ _ -> ())
    let ptyPort = defaultArg ptyPort (PtyBackend.createPort ())
    let parentKey = SessionId.value parentId
    let directoryOf = defaultArg directoryFor (fun _ -> None)
    let runStarted = defaultArg onRunStarted (fun _ _ _ -> ())
    let parentWorkRecordOf = defaultArg parentWorkRecordFor (fun _ -> None)
    let childWorkRecordOf = defaultArg childWorkRecordFor (fun _ -> None)

    let sendChildPrompt =
        HostForkRunLifecycle.childPromptSender sessions parentId journal directoryOf

    let sendBusyNudge = HostForkBusyNudge.sender sessions parentId journal directoryOf

    let parentAbortToken =
        Pty.registerParentAbort parentKey (fun () -> this.Cancel() |> ignore)

    do
        ptyPort.AddMailboxSender(fun completion ->
            lock gate (fun () -> ptyRuns.Add completion.RunId |> ignore)
            runtime.UnregisterPty completion.RunId
            runtime.PublishCompletion completion)

    let completedTask () : Task = task { return () } :> Task
    let mutable recoveryTask: Task = completedTask ()

    let restoreChildren () =
        match journal with
        | None -> completedTask ()
        | Some j ->
            HostForkRestart.restoreLinkedChildren
                runtime
                sessionSnapshot
                j
                parentId
                children
                childCreatedDir
                directoryOf

    do recoveryTask <- restoreChildren ()

    let awaitRecovery () =
        task {
            try
                do! recoveryTask
            with _ ->
                ()
        }

    let complete run outcome =
        let workRecord =
            match outcome with
            | TerminalOutcome.Completed _ -> AgentCompletion.snapshotOption (childWorkRecordOf run.ChildId)
            | _ -> None

        HostForkRunLifecycle.complete gate pendingRuns sessions run outcome workRecord

    let installRun agentId childId role =
        let run =
            HostForkRunLifecycle.installRun gate pendingRuns sessions agentId childId role

        runtime.BindChildSession(agentId, SessionId.value childId)
        runStarted childId role (directoryOf agentId)
        run

    let failRun run error =
        HostForkRunLifecycle.failRun gate pendingRuns sessions run error

    let markReady run = HostForkRunLifecycle.markReady gate run

    member _.Fork(agentId: string, role: AgentRole, prompt: string, ?agent: string) : Task<Result<ForkResult, string>> =
        let agentName =
            match agent with
            | Some name when not (System.String.IsNullOrWhiteSpace name) -> name.Trim()
            | _ -> defaultFastManagedName role

        task {
            do! awaitRecovery ()

            let existing =
                lock gate (fun () ->
                    match children.TryGetValue agentId with
                    | true, childId -> Some childId
                    | false, _ -> None)

            match existing with
            | Some childId ->
                match HostPendingRun.sessionDeadRefusal journal childId with
                | Some refusal -> return Error refusal
                | None ->
                    return!
                        HostForkChildDispatch.sendToExistingChild
                            gate
                            pendingRuns
                            sessions
                            runtime
                            sendChildPrompt
                            sendBusyNudge
                            (fun child role -> runStarted child role (directoryOf agentId))
                            agentId
                            childId
                            role
                            prompt
                            agentName
            | None ->
                let! childResult =
                    sessions.CreateChildSession(
                        parentId,
                        { Title = Some agentId
                          Agent = Some agentName
                          Directory = directoryOf agentId }
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
                                       Role = Some agentName |}

                            match AgentJournal.appendAgent (StreamId.Session parentId) None fact journal with
                            | Ok _ -> Ok()
                            | Error failure -> Error(sprintf "Failed to persist AgentLinked: %A" failure.Failure)

                    match linkageResult with
                    | Error err ->
                        let! _ = sessions.AbortSession childId
                        return Error err
                    | Ok() ->
                        let run = installRun agentId childId role

                        lock gate (fun () -> children.[agentId] <- childId)

                        childCreated agentId role childId
                        childCreatedDir agentId childId (directoryOf agentId)

                        let result =
                            runtime.Fork(agentId, role, runWork = (fun () -> run.Source.Task), agent = agentName)

                        match result with
                        | ForkResult.NotFound _ ->
                            failRun run "Fork runtime is cancelled"
                            return Error "Fork runtime is cancelled"
                        | _ ->
                            markReady run

                            let enrichedPrompt =
                                match parentWorkRecordOf parentId with
                                | Some workRecord when not (System.String.IsNullOrWhiteSpace workRecord) ->
                                    sprintf
                                        "[Parent work record — background only; B preferred, else session A]
%s

[Assignment]
%s

[Required final report]
Result:
Files changed:
Tests run:
Evidence:
Remaining risks:
Blockers:"
                                        workRecord
                                        prompt
                                | _ -> prompt

                            let! sent =
                                HostForkAgentOwner.sendFirstPrompt
                                    sessions
                                    journal
                                    childId
                                    agentName
                                    (directoryOf agentId)
                                    enrichedPrompt

                            match sent with
                            | Ok _ -> return Ok result
                            | Error err ->
                                failRun run err
                                return Error err
        }

    member _.Reuse(agentId: string, prompt: string) : Task<Result<ForkResult, string>> =
        task {
            do! awaitRecovery ()

            let existing =
                lock gate (fun () ->
                    match children.TryGetValue agentId with
                    | true, childId -> Some childId
                    | false, _ -> None)

            match existing with
            | None -> return Error(sprintf "Unknown agent id: %s" agentId)
            | Some childId ->
                match HostPendingRun.sessionDeadRefusal journal childId with
                | Some refusal -> return Error refusal
                | None ->
                    let roleOpt =
                        runtime.List()
                        |> fst
                        |> List.tryFind (fun agent -> agent.AgentId = agentId)
                        |> Option.map (fun agent -> agent.Role)

                    match roleOpt with
                    | None -> return Error(sprintf "Unknown agent id: %s" agentId)
                    | Some role ->
                        let agentName =
                            runtime.List()
                            |> fst
                            |> List.tryFind (fun a -> a.AgentId = agentId)
                            |> Option.map (fun a -> a.Agent)
                            |> Option.defaultValue (defaultFastManagedName role)

                        return!
                            HostForkChildDispatch.sendToExistingChild
                                gate
                                pendingRuns
                                sessions
                                runtime
                                sendChildPrompt
                                sendBusyNudge
                                (fun child role -> runStarted child role (directoryOf agentId))
                                agentId
                                childId
                                role
                                prompt
                                agentName
        }

    member _.TryFindAgent(agentId: string) : AgentRecord option =
        runtime.List() |> fst |> List.tryFind (fun agent -> agent.AgentId = agentId)

    member internal _.TrackPtyRun(id: PtyId) =
        lock gate (fun () -> ptyRuns.Add id.Value |> ignore)

    member internal _.RegisterPtySnapshot (id: PtyId) (command: string) =
        runtime.RegisterPty
            { PtyId = id.Value
              AgentId = id.Value
              Command = command
              StartedAt = DateTimeOffset.UtcNow }

    member internal _.UntrackPtyRun(id: string) = runtime.UnregisterPty id

    member _.IsPtyCompletion(runId: string) =
        lock gate (fun () -> ptyRuns.Contains runId)

    member _.PtyPort = ptyPort

    member _.Join() : Task<Result<RunCompletion, ForkError>> =
        task {
            do! awaitRecovery ()
            return! runtime.Join()
        }

    member _.List() = runtime.List()
    member _.PendingRunCount = lock gate (fun () -> pendingRuns.Count)
    member _.PendingCompletionCount = runtime.PendingCompletionCount

    member this.Cancel() : Task<unit> =
        HostForkChildDispatch.cancelParent
            awaitRecovery
            runtime
            ptyPort
            parentKey
            parentAbortToken
            gate
            pendingRuns
            children
            sessions
            journal
            parentId
            complete
