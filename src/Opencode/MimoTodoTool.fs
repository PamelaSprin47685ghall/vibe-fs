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

[<Import("dirname", "node:path")>]
let private dirname (path: string) : string = jsNative

[<Import("join", "node:path")>]
let private join (left: string) (right: string) : string = jsNative

[<Import("pathToFileURL", "node:url")>]
let private pathToFileURL (path: string) : obj = jsNative

[<Import("fileURLToPath", "node:url")>]
let private fileURLToPath (url: string) : string = jsNative

[<Import("existsSync", "node:fs")>]
let private existsSync (path: string) : bool = jsNative

[<Import("readFileSync", "node:fs")>]
let private readFileSync (path: string) (encoding: string) : string = jsNative

[<Global("import.meta")>]
let private importMeta : obj = jsNative

[<Global("process")>]
let private nodeProcess : obj = jsNative

type private TodoItem =
    { content: string
      status: string }

let private resolveStr (text: string) : JS.Promise<string> = Promise.lift text

let private importMetaUrl = string importMeta?url

let private tryResolveFromHostPackage (relativePath: string) : string option =
    try
        let hostRequire = createRequire importMetaUrl
        let packageJsonPath = Dyn.call1 (Dyn.get hostRequire "resolve") (box "@mimo-ai/cli/package.json") |> string
        Some(join (dirname packageJsonPath) relativePath)
    with _ ->
        None

let private tryFindCliRootFrom (startPath: string) : string option =
    let rec loop (currentPath: string) =
        if String.IsNullOrWhiteSpace currentPath then None
        else
            let candidate = join currentPath "package.json"
            if existsSync candidate then
                try
                    let pkg = JS.JSON.parse(readFileSync candidate "utf8")
                    if Dyn.str pkg "name" = "@mimo-ai/cli" then Some currentPath
                    else
                        let parent = dirname currentPath
                        if parent = currentPath then None else loop parent
                with _ ->
                    let parent = dirname currentPath
                    if parent = currentPath then None else loop parent
            else
                let parent = dirname currentPath
                if parent = currentPath then None else loop parent

    if String.IsNullOrWhiteSpace startPath then None
    else loop (if startPath.StartsWith("file://") then dirname (fileURLToPath startPath) else dirname startPath)

let private tryResolveCliModulePath (relativePath: string) : string option =
    let argvBase =
        let argv = Dyn.get nodeProcess "argv"
        if Dyn.isArray argv then
            let values = argv :?> obj array
            if values.Length > 1 then string values.[1] else ""
        else
            ""

    tryResolveFromHostPackage relativePath
    |> Option.orElseWith (fun () ->
        [ argvBase; importMetaUrl ]
        |> List.tryPick tryFindCliRootFrom
        |> Option.map (fun cliRoot -> join cliRoot relativePath))

let private importCliModule (relativePath: string) : JS.Promise<obj> =
    promise {
        match tryResolveCliModulePath relativePath with
        | Some resolvedPath ->
            let href = string (Dyn.get (pathToFileURL resolvedPath) "href")
            return! importDynamic<obj> href
        | None ->
            return raise (Exception $"Could not resolve MiMo CLI root from the running process for {relativePath}")
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
                let! appRuntimeModule = importCliModule "src/effect/app-runtime.ts"
                let! todoModule = importCliModule "src/session/todo.ts"
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
