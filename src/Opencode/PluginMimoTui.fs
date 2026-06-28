module Wanxiangshu.Opencode.PluginMimoTui

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Shell
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.Clock

let private dateNow () : float =
    float (getTimestampMs())

let private relTime (updated: float) : string =
    let diff = dateNow () - updated
    if diff < 1000.0 then "active now"
    else
        let sec = diff / 1000.0
        if sec < 60.0 then $"{int sec}s ago"
        elif sec < 3600.0 then $"{int (sec / 60.0)}m ago"
        else $"{int (sec / 3600.0)}h ago"

let private numField (o: obj) (parent: string) (child: string) : float =
    let p = Dyn.get o parent
    if Dyn.isNullish p then 0.0
    else
        let v = Dyn.get p child
        if Dyn.isNullish v then 0.0 else unbox<float> v

let private checkpointWriterTitlePrefix = "checkpoint-writer:"

let private isVisibleSubagent (child: obj) : bool =
    let title = Dyn.str child "title"
    not (title.StartsWith checkpointWriterTitlePrefix)

let private childLabel (child: obj) : string =
    let title = Dyn.str child "title"
    if title = "" then Dyn.str child "id" else title

let private isCheckpointWriterSession (child: obj) : bool =
    (childLabel child).StartsWith("checkpoint-writer:")

let private toOption (child: obj) : obj =
    let id = Dyn.str child "id"
    let label = childLabel child
    box {| title = label; value = id; description = relTime (numField child "time" "updated") |}

let private awaitObj (p: obj) : JS.Promise<obj> = unbox<JS.Promise<obj>> p

let private asObjArray (value: obj) : obj array =
    if Dyn.isNullish value || not (Dyn.isArray value) then [||] else unbox<obj array> value

let private tryRecoverTodosFromTaskPart (part: obj) : obj array option =
    if Dyn.str part "type" <> "tool" || Dyn.str part "tool" <> "task" then None
    else
        let state = Dyn.get part "state"
        let status = Dyn.str state "status"
        if status = "error" then None
        else
            let input = Dyn.get state "input"
            let todos = Dyn.get input "todos"
            if Dyn.isArray todos then
                Some (
                    unbox<obj array> todos
                    |> Array.map (fun todo ->
                        box {| content = Dyn.str todo "content"
                               status = Dyn.str todo "status" |})
                )
            else None

let private tryRecoverTodosFromMessages (state: obj) (sessionID: string) : obj array option =
    let sessionState = Dyn.get state "session"
    let messages = Dyn.call1 (Dyn.get sessionState "messages") (box sessionID) |> asObjArray
    let partsOf messageID = Dyn.call1 (Dyn.get state "part") (box messageID) |> asObjArray
    messages
    |> Array.rev
    |> Array.tryPick (fun message ->
        partsOf (Dyn.str message "id")
        |> Array.rev
        |> Array.tryPick tryRecoverTodosFromTaskPart)

let private installTodoFallback (api: obj) : unit =
    let state = Dyn.get api "state"
    let sessionState = Dyn.get state "session"
    let originalTodo = Dyn.get sessionState "todo"
    let fallback =
        System.Func<string, obj>(fun sessionID ->
            let existing = Dyn.call1 originalTodo (box sessionID)
            let existingTodos = asObjArray existing
            if existingTodos.Length > 0 then box existingTodos
            else
                match tryRecoverTodosFromMessages state sessionID with
                | Some todos -> box todos
                | None -> existing)
    sessionState?("todo") <- box fallback
    api?lifecycle?onDispose(System.Func<unit>(fun () -> sessionState?("todo") <- originalTodo)) |> ignore

let private fetchVisibleChildren (api: obj) (sessionID: string) (directory: obj) : JS.Promise<obj[]> =
    promise {
        let! sessRes = awaitObj (api?client?session?get(box {| sessionID = sessionID; directory = directory |}))
        let sess = Dyn.get sessRes "data"
        let parentID = if Dyn.isNullish sess then "" else Dyn.str sess "parentID"
        let rootID = if parentID = "" then sessionID else parentID
        let! childRes = awaitObj (api?client?session?children(box {| sessionID = rootID; directory = directory |}))
        let data = Dyn.get childRes "data"
        return
            if Dyn.isNullish data then [||]
            else unbox<obj[]> data |> Array.filter (fun c -> isVisibleSubagent c && not (isCheckpointWriterSession c))
    }

let private showSubagentDialog (api: obj) (sessionID: string) (visible: obj[]) : unit =
    let options =
        visible
        |> Array.sortBy (fun c -> numField c "time" "created")
        |> Array.map toOption
    let onSelect =
        System.Func<obj, unit>(fun opt ->
            api?route?navigate("session", box {| sessionID = Dyn.str opt "value" |}) |> ignore
            api?ui?dialog?clear() |> ignore)
    let props =
        box {| title = "Subagents"
               placeholder = "Switch to subagent"
               current = sessionID
               options = options
               onSelect = onSelect |}
    api?ui?dialog?replace(System.Func<obj>(fun () -> api?ui?DialogSelect(props))) |> ignore

let private toast (api: obj) (variant: string) (message: string) : unit =
    api?ui?toast(box {| message = message; variant = variant |}) |> ignore

/// Fetch every subagent that shares this session's top-level parent and present
/// them in a switchable dialog. Child sessions always carry parentID == the
/// top-level manager session, so resolving the parent first lets the dialog work
/// identically whether invoked from the manager or from inside a subagent.
let private openSubagents (api: obj) : unit =
    let route = api?route?current
    if Dyn.str route "name" <> "session" then
        toast api "info" "Open a session to view its subagents"
    else
        let sessionID = Dyn.str (Dyn.get route "params") "sessionID"
        let directory = api?state?path?directory
        promise {
            try
                let! visible = fetchVisibleChildren api sessionID directory
                if visible.Length = 0 then
                    toast api "info" "No subagents running yet"
                else
                    showSubagentDialog api sessionID visible
            with _ ->
                toast api "error" "Failed to load subagents"
        }
        |> Promise.start

let private registerCommands (api: obj) : unit =
    api?command?register(System.Func<obj>(fun () ->
        let inSession = Dyn.str (api?route?current) "name" = "session"
        box [| box {| title = "Subagents"
                      value = "vibe.subagents"
                      description = "List and switch to running subagents"
                      category = "vibe-fs"
                      slash = box {| name = "subagents" |}
                      hidden = not inSession
                      onSelect = System.Func<unit>(fun () -> openSubagents api) |} |]
    )) |> ignore

let private tuiImpl (api: obj) : JS.Promise<unit> =
    promise {
        installTodoFallback api
        registerCommands api
    }

[<ExportDefault>]
let plugin = box {| id = "vibe-fs-mimo-tui"; tui = tuiImpl |}
