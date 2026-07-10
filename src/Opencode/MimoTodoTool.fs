module Wanxiangshu.Opencode.MimoTodoTool

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Opencode.ToolSchema
open Wanxiangshu.Shell.Dyn

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
                [ "content", strReq todoContentDesc
                  "status", strReq todoStatusDesc
                  "priority", strReq todoPriorityDesc ]
        )

    let enumVals = Wanxiangshu.Methodology.Registry.enumValues.Value |> List.toArray

    define
        (toolDescriptionFor Mimocode)
        (box
            {| todos = ToolSchema.call1 (arr todoItem) "describe" (box todosDesc)
               ahaMoments = strMin 1024 ahaMomentsDesc
               changesAndReasons = strMin 1024 changesAndReasonsDesc
               gotchas = strMin 1024 gotchasDesc
               lessonsAndConventions = strMin 1024 lessonsAndConventionsDesc
               plan = strMin 1024 planDesc
               select_methodology =
                enumArrayMin enumVals 1 Wanxiangshu.Kernel.Methodology.selectMethodologyFieldDescription |})
        (fun args context ->
            let sessionID = str context "sessionID" |> fun value -> value.Trim()
            let methodologies = decodeMethodologies args
            let ahaMoments = (str args "ahaMoments").Trim()
            let changesAndReasons = (str args "changesAndReasons").Trim()
            let gotchas = (str args "gotchas").Trim()
            let lessonsAndConventions = (str args "lessonsAndConventions").Trim()
            let plan = (str args "plan").Trim()

            if sessionID = "" then
                resolveStr "task requires sessionID"
            elif ahaMoments.Length < 1024 then
                resolveStr "task requires ahaMoments (min 1024 chars)"
            elif changesAndReasons.Length < 1024 then
                resolveStr "task requires changesAndReasons (min 1024 chars)"
            elif gotchas.Length < 1024 then
                resolveStr "task requires gotchas (min 1024 chars)"
            elif lessonsAndConventions.Length < 1024 then
                resolveStr "task requires lessonsAndConventions (min 1024 chars)"
            elif plan.Length < 1024 then
                resolveStr "task requires plan (min 1024 chars)"
            elif methodologies.IsEmpty then
                resolveStr "task requires select_methodology"
            else
                match decodeTodoItems args with
                | Error error -> resolveStr error
                | Ok _ -> resolveStr (todoWriteOutput methodologies))
