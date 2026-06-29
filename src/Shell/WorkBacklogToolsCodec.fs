module Wanxiangshu.Shell.WorkBacklogToolsCodec

open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.BacklogProjectionCore
open Wanxiangshu.Shell.Dyn
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

let private requireReportField (args: obj) (field: string) : Result<string, DomainError> =
    match strField args field with
    | None -> Error (InvalidIntent ("todowrite", field, "required"))
    | Some v when System.String.IsNullOrWhiteSpace v ->
        Error (InvalidIntent ("todowrite", field, "required"))
    | Some v ->
        let trimmed = v.Trim()
        if trimmed.Length < reportMinLength then
            Error (InvalidIntent ("todowrite", field, sprintf "must be at least %d characters" reportMinLength))
        else Ok trimmed

let decodeTodoWriteArgs (args: obj) : Result<TodoWriteArgs, DomainError> =
    match requireReportField args "ahaMoments" with
    | Error e -> Error e
    | Ok ahaMoments ->
        match requireReportField args "changesAndReasons" with
        | Error e -> Error e
        | Ok changesAndReasons ->
            match requireReportField args "gotchas" with
            | Error e -> Error e
            | Ok gotchas ->
                match requireReportField args "lessonsAndConventions" with
                | Error e -> Error e
                | Ok lessonsAndConventions ->
                    match requireReportField args "plan" with
                    | Error e -> Error e
                    | Ok plan ->
                        match decodeTodos args with
                        | Error e -> Error e
                        | Ok todos ->
                            Ok {
                                AhaMoments = ahaMoments
                                ChangesAndReasons = changesAndReasons
                                Gotchas = gotchas
                                LessonsAndConventions = lessonsAndConventions
                                Plan = plan
                                Todos = todos
                                SelectMethodology = decodeSelectMethodology args
                            }

let decodeTodoToolOpts (opts: obj) : Result<TodoToolOpts, DomainError> =
    match strField opts "toolCallId" with
    | Some id when not (System.String.IsNullOrWhiteSpace id) -> Ok { ToolCallId = id.Trim() }
    | _ -> Error (InvalidIntent ("todowrite", "toolCallId", "required"))