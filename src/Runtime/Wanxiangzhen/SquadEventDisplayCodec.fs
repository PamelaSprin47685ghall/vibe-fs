module Wanxiangshu.Runtime.Wanxiangzhen.SquadEventDisplayCodec

open System
open Wanxiangshu.Kernel.Wanxiangzhen.SquadEvent
open Wanxiangshu.Runtime.Tooling.ToolOutputToml

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

        { eventKind = kind
          sessionId = sid
          taskId = None
          commitSha = None
          message = msg }
        : SquadEventTomlView
    | TasksCreated(_, _) ->
        { eventKind = kind
          sessionId = sid
          taskId = None
          commitSha = None
          message = prose }
        : SquadEventTomlView
    | TaskStarted(_, tid, wt, branch) ->
        { eventKind = kind
          sessionId = sid
          taskId = Some tid
          commitSha = None
          message = sprintf "%s (Worktree: %s, Branch: %s)" prose wt branch }
        : SquadEventTomlView
    | TaskSubmitted(_, tid, sha) ->
        { eventKind = kind
          sessionId = sid
          taskId = Some tid
          commitSha = Some sha
          message = prose }
        : SquadEventTomlView
    | TaskMerged(_, tid, sha) ->
        { eventKind = kind
          sessionId = sid
          taskId = Some tid
          commitSha = Some sha
          message = prose }
        : SquadEventTomlView
    | TaskDone(_, tid, _) ->
        { eventKind = kind
          sessionId = sid
          taskId = Some tid
          commitSha = None
          message = prose }
        : SquadEventTomlView
    | TaskError(_, tid, _) ->
        { eventKind = kind
          sessionId = sid
          taskId = Some tid
          commitSha = None
          message = prose }
        : SquadEventTomlView
    | SquadCancelled _ ->
        { eventKind = kind
          sessionId = sid
          taskId = None
          commitSha = None
          message = prose }
        : SquadEventTomlView

let encodeEvent (e: SquadEvent) : string = toTomlView e |> renderSquadEvent

let encodeEvents (events: SquadEvent list) : string =
    events |> List.map encodeEvent |> String.concat "\n"
