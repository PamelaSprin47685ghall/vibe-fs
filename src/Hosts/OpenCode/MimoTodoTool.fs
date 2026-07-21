module Wanxiangshu.Hosts.Opencode.MimoTodoTool

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Runtime.ToolOutputInfo
open Wanxiangshu.Hosts.Opencode.ToolSchema
open Wanxiangshu.Runtime.Dyn

type private TodoItem = { content: string; status: string }

let private resolveStr (text: string) : JS.Promise<string> = Promise.lift text

let private decodeMethodologies (args: obj) : string list =
    let raw = get args "select_methodology"

    if isNullish raw || not (isArray raw) then
        []
    else
        raw :?> obj array |> Array.map string |> Array.toList

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

                if content = "" then
                    Error $"task todos[{index}] requires content"
                elif status = "" then
                    Error $"task todos[{index}] requires status"
                else
                    Ok { content = content; status = status })

        let errors =
            parsed
            |> List.choose (fun item ->
                match item with
                | Error error -> Some error
                | Ok _ -> None)

        if not (List.isEmpty errors) then
            Error errors.Head
        else
            Ok(
                parsed
                |> List.choose (fun item ->
                    match item with
                    | Ok todo -> Some todo
                    | Error _ -> None)
            )

let mimoTodoTool (_pluginCtx: obj) : obj =
    let todoItem =
        obj (
            createObj
                [ "content", strReq "todo content"
                  "status", strReq "todo status"
                  "priority", strOpt "todo priority" ]
        )

    let enumVals =
        Wanxiangshu.Kernel.Methodology.Registry.enumValues.Value |> List.toArray

    define
        ("Mimo todo tool")
        (box
            {| todos = ToolSchema.call1 (arr todoItem) "describe" (box "todos list")
               select_methodology =
                enumArrayMin enumVals 1 Wanxiangshu.Kernel.Methodology.Api.selectMethodologyFieldDescription |})
        (fun args context ->
            let sessionID = str context "sessionID" |> fun value -> value.Trim()
            let methodologies = decodeMethodologies args

            if sessionID = "" then
                resolveStr "task requires sessionID"
            elif methodologies.IsEmpty then
                resolveStr "task requires select_methodology"
            else
                match decodeTodoItems args with
                | Error error -> resolveStr error
                | Ok _ -> resolveStr (todoWriteOutput methodologies))
