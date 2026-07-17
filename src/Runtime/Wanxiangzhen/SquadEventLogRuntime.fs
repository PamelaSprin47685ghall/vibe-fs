module Wanxiangshu.Runtime.Wanxiangzhen.SquadEventLogRuntime

open Fable.Core
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Kernel.Wanxiangzhen.SquadEvent
open Wanxiangshu.Runtime.EventLogRuntime
open Wanxiangshu.Runtime.SquadEventStore
open Wanxiangshu.Runtime.Wanxiangzhen.SquadEventWanCodec
open Wanxiangshu.Runtime.Clock

let readAllSquadEvents (workspaceRoot: string) : JS.Promise<SquadEvent list> =
    promise {
        let! events = getStore(workspaceRoot).ReadAllEvents()
        return events |> List.choose trySquadEventFromWanEvent
    }

let appendSquadEvent (workspaceRoot: string) (at: string) (e: SquadEvent) : JS.Promise<Result<unit, string>> =
    getStore(workspaceRoot).AppendSquadEvent at e

let appendSquadEventNow (workspaceRoot: string) (e: SquadEvent) : JS.Promise<Result<unit, string>> =
    appendSquadEvent workspaceRoot (getTimestampMs().ToString()) e
