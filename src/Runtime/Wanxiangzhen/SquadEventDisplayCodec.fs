module Wanxiangshu.Runtime.Wanxiangzhen.SquadEventDisplayCodec

open System
open Wanxiangshu.Kernel.Wanxiangzhen.SquadEvent
open Wanxiangshu.Runtime.Tooling.ToolOutputToml

let private makeView kind sid tid sha msg : SquadEventTomlView =
    { eventKind = kind
      sessionId = sid
      taskId = tid
      commitSha = sha
      message = msg }

let toTomlView (e: SquadEvent) : SquadEventTomlView =
    let sid = eventSessionId e
    let kind = eventTypeName e
    let prose = eventProse e

    match e with
    | SquadCreated(_, req) ->
        let msg =
            if String.IsNullOrWhiteSpace req then
                prose
            else
                sprintf "%s\nRequirement: %s" prose req

        makeView kind sid None None msg
    | TasksCreated _ -> makeView kind sid None None prose
    | TaskStarted(_, tid, wt, branch) ->
        let msg = sprintf "%s (Worktree: %s, Branch: %s)" prose wt branch
        makeView kind sid (Some tid) None msg
    | TaskSubmitted(_, tid, sha) -> makeView kind sid (Some tid) (Some sha) prose
    | TaskMerged(_, tid, sha) -> makeView kind sid (Some tid) (Some sha) prose
    | TaskDone(_, tid, _) -> makeView kind sid (Some tid) None prose
    | TaskError(_, tid, _) -> makeView kind sid (Some tid) None prose
    | SquadCancelled _ -> makeView kind sid None None prose

let encodeEvent (e: SquadEvent) : string = toTomlView e |> renderSquadEvent

let encodeEvents (events: SquadEvent list) : string =
    events |> List.map encodeEvent |> String.concat "\n"
