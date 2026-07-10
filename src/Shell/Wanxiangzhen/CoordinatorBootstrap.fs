module Wanxiangshu.Shell.Wanxiangzhen.CoordinatorBootstrap

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Wanxiangzhen.Dag
open Wanxiangshu.Kernel.Wanxiangzhen.SquadConfig
open Wanxiangshu.Shell.PromiseQueue
open Wanxiangshu.Shell.Wanxiangzhen.HttpServer
open Wanxiangshu.Shell.Wanxiangzhen.CoordinatorRuntime
open Wanxiangshu.Shell.Wanxiangzhen.CoordinatorRoutes
open Wanxiangshu.Shell.Wanxiangzhen.PidMonitor

let createWithDeps
    (client: obj)
    (directory: string)
    (config: SquadConfig)
    (masterBranch: string)
    (gitError: string option)
    (deps: CoordinatorDeps)
    : JS.Promise<CoordinatorRuntime> =
    promise {
        let token =
            System.String([| for _ in 0..31 -> "0123456789abcdef".[int (deps.RandomGen () * 16.0)] |])

        let rtRef = ref None

        let! server =
            startServer token (fun m p b ->
                promise {
                    match rtRef.Value with
                    | None ->
                        return
                            { StatusCode = 503
                              Body = box {| result = "not_ready" |} }
                    | Some r -> return! routeHandler r m p b
                })

        let runtime =
            { Dag = empty "" ""
              Sessions = Map.empty
              Config = config
              MasterBranch = masterBranch
              ProjectRoot = directory
              MasterSessionId = ""
              Client = client
              Token = token
              CoordinatorUrl = server.Url
              GitQueue = SerialQueue()
              DagQueue = SerialQueue()
              InjectQueue = SerialQueue()
              Server = server
              Scheduling = false
              PidPollHandle = None
              GitError = gitError
              InjectError = None
              Deps = deps }

        rtRef.Value <- Some runtime
        startPidPolling runtime
        return runtime
    }
