namespace Wanxiangshu.Next.Session

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal

/// Per-run terminal lifecycle for HostForkRuntime: install, complete, fail.
module HostForkRunLifecycle =

    let outputSince (sessions: ISessionHostPort) (run: PendingHostRun) =
        let all = sessions.GetSessionOutput run.ChildId
        let start = max 0 (min run.FallbackOutputCount all.Length)
        let output = all |> List.skip start

        output
        |> List.filter (fun line -> not (line.StartsWith("Prompt: ")) && not (line.StartsWith("ChildPrompt: ")))
        |> String.concat "\n"

    let sendChildPrompt
        (sessions: ISessionHostPort)
        (parentId: SessionId)
        (childId: SessionId)
        (role: AgentRole)
        (model: OpencodeModel option)
        (directory: string option)
        (prompt: string)
        =
        sessions.SendChildPromptFireAndForget(
            parentId,
            childId,
            prompt,
            { Model = model
              Agent = Some(role.ToString().ToLowerInvariant())
              Directory = directory
              Metadata = None }
        )

    let childPromptSender sessions parentId modelResolver journal directoryOf =
        fun agentId childId role prompt ->
            sendChildPrompt
                sessions
                parentId
                childId
                role
                (HostPendingRun.resolveModel modelResolver journal childId)
                (directoryOf agentId)
                prompt

    let complete
        (gate: obj)
        (pendingRuns: Dictionary<string, PendingHostRun>)
        (sessions: ISessionHostPort)
        (run: PendingHostRun)
        (outcome: TerminalOutcome)
        =
        // Only the first matching terminal may claim the run. Duplicate idle/
        // abort from dual event streams must not SetResult twice.
        let claimed, subscriptionToDispose =
            lock gate (fun () ->
                match pendingRuns.TryGetValue run.AgentId with
                | true, current when obj.ReferenceEquals(current.Token, run.Token) && run.Ready && not run.Finished ->
                    run.Finished <- true
                    pendingRuns.Remove run.AgentId |> ignore
                    true, run.Subscription
                | _ -> false, None)

        if claimed then
            subscriptionToDispose
            |> Option.iter (fun subscription -> subscription.Dispose())

            match outcome with
            | Completed result ->
                // PR2: TerminalOutcome.Completed now carries AgentRunResult.
                // Extract FinalText for backward-compatible Source channel.
                let text = result.FinalText

                if String.IsNullOrWhiteSpace text then
                    run.Source.SetResult(Error "completed with empty final text (PR2 invariant)")
                else
                    run.Source.SetResult(Ok text)
            | Aborted reason -> run.Source.SetResult(Error reason)
            | Failed error -> run.Source.SetResult(Error error)

    let installRun
        (gate: obj)
        (pendingRuns: Dictionary<string, PendingHostRun>)
        (sessions: ISessionHostPort)
        (agentId: string)
        (childId: SessionId)
        =
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
            sessions.SubscribeTerminal(childId, (fun _ outcome -> complete gate pendingRuns sessions run outcome))

        let disposeImmediately =
            lock gate (fun () ->
                run.Subscription <- Some subscription
                run.Finished)

        if disposeImmediately then
            subscription.Dispose()

        run

    let failRun
        (gate: obj)
        (pendingRuns: Dictionary<string, PendingHostRun>)
        (sessions: ISessionHostPort)
        (run: PendingHostRun)
        (error: string)
        =
        lock gate (fun () -> run.Ready <- true)
        complete gate pendingRuns sessions run (Failed error)

    let markReady (gate: obj) (run: PendingHostRun) = lock gate (fun () -> run.Ready <- true)

    /// Sends a prompt to an already-linked child: if a run is active for this
    /// agent, nudge (fire-and-forget send, carrying role explicitly — after a
    /// host restart OpenCode would otherwise resolve an agent-less child prompt
    /// to the default build agent, not the session's original role); otherwise
    /// install a fresh run and fork it. Shared by HostForkRuntime.Fork's
    /// existing-child path and Reuse, which differ only in how they obtain
    /// `role` before reaching this point.
    let sendToExistingChild
        (gate: obj)
        (pendingRuns: Dictionary<string, PendingHostRun>)
        (sessions: ISessionHostPort)
        (runtime: ForkRuntime)
        (sendChildPrompt: string -> SessionId -> AgentRole -> string -> Task<Result<unit, string>>)
        (sendBusyNudge: string -> SessionId -> AgentRole -> string -> Task<Result<unit, string>>)
        (onRunStarted: SessionId -> AgentRole -> unit)
        (agentId: string)
        (childId: SessionId)
        (role: AgentRole)
        (prompt: string)
        : Task<Result<ForkResult, string>> =
        task {
            let activeRun =
                lock gate (fun () ->
                    match pendingRuns.TryGetValue agentId with
                    | true, run -> Some run
                    | false, _ -> None)

            match activeRun with
            | Some _ when runtime.IsCancelled -> return Error "Fork runtime is cancelled"
            | Some _ ->
                // Active run: BusyAgentNudge continuation (same LogicalRun).
                let! sent = sendBusyNudge agentId childId role prompt

                match sent with
                | Ok() -> return Ok(ForkResult.Nudged agentId)
                | Error err -> return Error err
            | None ->
                // Idle existing child: new AgentOwnerRoot work via ordinary send.
                let run = installRun gate pendingRuns sessions agentId childId
                onRunStarted childId role
                let result = runtime.Fork(agentId, role, runWork = (fun () -> run.Source.Task))

                match result with
                | ForkResult.NotFound _ ->
                    failRun gate pendingRuns sessions run "Fork runtime is cancelled"
                    return Error "Fork runtime is cancelled"
                | _ ->
                    markReady gate run
                    let! sent = sendChildPrompt agentId childId role prompt

                    match sent, result with
                    | Ok(), ForkResult.Nudged _ -> return Ok result
                    | Ok(), _ ->
                        failRun gate pendingRuns sessions run "Existing agent did not accept a new run"
                        return Error "Existing agent did not accept a new run"
                    | Error err, _ ->
                        failRun gate pendingRuns sessions run err
                        return Error err
        }

    /// Persist AgentUnlinked facts for each distinct child BEFORE aborting, so a
    /// crash mid-Cancel cannot leave a session aborted but still linked (which
    /// would make a restart restore a dead child). A leaked abort is recoverable;
    /// a leaked link is not.
    ///
    /// Timing adjudication: unlink is driven ONLY by the parent's Cancel (the sole
    /// teardown path — HostForkRuntime has no other Dispose hook). There is no
    /// child-normal-close host event (host docs confirm no durable child-close event), so
    /// a child that completes normally intentionally KEEPS its link: the child
    /// stays addressable for Reuse/nudge.
    ///
    let unlinkChildren
        (journal: AgentJournal option)
        (parentId: SessionId)
        (childIds: SessionId list)
        : Result<unit, string> =
        match journal with
        | None -> Ok()
        | Some journal ->
            let rec appendRemaining ids =
                match ids with
                | [] -> Ok()
                | childId :: rest ->
                    match
                        AgentJournal.appendAgent
                            (StreamId.Session parentId)
                            None
                            (AgentFact.AgentUnlinked
                                {| ParentId = parentId
                                   ChildId = ChildId.create (SessionId.value childId) |})
                            journal
                    with
                    | Ok _ -> appendRemaining rest
                    | Error failure -> Error(sprintf "%A" failure.Failure)

            appendRemaining childIds

    /// Tear down linked children only after every unlink fact is durable.
    let teardownChildren
        (sessions: ISessionHostPort)
        (journal: AgentJournal option)
        (parentId: SessionId)
        (children: Dictionary<string, SessionId>)
        (gate: obj)
        : Task<Result<unit, string>> =
        task {
            let childIds = lock gate (fun () -> children.Values |> Seq.distinct |> Seq.toList)

            match unlinkChildren journal parentId childIds with
            | Error err -> return Error err
            | Ok() ->
                let mutable firstError: string option = None

                for childId in childIds do
                    try
                        let! abortResult = sessions.AbortSession childId

                        match abortResult, firstError with
                        | Error err, None -> firstError <- Some err
                        | _ -> ()
                    with ex ->
                        if firstError.IsNone then
                            firstError <- Some ex.Message

                match firstError with
                | Some err -> return Error err
                | None -> return Ok()
        }
