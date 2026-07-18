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
open Wanxiangshu.Runtime.Wanxiangzhen.SquadTaskLifecycle

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

let handleSubmitCore (rt: CoordinatorRuntime) (taskId: string) (reportedSha: string) : JS.Promise<HttpResponse> =
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

                let! result = queryGitStatus rt branchName reportedSha

                match result with
                | Merged sha -> do! handleMerged rt taskId sha
                | _ -> do! handleNonMergedResult rt taskId result

                return
                    { StatusCode = 200
                      Body = encodeFfResponseBody result }
        }

let handleSubmit (rt: CoordinatorRuntime) (taskId: string) (reportedSha: string) : JS.Promise<HttpResponse> =
    rt.DagQueue.Enqueue(fun () -> handleSubmitCore rt taskId reportedSha)

let schedulerTick rt = SquadTaskLifecycle.schedulerTick rt

let handleSlaveExit rt taskId =
    SquadTaskLifecycle.handleSlaveExit rt taskId

let handleSlaveExitCore rt taskId =
    SquadTaskLifecycle.handleSlaveExitCore rt taskId

let safeKillPid rt pid = SquadTaskLifecycle.safeKillPid rt pid
