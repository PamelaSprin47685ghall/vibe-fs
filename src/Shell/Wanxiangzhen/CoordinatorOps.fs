module Wanxiangshu.Shell.Wanxiangzhen.CoordinatorOps

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Wanxiangzhen.SquadTask
open Wanxiangshu.Kernel.Wanxiangzhen.Dag
open Wanxiangshu.Kernel.Wanxiangzhen.SquadEvent
open Wanxiangshu.Kernel.Wanxiangzhen.Scheduler
open Wanxiangshu.Kernel.Wanxiangzhen.SquadConfig
open Wanxiangshu.Kernel.Wanxiangzhen.SquadPrompts
open Wanxiangshu.Kernel.Wanxiangzhen.FfDecision
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.Wanxiangzhen.GitShell
open Wanxiangshu.Shell.PromiseQueue
open Wanxiangshu.Shell.Wanxiangzhen.HttpServer
open Wanxiangshu.Shell.Wanxiangzhen.HttpCodec
open Wanxiangshu.Shell.Wanxiangzhen.SquadEventDisplayCodec
open Wanxiangshu.Shell.Wanxiangzhen.ConfigReader
open Wanxiangshu.Shell.Wanxiangzhen.SessionIo
open Wanxiangshu.Shell.Wanxiangzhen.SlaveSpawn
open Wanxiangshu.Shell.Wanxiangzhen.PidMonitor
open Wanxiangshu.Shell.Wanxiangzhen.SymlinkShell
open Wanxiangshu.Kernel.Yaml
open Wanxiangshu.Shell.Wanxiangzhen.CoordinatorRuntime

[<Global("process")>]
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

            let parent =
                let lastSlash = rt.ProjectRoot.LastIndexOf '/'
                rt.ProjectRoot.Substring(0, lastSlash + 1)

            let branchName = resolveBranchName rt taskId 5
            let wtPath = parent + "worktree-" + branchName

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
                            { (withStatus t Running now) with
                                WorktreePath = Some wtPath
                                BranchName = Some branchName })
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

let handleSlaveExit (rt: CoordinatorRuntime) (taskId: string) : JS.Promise<unit> =
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
                rt.Dag <- rt.Dag |> updateTask taskId (fun (t: SquadTask) -> withStatus t Done now)
                cleanupTask rt task
                do! schedulerTick rt
    }

let safeKillPid (deps: CoordinatorDeps) (pid: int) : unit =
    try
        deps.KillPid pid (box "SIGTERM")
    with _ ->
        ()

let handleSubmit (rt: CoordinatorRuntime) (taskId: string) (reportedSha: string) : JS.Promise<HttpResponse> =
    match findTask taskId rt.Dag with
    | None ->
        Promise.lift
            { StatusCode = 404
              Body = encodeResult "task_not_found" }
    | Some task when task.Status <> Running ->
        Promise.lift
            { StatusCode = 200
              Body = encodeFfResponseBody (NotSubmittable(statusToString task.Status)) }
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
                rt.Dag <- rt.Dag |> updateTask taskId (fun (t: SquadTask) -> withStatus t Submitted now)

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
                    | Error _ ->
                        let n2 = rt.Deps.Now()
                        rt.Dag <- rt.Dag |> updateTask taskId (fun (t: SquadTask) -> withStatus t Running n2)
                    | Ok() ->
                        let n2 = rt.Deps.Now()

                        rt.Dag <-
                            rt.Dag
                            |> updateTask taskId (fun (t: SquadTask) ->
                                { (withStatus t SquadTaskStatus.Merged n2) with
                                    MergedSha = Some sha })

                    match findTask taskId rt.Dag with
                    | Some t ->
                        match t.SlavePid with
                        | Some pid ->
                            safeKillPid rt.Deps pid
                            do! rt.Deps.WaitForPidDeath pid 5
                        | None -> ()

                        cleanupTask rt t
                    | None -> ()

                    do! schedulerTick rt
                | _ ->
                    let n2 = rt.Deps.Now()
                    rt.Dag <- rt.Dag |> updateTask taskId (fun (t: SquadTask) -> withStatus t Running n2)
                    do! schedulerTick rt

                return
                    { StatusCode = 200
                      Body = encodeFfResponseBody result }
        }
