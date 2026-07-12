module Wanxiangshu.Shell.WorkBacklogToolsCodec

open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.BacklogProjectionCore
open Wanxiangshu.Kernel.ToolArgs
open Wanxiangshu.Shell.DynField

type TodoItem =
    { Content: string
      Status: TodoItemStatus
      Priority: TodoItemPriority }

type TodoWriteArgs =
    { AhaMoments: string
      ChangesAndReasons: string
      Gotchas: string
      LessonsAndConventions: string
      Plan: string
      Todos: TodoItem array
      SelectMethodology: string list }

type TodoToolOpts = { ToolCallId: string }

let reportMinLength = 1024

let private requireNonBlank
    (tool: string)
    (field: string)
    (index: int)
    (label: string)
    (value: string option)
    : Result<string, DomainError> =
    match value with
    | Some s when not (System.String.IsNullOrWhiteSpace s) -> Ok(s.Trim())
    | _ -> Error(InvalidIntent(tool, field, sprintf "item %d: %s required" index label))

let parseTodoItemStatus (s: string) : Result<TodoItemStatus, DomainError> =
    match s.Trim().ToLowerInvariant() with
    | "pending" -> Ok Todo
    | "in_progress"
    | "inprogress" -> Ok InProgress
    | "completed" -> Ok Completed
    | "cancelled"
    | "canceled" -> Ok Cancelled
    | other -> Error(InvalidIntent("todowrite", "todos", sprintf "unknown status: %s" other))

let parseTodoItemPriority (s: string) : Result<TodoItemPriority, DomainError> =
    match s.Trim().ToLowerInvariant() with
    | "low" -> Ok Low
    | "medium" -> Ok Medium
    | "high" -> Ok High
    | other -> Error(InvalidIntent("todowrite", "todos", sprintf "unknown priority: %s" other))

let private decodeTodoItem (todo: obj) (index: int) : Result<TodoItem, DomainError> =
    let contentResult =
        requireNonBlank "todowrite" "todos" index "content" (strField todo "content")

    let statusRawResult =
        requireNonBlank "todowrite" "todos" index "status" (strField todo "status")

    let priorityRawResult =
        requireNonBlank "todowrite" "todos" index "priority" (strField todo "priority")

    contentResult
    |> Result.bind (fun content ->
        statusRawResult
        |> Result.bind parseTodoItemStatus
        |> Result.bind (fun status ->
            priorityRawResult
            |> Result.bind parseTodoItemPriority
            |> Result.map (fun priority ->
                { Content = content
                  Status = status
                  Priority = priority })))

let private decodeTodos (args: obj) : Result<TodoItem array, DomainError> =
    match objListField args "todos" with
    | None -> Ok [||]
    | Some items ->
        items
        |> Array.ofList
        |> Array.mapi (fun i todo -> decodeTodoItem todo i)
        |> Array.fold
            (fun acc r ->
                match acc, r with
                | Error e, _ -> Error e
                | Ok soFar, Error e -> Error e
                | Ok soFar, Ok item -> Ok(Array.append soFar [| item |]))
            (Ok [||])

let private decodeSelectMethodology (args: obj) : string list =
    match strListField args "select_methodology" with
    | None -> []
    | Some items -> items

let private requireReportField (args: obj) (field: string) : Result<string, DomainError> =
    strField args field
    |> Option.map (fun v -> v.Trim())
    |> function
        | Some v when v.Length >= reportMinLength -> Ok v
        | Some _ -> Error(InvalidIntent("todowrite", field, sprintf "must be at least %d characters" reportMinLength))
        | None -> Error(InvalidIntent("todowrite", field, "required"))

let decodeTodoWriteArgs (args: obj) : Result<TodoWriteArgs, DomainError> =
    let isTask = Dyn.isNullish (Dyn.get args "ahaMoments")

    let getReportField k =
        if isTask then Ok "" else requireReportField args k

    let ahaResult = getReportField "ahaMoments"
    let changesResult = getReportField "changesAndReasons"
    let gotchasResult = getReportField "gotchas"
    let lessonsResult = getReportField "lessonsAndConventions"
    let planResult = getReportField "plan"
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
    | _ -> Error(InvalidIntent("todowrite", "toolCallId", "required"))
