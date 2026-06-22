module VibeFs.Opencode.MimoTodoTool

open System
open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.MagicTodo
open VibeFs.Opencode.ToolSchema

[<Import("createRequire", "node:module")>]
let private createRequire (pathOrUrl: string) : obj = jsNative

[<Import("pathToFileURL", "node:url")>]
let private pathToFileURL (path: string) : obj = jsNative

[<Global("import.meta")>]
let private importMeta : obj = jsNative

[<Global("process")>]
let private nodeProcess : obj = jsNative

type private TodoItem =
    { content: string
      status: string }

let private resolveStr (text: string) : JS.Promise<string> = Promise.lift text

let private importMetaUrl = string importMeta?url

let private tryResolveHostModulePath (specifier: string) : string option =
    let tryResolveFrom (basePathOrUrl: string) =
        if String.IsNullOrWhiteSpace basePathOrUrl then None
        else
            try
                let resolver = createRequire basePathOrUrl
                let resolved = Dyn.call1 (Dyn.get resolver "resolve") (box specifier) |> string
                if String.IsNullOrWhiteSpace resolved then None else Some resolved
            with _ ->
                None

    let argvBase =
        let argv = Dyn.get nodeProcess "argv"
        if Dyn.isArray argv then
            let values = argv :?> obj array
            if values.Length > 1 then string values.[1] else ""
        else
            ""

    [ argvBase; importMetaUrl ]
    |> List.tryPick tryResolveFrom

let private importHostModule (specifier: string) : JS.Promise<obj> =
    promise {
        match tryResolveHostModulePath specifier with
        | Some resolvedPath ->
            let href = string (Dyn.get (pathToFileURL resolvedPath) "href")
            return! importDynamic<obj> href
        | None ->
            return raise (Exception $"Could not resolve host module {specifier} from the running MiMo process")
    }

let private toTodoPayload (todos: TodoItem list) : obj array =
    todos
    |> List.map (fun todo ->
        createObj [
            "content", box todo.content
            "status", box todo.status
        ])
    |> List.toArray

let private decodeTodoItems (args: obj) : Result<TodoItem list, string> =
    let rawTodos = Dyn.get args "todos"
    if Dyn.isNullish rawTodos || not (Dyn.isArray rawTodos) then
        Error "task requires a todos array"
    else
        let todos = rawTodos :?> obj array
        let parsed =
            todos
            |> Array.toList
            |> List.mapi (fun index item ->
                let content = Dyn.str item "content" |> fun value -> value.Trim()
                let status = Dyn.str item "status" |> fun value -> value.Trim()
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

let private trySyncViaInjectedHook (pluginCtx: obj) (sessionID: string) (todos: obj array) : JS.Promise<string option> =
    promise {
        let sync = Dyn.get pluginCtx "__mimoTodoSyncForTesting"
        if Dyn.isNullish sync then
            return None
        else
            try
                let! warning = Dyn.call2 sync (box sessionID) (box todos) |> unbox<JS.Promise<obj>>
                if Dyn.isNullish warning then return None else return Some(string warning)
            with ex ->
                return Some $"Warning: failed to sync MiMo sidebar todos: {ex.Message}"
    }

let private trySyncViaHostRuntime (pluginCtx: obj) (sessionID: string) (todos: obj array) : JS.Promise<string option> =
    promise {
        if Dyn.isNullish (Dyn.get pluginCtx "client") then
            return None
        else
            try
                let! appRuntimeModule = importHostModule "@mimo-ai/cli/effect/app-runtime"
                let! todoModule = importHostModule "@mimo-ai/cli/session/todo"
                let appRuntime = Dyn.get appRuntimeModule "AppRuntime"
                let todoService = Dyn.get todoModule "Service"
                let effect =
                    Dyn.call1
                        (Dyn.get todoService "use")
                        (box (fun (svc: obj) ->
                            svc?update(
                                createObj [
                                    "sessionID", box sessionID
                                    "todos", box todos
                                ]
                            )))
                let! _ = appRuntime?runPromise(effect) |> unbox<JS.Promise<obj>>
                return None
            with ex ->
                return Some $"Warning: failed to sync MiMo sidebar todos: {ex.Message}"
    }

let private syncTodoTable (pluginCtx: obj) (sessionID: string) (todos: TodoItem list) : JS.Promise<string option> =
    promise {
        let todoPayload = toTodoPayload todos
        let! injectedWarning = trySyncViaInjectedHook pluginCtx sessionID todoPayload
        match injectedWarning with
        | Some _ -> return injectedWarning
        | None -> return! trySyncViaHostRuntime pluginCtx sessionID todoPayload
    }

let mimoTodoTool (pluginCtx: obj) : obj =
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
            let sessionID = Dyn.str context "sessionID" |> fun value -> value.Trim()
            let report = Dyn.str args "completedWorkReport" |> fun value -> value.Trim()
            if sessionID = "" then resolveStr "task requires sessionID"
            elif report = "" then resolveStr "task requires completedWorkReport"
            else
                match decodeTodoItems args with
                | Error error -> resolveStr error
                | Ok todos ->
                    promise {
                        let! warning = syncTodoTable pluginCtx sessionID todos
                        return
                            match warning with
                            | Some text when text <> "" -> "Todos updated.\n\n" + text
                            | _ -> "Todos updated."
                    })
