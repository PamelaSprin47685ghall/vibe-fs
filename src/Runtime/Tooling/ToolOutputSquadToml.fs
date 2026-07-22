module Wanxiangshu.Runtime.Tooling.ToolOutputSquadToml

open Wanxiangshu.Runtime.Serialization.TomlValue
open Wanxiangshu.Runtime.Serialization.Toml

type SquadEventPayload =
    | SquadCreated of requirement: string option
    | TasksCreated
    | TaskStarted of taskId: string * worktree: string * branch: string
    | TaskSubmitted of taskId: string * commitSha: string
    | TaskMerged of taskId: string * commitSha: string
    | TaskDone of taskId: string * success: bool
    | TaskError of taskId: string * error: string
    | SquadCancelled

type SquadEventTomlView =
    { eventKind: string
      sessionId: string
      taskId: string option
      commitSha: string option
      payload: SquadEventPayload }

let squadEventDocument (view: SquadEventTomlView) : TomlValue =
    let mutable fields =
        [ "event_kind", String view.eventKind
          "session_id", String view.sessionId ]

    match view.taskId with
    | Some tid -> fields <- fields @ [ "task_id", String tid ]
    | None -> ()

    match view.commitSha with
    | Some sha -> fields <- fields @ [ "commit_sha", String sha ]
    | None -> ()

    let payloadFields =
        match view.payload with
        | SquadCreated req ->
            match req with
            | Some r when r <> "" -> [ "requirement", String r ]
            | _ -> []
        | TaskStarted(_, worktree, branch) ->
            [ "worktree", String worktree
              "branch", String branch ]
        | TaskDone(_, success) -> [ "success", Boolean success ]
        | TaskError(_, error) -> [ "error", String error ]
        | TasksCreated
        | TaskSubmitted _
        | TaskMerged _
        | SquadCancelled -> []

    Table (fields @ payloadFields)

let renderSquadEvent (view: SquadEventTomlView) : string = squadEventDocument view |> stringify
