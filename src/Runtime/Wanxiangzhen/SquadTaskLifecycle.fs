module Wanxiangshu.Runtime.Wanxiangzhen.SquadTaskLifecycle

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Wanxiangzhen.SquadTask
open Wanxiangshu.Kernel.Wanxiangzhen.Dag
open Wanxiangshu.Kernel.Wanxiangzhen.SquadEvent
open Wanxiangshu.Kernel.Wanxiangzhen.Scheduler
open Wanxiangshu.Kernel.Wanxiangzhen.FfDecision
open Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorRuntime
open Wanxiangshu.Runtime.Wanxiangzhen.SquadTaskLifecycleStart

let schedulerTick (rt: CoordinatorRuntime) : JS.Promise<unit> =
    if rt.Scheduling then
        Promise.lift ()
    else
        rt.Scheduling <- true

        promise {
            try
                let decision = decide rt.Dag rt.Config.MaxConcurrent

                for tid in decision.TasksToStart do
                    do! startTask rt tid
            finally
                rt.Scheduling <- false
        }

let private cleanupAndReport (rt: CoordinatorRuntime) (task: SquadTask) : JS.Promise<unit> =
    promise {
        cleanupTask rt task

        match rt.GitError with
        | Some err ->
            let! _ = commitEvent rt (TaskError(rt.Dag.SessionId, task.Id, sprintf "cleanup failed: %s" err))
            rt.GitError <- None
        | None -> ()
    }

let handleSlaveExitCore (rt: CoordinatorRuntime) (taskId: string) : JS.Promise<unit> =
    promise {
        match findTask taskId rt.Dag with
        | None -> return ()
        | Some task when isTerminal task.Status -> return ()
        | Some task ->
            let! cr = commitEvent rt (TaskDone(rt.Dag.SessionId, taskId, false))

            match cr with
            | Error _ -> return ()
            | Ok() ->
                let now = rt.Deps.Now()

                rt.Dag <-
                    rt.Dag
                    |> updateTask taskId (fun (t: SquadTask) ->
                        match tryWithStatus t Done now with
                        | Ok t2 -> t2
                        | Error _ -> t)

                do! cleanupAndReport rt task
                do! schedulerTick rt
    }

let handleSlaveExit (rt: CoordinatorRuntime) (taskId: string) : JS.Promise<unit> =
    rt.DagQueue.Enqueue(fun () -> handleSlaveExitCore rt taskId)

let safeKillPid (rt: CoordinatorRuntime) (pid: int) : unit =
    try
        rt.Deps.KillPid pid (box "SIGTERM")
    with ex ->
        rt.GitError <- Some(sprintf "kill pid %d failed: %s" pid (string ex.Message))

let handleNonMergedResult (rt: CoordinatorRuntime) (taskId: string) (result: FfResult) : JS.Promise<unit> =
    promise {
        let n2 = rt.Deps.Now()

        rt.Dag <-
            rt.Dag
            |> updateTask taskId (fun (t: SquadTask) ->
                match tryWithStatus t Running n2 with
                | Ok t2 -> t2
                | Error _ -> t)

        do! schedulerTick rt
    }

let handleMerged (rt: CoordinatorRuntime) (taskId: string) (sha: string) : JS.Promise<unit> =
    promise {
        let! mCommit = commitEvent rt (TaskMerged(rt.Dag.SessionId, taskId, sha))

        match mCommit with
        | Error err ->
            do!
                commitEvent
                    rt
                    (TaskError(rt.Dag.SessionId, taskId, sprintf "git merged but event log write failed: %s" err))
                |> Promise.map ignore
        | Ok() ->
            let n2 = rt.Deps.Now()

            rt.Dag <-
                rt.Dag
                |> updateTask taskId (fun (t: SquadTask) ->
                    match tryWithStatus t SquadTaskStatus.Merged n2 with
                    | Ok t2 -> { t2 with MergedSha = Some sha }
                    | Error _ -> t)

        match findTask taskId rt.Dag with
        | Some t ->
            match t.SlavePid with
            | Some pid ->
                safeKillPid rt pid
                do! rt.Deps.WaitForPidDeath pid 5
            | None -> ()

            do! cleanupAndReport rt t
        | None -> ()

        do! schedulerTick rt
    }

let queryGitStatus (rt: CoordinatorRuntime) (branchName: string) (reportedSha: string) : JS.Promise<FfResult> =
    rt.GitQueue.Enqueue(fun () ->
        promise {
            let branchSha = rt.Deps.RevParseRef rt.ProjectRoot branchName

            if branchSha <> reportedSha then
                return StaleCommit
            else
                let cur = rt.Deps.RevParseBranch rt.ProjectRoot

                if cur <> rt.MasterBranch then
                    return CoordinatorNotReady "not_on_master"
                elif not (rt.Deps.StatusIsClean rt.ProjectRoot) then
                    return CoordinatorNotReady "dirty"
                elif rt.Deps.MergeBaseIsAncestor rt.ProjectRoot rt.MasterBranch branchName then
                    let sha = rt.Deps.MergeFfOnly rt.ProjectRoot branchName
                    return Merged sha
                else
                    let sha = rt.Deps.RevParseRef rt.ProjectRoot rt.MasterBranch
                    return RebaseNeeded sha
        })
