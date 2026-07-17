module Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorOps

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Wanxiangzhen.SquadTask
open Wanxiangshu.Kernel.Wanxiangzhen.Dag
open Wanxiangshu.Kernel.Wanxiangzhen.SquadEvent
open Wanxiangshu.Kernel.Wanxiangzhen.Scheduler
open Wanxiangshu.Kernel.Wanxiangzhen.SquadConfig
open Wanxiangshu.Kernel.Wanxiangzhen.SquadPrompts
open Wanxiangshu.Kernel.Wanxiangzhen.FfDecision
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Wanxiangzhen.GitShell
open Wanxiangshu.Runtime.PromiseQueue
open Wanxiangshu.Runtime.Wanxiangzhen.HttpServer
open Wanxiangshu.Runtime.Wanxiangzhen.HttpCodec
open Wanxiangshu.Runtime.Wanxiangzhen.ConfigReader
open Wanxiangshu.Runtime.Wanxiangzhen.SessionIo
open Wanxiangshu.Runtime.Wanxiangzhen.SlaveSpawn
open Wanxiangshu.Runtime.Wanxiangzhen.PidMonitor
open Wanxiangshu.Runtime.Wanxiangzhen.SymlinkShell
open Wanxiangshu.Runtime.Yaml
open Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorRuntime

[<Import("dirname", "node:path")>]
let private pathDirname (p: string) : string = jsNative

[<Import("join", "node:path")>]
let private pathJoin (a: string) (b: string) : string = jsNative

[<Global("globalThis.process")>]
let private nodeProcess: obj = jsNative

let internal extractTaskId (path: string) (suffix: string) : string =
    let prefix = "/task/"
    let suf = "/" + suffix

    if path.StartsWith prefix && path.EndsWith suf then
        path.Substring(prefix.Length, path.Length - prefix.Length - suf.Length)
    else
        ""

let formatDagText (rt: CoordinatorRuntime) : string = formatDag rt.Dag

let rec private resolveBranchName (rt: CoordinatorRuntime) (taskId: string) (attempts: int) : string =
    let candidate = taskId

    if attempts <= 0 then
        candidate
    elif rt.Deps.ShowRefExists rt.ProjectRoot candidate then
        let suffix = (generateTaskId ()).Substring 6
        resolveBranchName rt (taskId + "-" + suffix) (attempts - 1)
    else
        candidate

let private startTask (rt: CoordinatorRuntime) (taskId: string) : JS.Promise<unit> =
    promise {
        match findTask taskId rt.Dag with
        | None -> return ()
        | Some task ->
            if not (rt.Deps.HasCommits rt.ProjectRoot) then
                let errorMessage =
                    "Repository has no commits. Run 'git commit --allow-empty -m \"Initial commit\"' before using /squad."

                let! _ = commitEvent rt (TaskError(rt.Dag.SessionId, taskId, errorMessage))
                return ()

            let parent = pathDirname rt.ProjectRoot
            let branchName = resolveBranchName rt taskId 5
            let wtPath = pathJoin parent ("worktree-" + branchName)

            match rt.Deps.TryWorktreeAdd rt.ProjectRoot branchName wtPath rt.MasterBranch with
            | Error e ->
                let! _ = commitEvent rt (TaskError(rt.Dag.SessionId, taskId, e))
                return ()
            | Ok _ ->
                let! cr = commitEvent rt (TaskStarted(rt.Dag.SessionId, taskId, wtPath, branchName))

                match cr with
                | Error _ -> return ()
                | Ok() ->
                    rt.Deps.CreateSymlinks wtPath rt.ProjectRoot rt.Config.SharedDirs
                    let prompt = buildSlavePrompt taskId task.Title task.Description rt.MasterBranch
                    let slaveEnv = createObj []
                    assignInto slaveEnv (get nodeProcess "env") |> ignore
                    setKey slaveEnv "SQUAD_COORDINATOR_URL" (box rt.CoordinatorUrl)
                    setKey slaveEnv "SQUAD_TASK_ID" (box taskId)
                    setKey slaveEnv "SQUAD_WORKTREE_PATH" (box wtPath)
                    setKey slaveEnv "SQUAD_MASTER_BRANCH" (box rt.MasterBranch)
                    setKey slaveEnv "SQUAD_TOKEN" (box rt.Token)
                    rt.Deps.SpawnSlave rt.Config.Terminal wtPath slaveEnv prompt
                    let now = rt.Deps.Now()

                    rt.Dag <-
                        rt.Dag
                        |> updateTask taskId (fun (t: SquadTask) ->
                            match tryWithStatus t Running now with
                            | Ok t2 ->
                                { t2 with
                                    WorktreePath = Some wtPath
                                    BranchName = Some branchName }
                            | Error _ -> t)
    }

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

// ARCHITECTURE_EXEMPT: split this 110-line function later
let private handleSubmitCore
    (rt: CoordinatorRuntime)
    (taskId: string)
    (reportedSha: string)
    : JS.Promise<HttpResponse> =
    match findTask taskId rt.Dag with
    | None ->
        Promise.lift
            { StatusCode = 404
              Body = encodeResult "task_not_found" }
    | Some task when task.Status <> Running ->
        Promise.lift
            { StatusCode = 200
              Body = encodeFfResponseBody (NotSubmittable task.Status) }
    | Some task ->
        let branchName = task.BranchName |> Option.defaultValue taskId

        promise {
            let! subCommit = commitEvent rt (TaskSubmitted(rt.Dag.SessionId, taskId, reportedSha))

            match subCommit with
            | Error _ ->
                return
                    { StatusCode = 503
                      Body = encodeResult "event_log_failed" }
            | Ok() ->
                let now = rt.Deps.Now()

                rt.Dag <-
                    rt.Dag
                    |> updateTask taskId (fun (t: SquadTask) ->
                        match tryWithStatus t Submitted now with
                        | Ok t2 -> t2
                        | Error _ -> t)

                let! result =
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

                match result with
                | Merged sha ->
                    let! mCommit = commitEvent rt (TaskMerged(rt.Dag.SessionId, taskId, sha))

                    match mCommit with
                    | Error err ->
                        do!
                            commitEvent
                                rt
                                (TaskError(
                                    rt.Dag.SessionId,
                                    taskId,
                                    sprintf "git merged but event log write failed: %s" err
                                ))
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
                | _ ->
                    let n2 = rt.Deps.Now()

                    rt.Dag <-
                        rt.Dag
                        |> updateTask taskId (fun (t: SquadTask) ->
                            match tryWithStatus t Running n2 with
                            | Ok t2 -> t2
                            | Error _ -> t)

                    do! schedulerTick rt

                return
                    { StatusCode = 200
                      Body = encodeFfResponseBody result }
        }

let handleSubmit (rt: CoordinatorRuntime) (taskId: string) (reportedSha: string) : JS.Promise<HttpResponse> =
    rt.DagQueue.Enqueue(fun () -> handleSubmitCore rt taskId reportedSha)
