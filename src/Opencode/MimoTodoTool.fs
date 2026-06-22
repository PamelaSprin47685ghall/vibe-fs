module VibeFs.Opencode.MimoTodoTool

open System
open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.MagicTodo
open VibeFs.Opencode.ToolSchema

type private TodoItem =
    { content: string
      status: string }

let private resolveStr (text: string) : JS.Promise<string> = Promise.lift text

let private decodeTodoItems (args: obj) : Result<TodoItem list, string> =
    let rawTodos = get args "todos"
    if isNullish rawTodos || not (isArray rawTodos) then
        Error "task requires a todos array"
    else
        let todos = unbox<obj array> rawTodos
        let parsed =
            todos
            |> Array.toList
            |> List.mapi (fun index item ->
                let content = str item "content" |> fun value -> value.Trim()
                let status = str item "status" |> fun value -> value.Trim()
                if content = "" then Error $"task todos[{index}] requires content"
                elif status = "" then Error $"task todos[{index}] requires status"
                else Ok { content = content; status = status })
        let errors =
            parsed
            |> List.choose (fun item ->
                match item with
                | Error error -> Some error
                | Ok _ -> None)
        if not (List.isEmpty errors) then Error errors.Head
        else
            Ok (
                parsed
                |> List.choose (fun item ->
                    match item with
                    | Ok todo -> Some todo
                    | Error _ -> None)
            )

let mimoTodoTool (_pluginCtx: obj) : obj =
    let todoItem =
        obj (createObj [
            "content", strReq todoContentDesc
            "status", strReq todoStatusDesc
            "priority", strReq todoPriorityDesc
        ])
    define
        (toolDescriptionFor Mimocode)
        (box {| todos = call1 (arr todoItem) "describe" (box todosDesc)
                completedWorkReport = strReq reportDesc |})
        (fun args context ->
            let sessionID = str context "sessionID" |> fun value -> value.Trim()
            let report = str args "completedWorkReport" |> fun value -> value.Trim()
            if sessionID = "" then resolveStr "task requires sessionID"
            elif report = "" then resolveStr "task requires completedWorkReport"
            else
                match decodeTodoItems args with
                | Error error -> resolveStr error
                | Ok _ -> resolveStr "Todos updated.")
