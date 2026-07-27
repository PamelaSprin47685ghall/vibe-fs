namespace Wanxiangshu.Next.Session

open System.Collections.Generic
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
              Directory = directory }
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
            | Completed _ -> run.Source.SetResult(Ok(outputSince sessions run))
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

    /// Persist AgentUnlinked facts for each distinct child BEFORE aborting, so a
    /// crash mid-Cancel cannot leave a session aborted but still linked (which
    /// would make a restart restore a dead child). A leaked abort is recoverable;
    /// a leaked link is not.
    ///
    /// Timing adjudication: unlink is driven ONLY by the parent's Cancel (the sole
    /// teardown path — HostForkRuntime has no other Dispose hook). There is no
    /// child-normal-close host event (host docs confirm no `session.deleted`), so
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
        : Result<unit, string> =
        let childIds = lock gate (fun () -> children.Values |> Seq.distinct |> Seq.toList)

        match unlinkChildren journal parentId childIds with
        | Error err -> Error err
        | Ok() ->
            childIds |> List.iter (fun childId -> sessions.AbortSession childId |> ignore)
            Ok()
