module Wanxiangshu.Runtime.Wanxiangzhen.SquadEventDisplayCodec

open System
open Wanxiangshu.Kernel.Wanxiangzhen.SquadEvent
open Wanxiangshu.Runtime.Tooling.ToolOutputSquadToml

let private makeView kind sid tid sha payload : SquadEventTomlView =
    { eventKind = kind
      sessionId = sid
      taskId = tid
      commitSha = sha
      payload = payload }

let toTomlView (e: SquadEvent) : SquadEventTomlView =
    let sid = eventSessionId e
    let kind = eventTypeName e

    match e with
    | SquadEvent.SquadCreated(_, req) ->
        let reqOpt =
            if String.IsNullOrWhiteSpace req then None else Some req
        makeView kind sid None None (SquadEventPayload.SquadCreated reqOpt)
    | SquadEvent.TasksCreated _ -> makeView kind sid None None SquadEventPayload.TasksCreated
    | SquadEvent.TaskStarted(_, tid, wt, branch) ->
        makeView kind sid (Some tid) None (SquadEventPayload.TaskStarted(tid, wt, branch))
    | SquadEvent.TaskSubmitted(_, tid, sha) ->
        makeView kind sid (Some tid) (Some sha) (SquadEventPayload.TaskSubmitted(tid, sha))
    | SquadEvent.TaskMerged(_, tid, sha) ->
        makeView kind sid (Some tid) (Some sha) (SquadEventPayload.TaskMerged(tid, sha))
    | SquadEvent.TaskDone(_, tid, merged) ->
        makeView kind sid (Some tid) None (SquadEventPayload.TaskDone(tid, merged))
    | SquadEvent.TaskError(_, tid, err) ->
        makeView kind sid (Some tid) None (SquadEventPayload.TaskError(tid, err))
    | SquadEvent.SquadCancelled _ -> makeView kind sid None None SquadEventPayload.SquadCancelled

let encodeEvent (e: SquadEvent) : string = toTomlView e |> renderSquadEvent

let encodeEvents (events: SquadEvent list) : string =
    events |> List.map encodeEvent |> String.concat "\n"
