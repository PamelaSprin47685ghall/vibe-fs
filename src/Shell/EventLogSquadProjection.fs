module Wanxiangshu.Shell.EventLogSquadProjection

open Wanxiangshu.Kernel.EventLog.Types
open Wanxiangshu.Kernel.Wanxiangzhen.Dag
open Wanxiangshu.Kernel.Wanxiangzhen.SquadEvent
open Wanxiangshu.Shell.Wanxiangzhen.SquadEventWanCodec

type SquadProjection = { Dags: Map<string, Dag> }

let emptyProjection () = { Dags = Map.empty }

let applySquadEvent (proj: SquadProjection) (sid: string) (se: SquadEvent) : SquadProjection =
    let dag0 =
        match Map.tryFind sid proj.Dags with
        | Some d -> d
        | None -> empty sid ""

    { proj with
        Dags = Map.add sid (foldEvent dag0 se) proj.Dags }

let applyWanEvent (proj: SquadProjection) (e: WanEvent) : SquadProjection =
    if not (isSquadEventKind e.Kind) then
        proj
    else
        match trySquadEventFromWanEvent e with
        | Some se -> applySquadEvent proj e.Session se
        | None -> proj

let getDag (proj: SquadProjection) (sessionId: string) : Dag =
    match Map.tryFind sessionId proj.Dags with
    | Some d -> d
    | None -> empty sessionId ""
