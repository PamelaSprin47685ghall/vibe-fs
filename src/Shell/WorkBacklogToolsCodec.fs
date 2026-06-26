module Wanxiangshu.Shell.WorkBacklogToolsCodec

open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.DynField

type TodoItem = {
    Content: string
    Status: string
    Priority: string
}

type TodoWriteArgs = {
    CompletedWorkReport: string
    Todos: TodoItem array
    SelectMethodology: string list
}

type TodoToolOpts = { ToolCallId: string }

let private requireNonBlank (tool: string) (field: string) (index: int) (label: string) (value: string option) : Result<string, DomainError> =
    match value with
    | Some s when not (System.String.IsNullOrWhiteSpace s) -> Ok (s.Trim())
    | _ -> Error (InvalidIntent (tool, field, sprintf "item %d: %s required" index label))

let private decodeTodoItem (todo: obj) (index: int) : Result<TodoItem, DomainError> =
    match requireNonBlank "todowrite" "todos" index "content" (strField todo "content") with
    | Error e -> Error e
    | Ok content ->
        match requireNonBlank "todowrite" "todos" index "status" (strField todo "status") with
        | Error e -> Error e
        | Ok status ->
            match requireNonBlank "todowrite" "todos" index "priority" (strField todo "priority") with
            | Error e -> Error e
            | Ok priority -> Ok { Content = content; Status = status; Priority = priority }

let private decodeTodos (args: obj) : Result<TodoItem array, DomainError> =
    let raw = Dyn.get args "todos"
    if Dyn.isNullish raw || not (Dyn.isArray raw) then Ok [||]
    else
        let items = raw :?> obj array
        items
        |> Array.mapi (fun i todo -> decodeTodoItem todo i)
        |> Array.fold (fun acc r ->
            match acc, r with
            | Error e, _ -> Error e
            | Ok soFar, Error e -> Error e
            | Ok soFar, Ok item -> Ok (Array.append soFar [| item |])) (Ok [||])

let private decodeSelectMethodology (args: obj) : string list =
    let raw = Dyn.get args "select_methodology"
    if Dyn.isNullish raw || not (Dyn.isArray raw) then []
    else raw :?> obj array |> Array.map string |> Array.toList

let decodeTodoWriteArgs (args: obj) : Result<TodoWriteArgs, DomainError> =
    match strField args "completedWorkReport" with
    | None -> Error (InvalidIntent ("todowrite", "completedWorkReport", "required"))
    | Some report when System.String.IsNullOrWhiteSpace report ->
        Error (InvalidIntent ("todowrite", "completedWorkReport", "required"))
    | Some report ->
        match decodeTodos args with
        | Error e -> Error e
        | Ok todos ->
            Ok {
                CompletedWorkReport = report.Trim()
                Todos = todos
                SelectMethodology = decodeSelectMethodology args
            }

let decodeTodoToolOpts (opts: obj) : Result<TodoToolOpts, DomainError> =
    match strField opts "toolCallId" with
    | Some id when not (System.String.IsNullOrWhiteSpace id) -> Ok { ToolCallId = id.Trim() }
    | _ -> Error (InvalidIntent ("todowrite", "toolCallId", "required"))