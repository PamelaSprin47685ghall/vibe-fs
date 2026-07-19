module Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorReplay

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Wanxiangzhen.SquadTask
open Wanxiangshu.Kernel.Wanxiangzhen.Dag
open Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorRuntime

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


let private warnOrphans (rt: CoordinatorRuntime) =
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
            rt.SentWarnings <- rt.SentWarnings.Add warning

            rt.Deps.PromptSession rt.Client rt.MasterSessionId warning
            |> Promise.catch (fun ex ->
                JS.console.error (
                    "CoordinatorReplay: Failed to send orphan warning to prompt session: "
                    + ex.Message
                )

                ())
            |> Promise.start
            |> ignore

let replayFromEventLog (rt: CoordinatorRuntime) : JS.Promise<unit> =
    promise {
        let! latestSidOpt = rt.Deps.GetLatestSquadSessionId()
        let sessionId = defaultArg latestSidOpt ""
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

        if rt.MasterSessionId <> "" then
            warnOrphans rt
    }
