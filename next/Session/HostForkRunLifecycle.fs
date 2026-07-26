namespace Wanxiangshu.Next.Session

open System.Collections.Generic
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Kernel.Identity

/// Per-run terminal lifecycle for HostForkRuntime: install, complete, fail.
module HostForkRunLifecycle =

    let outputSince (sessions: ISessionHostPort) (run: PendingHostRun) =
        let all = sessions.GetSessionOutput run.ChildId
        let start = max 0 (min run.FallbackOutputCount all.Length)
        let output = all |> List.skip start

        output
        |> List.filter (fun line -> not (line.StartsWith("Prompt: ")) && not (line.StartsWith("ChildPrompt: ")))
        |> String.concat "\n"

    let complete
        (gate: obj)
        (pendingRuns: Dictionary<string, PendingHostRun>)
        (sessions: ISessionHostPort)
        (run: PendingHostRun)
        (outcome: TerminalOutcome)
        =
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
