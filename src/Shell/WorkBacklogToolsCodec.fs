module Wanxiangshu.Shell.WorkBacklogToolsCodec

open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.BacklogProjectionCore
open Wanxiangshu.Shell.DynField

type TodoItem = {
    Content: string
    Status: string
    Priority: string
}

type TodoWriteArgs = {
    AhaMoments: string
    ChangesAndReasons: string
    Gotchas: string
    LessonsAndConventions: string
    Plan: string
    Todos: TodoItem array
    SelectMethodology: string list
}

type TodoToolOpts = { ToolCallId: string }

let reportMinLength = 1024

let private requireNonBlank (tool: string) (field: string) (index: int) (label: string) (value: string option) : Result<string, DomainError> =
    match value with
    | Some s when not (System.String.IsNullOrWhiteSpace s) -> Ok (s.Trim())
    | _ -> Error (InvalidIntent (tool, field, sprintf "item %d: %s required" index label))

let private decodeTodoItem (todo: obj) (index: int) : Result<TodoItem, DomainError> =
    let contentResult = requireNonBlank "todowrite" "todos" index "content" (strField todo "content")
    let statusResult = requireNonBlank "todowrite" "todos" index "status" (strField todo "status")
    let priorityResult = requireNonBlank "todowrite" "todos" index "priority" (strField todo "priority")
    contentResult
    |> Result.bind (fun content ->
        statusResult
        |> Result.bind (fun status ->
            priorityResult
            |> Result.map (fun priority ->
                { Content = content; Status = status; Priority = priority })))

let private decodeTodos (args: obj) : Result<TodoItem array, DomainError> =
    match objListField args "todos" with
    | None -> Ok [||]
    | Some items ->
        items
        |> Array.ofList
        |> Array.mapi (fun i todo -> decodeTodoItem todo i)
        |> Array.fold (fun acc r ->
            match acc, r with
            | Error e, _ -> Error e
            | Ok soFar, Error e -> Error e
            | Ok soFar, Ok item -> Ok (Array.append soFar [| item |])) (Ok [||])

let private decodeSelectMethodology (args: obj) : string list =
    match strListField args "select_methodology" with
    | None -> []
    | Some items -> items

let private requireReportField (args: obj) (field: string) : Result<string, DomainError> =
    strField args field
    |> Option.map (fun v -> v.Trim())
    |> function
        | Some v when v.Length >= reportMinLength -> Ok v
        | Some _ -> Error (InvalidIntent ("todowrite", field, sprintf "must be at least %d characters" reportMinLength))
        | None -> Error (InvalidIntent ("todowrite", field, "required"))

let decodeTodoWriteArgs (args: obj) : Result<TodoWriteArgs, DomainError> =
    let ahaResult = requireReportField args "ahaMoments"
    let changesResult = requireReportField args "changesAndReasons"
    let gotchasResult = requireReportField args "gotchas"
    let lessonsResult = requireReportField args "lessonsAndConventions"
    let planResult = requireReportField args "plan"
    let todosResult = decodeTodos args
    ahaResult
    |> Result.bind (fun ahaMoments ->
        changesResult
        |> Result.bind (fun changesAndReasons ->
            gotchasResult
            |> Result.bind (fun gotchas ->
                lessonsResult
                |> Result.bind (fun lessonsAndConventions ->
                    planResult
                    |> Result.bind (fun plan ->
                        todosResult
                        |> Result.map (fun todos ->
                            { AhaMoments = ahaMoments
                              ChangesAndReasons = changesAndReasons
                              Gotchas = gotchas
                              LessonsAndConventions = lessonsAndConventions
                              Plan = plan
                              Todos = todos
                              SelectMethodology = decodeSelectMethodology args }))))))

let decodeTodoToolOpts (opts: obj) : Result<TodoToolOpts, DomainError> =
    match strField opts "toolCallId" with
    | Some id when not (System.String.IsNullOrWhiteSpace id) -> Ok { ToolCallId = id.Trim() }
    | _ -> Error (InvalidIntent ("todowrite", "toolCallId", "required"))
