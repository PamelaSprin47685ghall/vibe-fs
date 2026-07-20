module Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorReplay

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Wanxiangzhen.SquadTask
open Wanxiangshu.Kernel.Wanxiangzhen.Dag
open Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorRuntime
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope

let private reconcileTask (rt: CoordinatorRuntime) hasCommits now (_id: string) (t: SquadTask) =
    if t.Status = Submitted || t.Status = Running then
        match rt.GitError with
        | Some _ ->
            if t.Status = Submitted then
                applyStatus ReplayFact t Running now
            else
                t
        | None ->
            match t.BranchName with
            | Some b when hasCommits && rt.Deps.MergeBaseIsAncestor rt.ProjectRoot rt.MasterBranch b ->
                let sha = rt.Deps.RevParseRef rt.ProjectRoot rt.MasterBranch

                { (applyStatus ReplayFact t Merged now) with
                    MergedSha = Some sha }
            | _ ->
                if t.Status = Submitted then
                    applyStatus ReplayFact t Running now
                else
                    t
    else
        t


let private warnOrphans (rt: CoordinatorRuntime) : JS.Promise<unit> =
    promise {
        let orphans =
            rt.Dag.Tasks
            |> Map.toList
            |> List.map snd
            |> List.filter (fun (t: SquadTask) -> t.Status = Running && t.SlavePid.IsNone)
        if orphans <> [] then
            let names = orphans |> List.map (fun (t: SquadTask) -> t.Id) |> String.concat ", "
            let warning =
                sprintf "WARNING: Orphan running tasks without PID: %s. Use /squad-kill or ignore." names
            // Idempotency check / Dedup
            if not (rt.SentWarnings.Contains warning) then
                if rt.MasterSessionId = "" then
                    let diagnostics =
                        createObj
                            [ "event", box "wanxiangzhen_orphan_tasks_diagnostic"
                              "message", box "MasterSessionId is empty, cannot send prompt warning"
                              "warning", box warning
                              "orphans", box (orphans |> List.map (fun (t: SquadTask) -> t.Id) |> List.toArray) ]
                    JS.console.error (diagnostics)
                    rt.SentWarnings <- rt.SentWarnings.Add warning
                    // Log failure to send warning as an auditable event
                    let sid = if rt.Dag.SessionId <> "" then rt.Dag.SessionId else "unknown_session"
                    let errEvent: WanEvent =
                        { V = 1
                          Session = sid
                          Kind = "wanxiangzhen_prompt_failed"
                          At = rt.Deps.Now()
                          Payload = Map [ "text", warning; "error", "MasterSessionId is empty, cannot send prompt warning" ] }
                    let! _ = rt.Deps.AppendWanEvent rt.ProjectRoot errEvent
                    ()
                else
                    try
                        do! rt.Deps.PromptSession rt.Client rt.MasterSessionId warning
                        rt.SentWarnings <- rt.SentWarnings.Add warning
                        // Log successful warning sent event
                        let wanEvent: WanEvent =
                            { V = 1
                              Session = rt.MasterSessionId
                              Kind = "wanxiangzhen_warning_sent"
                              At = rt.Deps.Now()
                              Payload = Map [ "warning", warning ] }
                        let! _ = rt.Deps.AppendWanEvent rt.ProjectRoot wanEvent
                        ()
                    with ex ->
                        JS.console.error (
                            "CoordinatorReplay: Failed to send orphan warning to prompt session: "
                            + ex.Message
                        )
                        // Log prompt failure as an auditable event
                        let failedEvent: WanEvent =
                            { V = 1
                              Session = rt.MasterSessionId
                              Kind = "wanxiangzhen_prompt_failed"
                              At = rt.Deps.Now()
                              Payload = Map [ "text", warning; "error", ex.Message ] }
                        let! _ = rt.Deps.AppendWanEvent rt.ProjectRoot failedEvent
                        ()
    }

let replayFromEventLog (rt: CoordinatorRuntime) : JS.Promise<unit> =
    promise {
        let! latestSidOpt = rt.Deps.GetLatestSquadSessionId()
        let sessionId = defaultArg latestSidOpt ""

        // Recover SentWarnings from log
        let! events = rt.Deps.ReadWanEvents rt.ProjectRoot
        let historicalWarnings =
            events
            |> List.filter (fun e -> e.Session = sessionId && e.Kind = "wanxiangzhen_warning_sent")
            |> List.choose (fun e -> Map.tryFind "warning" e.Payload)
        rt.SentWarnings <- Set.ofList historicalWarnings

        let! currentDag = rt.Deps.GetSquadDag sessionId
        let! dags = rt.Deps.GetSquadSessions()
        let sessions = dags |> Map.remove sessionId

        let hasCommits = rt.Deps.HasCommits rt.ProjectRoot
        let now = rt.Deps.Now()

        let reconciledTasks = currentDag.Tasks |> Map.map (reconcileTask rt hasCommits now)

        rt.Dag <-
            { currentDag with
                Tasks = reconciledTasks }

        rt.Sessions <- sessions

        do! warnOrphans rt
    }
