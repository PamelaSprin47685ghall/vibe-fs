module Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorReplay

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Wanxiangzhen.SquadTask
open Wanxiangshu.Kernel.Wanxiangzhen.Dag
open Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorRuntime
open Wanxiangshu.Runtime.Wanxiangzhen.OrphanNotify

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

/// Best-effort orphan notification: failures never abort replay; only successful
/// delivery reserves durable idempotency (wanxiangzhen_warning_sent). In-flight
/// reservation prevents double-send; failure releases the key so retry can proceed.
let private warnOrphans (rt: CoordinatorRuntime) : JS.Promise<unit> =
    promise {
        let orphans =
            rt.Dag.Tasks
            |> Map.toList
            |> List.map snd
            |> List.filter (fun (t: SquadTask) -> t.Status = Running && t.SlavePid.IsNone)

        if orphans = [] then
            return ()
        else
            let orphanIds = orphans |> List.map (fun (t: SquadTask) -> t.Id)
            let key = idempotencyKey orphanIds
            let warning = warningText orphanIds

            if rt.SentWarnings.Contains key then
                return ()
            else
                // Reserve before host call: concurrent/retry path cannot double-send.
                rt.SentWarnings <- rt.SentWarnings.Add key

                let releaseReservation () =
                    rt.SentWarnings <- rt.SentWarnings.Remove key

                let auditSession =
                    if rt.MasterSessionId <> "" then
                        rt.MasterSessionId
                    elif rt.Dag.SessionId <> "" then
                        rt.Dag.SessionId
                    else
                        "unknown_session"

                if rt.MasterSessionId = "" then
                    let err = "MasterSessionId is empty, cannot send prompt warning"

                    let diagnostics =
                        createObj
                            [ "event", box "wanxiangzhen_orphan_tasks_diagnostic"
                              "message", box err
                              "idempotencyKey", box key
                              "warning", box warning
                              "orphans", box (orphanIds |> List.toArray) ]

                    JS.console.error diagnostics
                    let! _ = rt.Deps.AppendWanEvent rt.ProjectRoot (promptFailedEvent auditSession (rt.Deps.Now()) key warning err)
                    releaseReservation ()
                    return ()
                else
                    try
                        do! rt.Deps.PromptSession rt.Client rt.MasterSessionId warning
                        let! _ =
                            rt.Deps.AppendWanEvent
                                rt.ProjectRoot
                                (warningSentEvent rt.MasterSessionId (rt.Deps.Now()) key warning)
                        // Keep reservation = durable success (also recovered from event log).
                        return ()
                    with ex ->
                        JS.console.error (
                            createObj
                                [ "event", box "wanxiangzhen_orphan_notify_failed"
                                  "idempotencyKey", box key
                                  "sessionId", box rt.MasterSessionId
                                  "error", box ex.Message
                                  "warning", box warning ]
                        )

                        let! _ =
                            rt.Deps.AppendWanEvent
                                rt.ProjectRoot
                                (promptFailedEvent rt.MasterSessionId (rt.Deps.Now()) key warning ex.Message)

                        releaseReservation ()
                        return ()
    }

let replayFromEventLog (rt: CoordinatorRuntime) : JS.Promise<unit> =
    promise {
        let! latestSidOpt = rt.Deps.GetLatestSquadSessionId()
        let sessionId = defaultArg latestSidOpt ""

        let! events = rt.Deps.ReadWanEvents rt.ProjectRoot
        // Union keeps in-flight reservations across concurrent replay; recovered keys are durable successes.
        rt.SentWarnings <- Set.union rt.SentWarnings (recoverSentKeys sessionId events)

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
