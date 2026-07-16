module Wanxiangshu.Tests.BacklogMessageBuilders

open Fable.Core
open Fable.Core.JsInterop

open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Runtime.BacklogProjectionBuild
open Wanxiangshu.Kernel.ToolExecutionStatusModule


let mkInfo (id: string) (role: Role) : MessageInfo<obj> =
    { id = id
      sessionID = "test"
      role = role
      agent = ""
      isError = false
      toolName = ""
      details = null
      time = null }

let mkState (status: string) (output: string) (input: obj) : ToolState<obj> =
    { status = fromString status
      output = output
      error = ""
      input = input
      operationAction = "" }

let userMsg (id: string) (text: string) : Message<obj> =
    { info = mkInfo id User
      parts = [ TextPart text ]
      source = Native
      raw = null }

let timedTodoWriteMsg (id: string) (callID: string) (report: string) (created: int) (completed: int) : Message<obj> =
    let input =
        box (
            createObj
                [ "ahaMoments", box report
                  "changesAndReasons", box ""
                  "gotchas", box ""
                  "lessonsAndConventions", box ""
                  "plan", box ""
                  "todos", box [||] ]
        )

    let time = box (createObj [ "created", box created; "completed", box completed ])

    { info = { mkInfo id Assistant with time = time }
      parts = [ ToolPart(todoWriteToolNameDefault, callID, Some(mkState "completed" "Todos updated." input), null) ]
      source = Native
      raw = null }

let todoWriteMsg (id: string) (callID: string) (report: string) : Message<obj> =
    let input =
        box (
            createObj
                [ "ahaMoments", box report
                  "changesAndReasons", box ""
                  "gotchas", box ""
                  "lessonsAndConventions", box ""
                  "plan", box ""
                  "todos", box [||] ]
        )

    { info = mkInfo id Assistant
      parts = [ ToolPart(todoWriteToolNameDefault, callID, Some(mkState "completed" "Todos updated." input), null) ]
      source = Native
      raw = null }

let todoWriteErrorMsg (id: string) (callID: string) (errorText: string) : Message<obj> =
    { info = mkInfo id Assistant
      parts =
        [ ToolPart(
              todoWriteToolNameDefault,
              callID,
              Some(
                  { status = ToolExecutionStatus.Error
                    output = ""
                    error = errorText
                    input = box (createObj [])
                    operationAction = "" }
              ),
              null
          ) ]
      source = Native
      raw = null }

let assistantTextMsg (id: string) (text: string) : Message<obj> =
    { info = mkInfo id Assistant
      parts = [ TextPart text ]
      source = Native
      raw = null }

let reasoningMsg (id: string) (text: string) : Message<obj> =
    { info = mkInfo id Assistant
      parts = [ RawPart(box (createObj [ "type", box "reasoning"; "text", box text ])) ]
      source = Native
      raw = null }

let reviewMsg (id: string) (callID: string) (output: string) : Message<obj> =
    let input = box (createObj [ "review", box "looks good" ])

    { info = mkInfo id Assistant
      parts = [ ToolPart(reviewToolName, callID, Some(mkState "completed" output input), null) ]
      source = Native
      raw = null }

let taskMsgWithReport (id: string) (callID: string) (report: string) : Message<obj> =
    let input =
        box (
            createObj
                [ "operation", box (createObj [ "action", box "list" ])
                  "ahaMoments", box report
                  "changesAndReasons", box ""
                  "gotchas", box ""
                  "lessonsAndConventions", box ""
                  "plan", box "" ]
        )

    { info = mkInfo id Assistant
      parts =
        [ ToolPart(
              "task",
              callID,
              Some(
                  { status = ToolExecutionStatus.Completed
                    output = "ok"
                    error = ""
                    input = input
                    operationAction = "list" }
              ),
              null
          ) ]
      source = Native
      raw = null }

let taskMsgWithActionAndReport (action: string) (id: string) (callID: string) (report: string) : Message<obj> =
    let input =
        box (
            createObj
                [ "operation", box (createObj [ "action", box action ])
                  "ahaMoments", box report
                  "changesAndReasons", box ""
                  "gotchas", box ""
                  "lessonsAndConventions", box ""
                  "plan", box "" ]
        )

    { info = mkInfo id Assistant
      parts =
        [ ToolPart(
              "task",
              callID,
              Some(
                  { status = ToolExecutionStatus.Completed
                    output = "ok"
                    error = ""
                    input = input
                    operationAction = action }
              ),
              null
          ) ]
      source = Native
      raw = null }

let taskCreateMsg (id: string) (callID: string) : Message<obj> =
    let input =
        box (createObj [ "operation", box (createObj [ "action", box "create"; "summary", box "ignored" ]) ])

    { info = mkInfo id Assistant
      parts =
        [ ToolPart(
              "task",
              callID,
              Some(
                  { status = ToolExecutionStatus.Completed
                    output = "ok"
                    error = ""
                    input = input
                    operationAction = "create" }
              ),
              null
          ) ]
      source = Native
      raw = null }

let toolMsg (toolName: string) (id: string) (callID: string) (report: string) : Message<obj> =
    let input =
        box (
            createObj
                [ "ahaMoments", box report
                  "changesAndReasons", box ""
                  "gotchas", box ""
                  "lessonsAndConventions", box ""
                  "plan", box ""
                  "todos", box [||] ]
        )

    { info = mkInfo id Assistant
      parts = [ ToolPart(toolName, callID, Some(mkState "completed" "Todos updated." input), null) ]
      source = Native
      raw = null }

let backlogEntry (_: int) (report: string) : BacklogEntry =
    { ahaMoments = report
      changesAndReasons = ""
      gotchas = ""
      lessonsAndConventions = ""
      plan = "" }

let visibleText (messages: Message<obj> list) : string =
    messages
    |> List.collect (fun message ->
        message.parts
        |> List.choose (function
            | TextPart text -> Some text
            | ToolPart(_, _, Some state, _) -> Some state.output
            | _ -> None))
    |> String.concat "\n\n"
