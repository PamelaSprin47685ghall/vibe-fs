module Wanxiangshu.Runtime.WorkBacklogToolsCodec

open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Runtime.BacklogProjectionBuild
open Wanxiangshu.Kernel.ToolArgs
open Wanxiangshu.Runtime.DynField

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
    | None -> Error(InvalidIntent("todowrite", "todos", "required"))
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

let private decodeSelectMethodology (args: obj) : Result<string list, DomainError> =
    match strListField args "select_methodology" with
    | None -> Error(InvalidIntent("todowrite", "select_methodology", "required"))
    | Some items ->
        if items.IsEmpty then
            Error(InvalidIntent("todowrite", "select_methodology", "required"))
        else
            Ok items

let decodeTodoWriteArgs (isTask: bool) (args: obj) : Result<TodoWriteArgs * string list, DomainError> =
    let decodeReportField k =
        match strField args k with
        | None -> ("", None)
        | Some v ->
            let trimmed = v.Trim()
            (trimmed, None)

    let ahaMoments, ahaViol = decodeReportField "ahaMoments"
    let changesAndReasons, changesViol = decodeReportField "changesAndReasons"
    let gotchas, gotchasViol = decodeReportField "gotchas"
    let lessonsAndConventions, lessonsViol = decodeReportField "lessonsAndConventions"
    let plan, planViol = decodeReportField "plan"

    let violations =
        [ ahaViol; changesViol; gotchasViol; lessonsViol; planViol ] |> List.choose id

    match decodeTodos args with
    | Error e -> Error e
    | Ok todos ->
        match decodeSelectMethodology args with
        | Error e -> Error e
        | Ok methodology ->
            let decodedArgs =
                { AhaMoments = ahaMoments
                  ChangesAndReasons = changesAndReasons
                  Gotchas = gotchas
                  LessonsAndConventions = lessonsAndConventions
                  Plan = plan
                  Todos = todos
                  SelectMethodology = methodology }

            Ok(decodedArgs, violations)

let decodeTodoToolOpts (opts: obj) : Result<TodoToolOpts, DomainError> =
    match strField opts "toolCallId" with
    | Some id when not (System.String.IsNullOrWhiteSpace id) -> Ok { ToolCallId = id.Trim() }
    | _ -> Error(InvalidIntent("todowrite", "toolCallId", "required"))
