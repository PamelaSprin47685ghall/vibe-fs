module Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorLifecycle

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Wanxiangzhen.SquadTask
open Wanxiangshu.Kernel.Wanxiangzhen.Dag
open Wanxiangshu.Kernel.Wanxiangzhen.SquadEvent
open Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorRuntime
open Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorOps

let handleSquadKillCore (rt: CoordinatorRuntime) (optSessionId: string option) : JS.Promise<unit> =
    promise {
        let targetDagOpt =
            match optSessionId with
            | Some sid when sid <> rt.Dag.SessionId -> rt.Sessions.TryFind sid
            | _ -> Some rt.Dag

        match targetDagOpt with
        | None -> ()
        | Some targetDag ->
            let toKill =
                targetDag.Tasks
                |> Map.toList
                |> List.map snd
                |> List.filter (fun (t: SquadTask) -> t.Status = Running || t.Status = Submitted)

            let targetSessionId =
                match optSessionId with
                | Some sid when sid <> rt.Dag.SessionId -> sid
                | _ -> rt.Dag.SessionId

            for t in toKill do
                t.SlavePid |> Option.iter (safeKillPid rt)

            let! appendOk = commitEvent rt (SquadCancelled targetSessionId)

            match appendOk with
            | Error err ->
                rt.InjectError <- Some(sprintf "squad_cancelled append failed for %s: %s" targetSessionId err)
            | Ok() ->
                let updated = foldEvent targetDag (SquadCancelled targetSessionId)

                if targetSessionId = rt.Dag.SessionId then
                    rt.Dag <- updated
                else
                    rt.Sessions <- rt.Sessions.Add(targetSessionId, updated)

                schedulerTick rt |> Promise.start
    }

let handleSquadKill (rt: CoordinatorRuntime) (optSessionId: string option) : JS.Promise<unit> =
    rt.DagQueue.Enqueue(fun () -> handleSquadKillCore rt optSessionId)
