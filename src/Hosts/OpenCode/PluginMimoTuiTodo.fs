module Wanxiangshu.Hosts.Opencode.PluginMimoTuiTodo

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn

let private asObjArray (value: obj) : obj array =
    if Dyn.isNullish value || not (Dyn.isArray value) then
        [||]
    else
        unbox<obj array> value

let private tryRecoverTodosFromTaskPart (part: obj) : obj array option =
    if Dyn.str part "type" <> "tool" || Dyn.str part "tool" <> "task" then
        None
    else
        let state = Dyn.get part "state"
        let status = Dyn.str state "status"

        if status = "error" then
            None
        else
            let input = Dyn.get state "input"
            let todos = Dyn.get input "todos"

            if Dyn.isArray todos then
                Some(
                    unbox<obj array> todos
                    |> Array.map (fun todo ->
                        box
                            {| content = Dyn.str todo "content"
                               status = Dyn.str todo "status" |})
                )
            else
                None

let private tryRecoverTodosFromMessages (state: obj) (sessionID: string) : obj array option =
    let sessionState = Dyn.get state "session"
    let messages = Dyn.callMethod1 sessionState "messages" (box sessionID) |> asObjArray

    let partsOf messageID =
        Dyn.callMethod1 state "part" (box messageID) |> asObjArray

    messages
    |> Array.rev
    |> Array.tryPick (fun message ->
        partsOf (Dyn.str message "id")
        |> Array.rev
        |> Array.tryPick tryRecoverTodosFromTaskPart)

let installTodoFallback (api: obj) : unit =
    let state = Dyn.get api "state"
    let sessionState = Dyn.get state "session"
    let originalTodo = Dyn.get sessionState "todo"

    let fallback =
        System.Func<string, obj>(fun sessionID ->
            let existing = Dyn.callWithThis1 originalTodo sessionState (box sessionID)
            let existingTodos = asObjArray existing

            if existingTodos.Length > 0 then
                box existingTodos
            else
                match tryRecoverTodosFromMessages state sessionID with
                | Some todos -> box todos
                | None -> existing)

    sessionState?("todo") <- box fallback

    api?lifecycle?onDispose (System.Func<unit>(fun () -> sessionState?("todo") <- originalTodo))
    |> ignore
