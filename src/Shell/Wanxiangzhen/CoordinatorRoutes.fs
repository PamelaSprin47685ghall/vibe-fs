module Wanxiangshu.Shell.Wanxiangzhen.CoordinatorRoutes

open Wanxiangshu.Kernel.Wanxiangzhen.SquadTask
open Wanxiangshu.Kernel.Wanxiangzhen.Dag
open Wanxiangshu.Shell.Wanxiangzhen.HttpCodec
open Wanxiangshu.Shell.Wanxiangzhen.HttpServer
open Wanxiangshu.Shell.Wanxiangzhen.CoordinatorRuntime
open Wanxiangshu.Shell.Wanxiangzhen.CoordinatorOps

let routeHandler (rt: CoordinatorRuntime) : RouteHandler =
    fun method path body ->
        promise {
            let p = path.Split('?').[0]
            match method, p with
            | "POST", p when p.EndsWith "/submit" ->
                let tid = extractTaskId p "submit"
                let sha = decodeSubmitBody body |> Option.defaultValue ""
                return! handleSubmit rt tid sha
            | "POST", p when p.EndsWith "/register" ->
                let tid = extractTaskId p "register"

                match decodeRegisterBody body with
                | Some pid ->
                    do! rt.DagQueue.Enqueue(fun () ->
                        rt.Dag <- rt.Dag |> updateTask tid (fun (t: SquadTask) -> { t with SlavePid = Some pid })
                        Promise.lift ())

                    return
                        { StatusCode = 200
                          Body = encodeResult "registered" }
                | None ->
                    return
                        { StatusCode = 400
                          Body = encodeResult "bad_request" }
            | "POST", p when p.EndsWith "/done" ->
                let tid = extractTaskId p "done"
                do! handleSlaveExit rt tid

                return
                    { StatusCode = 200
                      Body = encodeResult "acknowledged" }
            | "POST", p when p.EndsWith "/log" ->
                let tid = extractTaskId p "log"

                match decodeLogBody body with
                | Some _msg ->
                    return
                        { StatusCode = 200
                          Body = encodeResult "logged" }
                | None ->
                    return
                        { StatusCode = 400
                          Body = encodeResult "bad_request" }
            | "GET", "/state" ->
                return
                    { StatusCode = 200
                      Body = encodeFullState rt.Dag rt.Sessions }
            | "GET", p when p.StartsWith "/task/" ->
                let tid = p.Substring 6

                match findTask tid rt.Dag with
                | None ->
                    return
                        { StatusCode = 404
                          Body = encodeResult "task_not_found" }
                | Some t ->
                    return
                        { StatusCode = 200
                          Body = encodeTaskDetail t }
            | _ ->
                return
                    { StatusCode = 404
                      Body = encodeResult "not_found" }
        }

let startPidPolling (rt: CoordinatorRuntime) : unit =
    rt.PidPollHandle <-
        Some(
            rt.Deps.StartPolling 2000 (fun () ->
                rt.DagQueue.Enqueue(fun () ->
                    promise {
                        let toCheck =
                            rt.Dag.Tasks
                            |> Map.toList
                            |> List.map snd
                            |> List.filter (fun (t: SquadTask) ->
                                (t.Status = Running || t.Status = Submitted) && t.SlavePid.IsSome)

                        for t in toCheck do
                            match t.SlavePid with
                            | Some pid when not (rt.Deps.IsPidAlive pid) -> do! handleSlaveExitCore rt t.Id
                            | Some pid when rt.Deps.IsPidAlive pid ->
                                let now = rt.Deps.Now()
                                rt.Dag <- rt.Dag |> updateTask t.Id (fun x -> { x with LastHeartbeatAt = Some now })
                            | _ -> ()
                    })
                |> Promise.start)
        )
