module VibeFs.Tests.IntegrationEventTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Kernel.Dyn
open VibeFs.Mux.Plugin
open VibeFs.Opencode.Plugin

let eventHookSpec (reg: obj) = promise {
    let hook = get reg "eventHook"
    check "eventHook.length === 2" (unbox<int> (hook?length) = 2)
    let ehResult = hook $ (createObj [ "type", box "stream-abort"; "workspaceId", box "test-ws" ], null)
    check "eventHook returns Promise" (not (isNullish (get ehResult "then")))
    do! unbox<JS.Promise<unit>> ehResult
}

let repeatedTodoNudgeSpec (reg: obj) = promise {
    let nudges = ResizeArray<string>()
    let helpers todoList =
        createObj [
            "getTodos", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                (promise { return box (todoList |> List.toArray) })))
            "nudge", box (System.Func<obj, obj, JS.Promise<bool>>(fun _ws msg ->
                (promise { nudges.Add(string msg); return true })))
        ]
    let hook = get reg "eventHook"
    let streamEnd ws parts =
        createObj [ "type", box "stream-end"; "workspaceId", box ws
                    "properties", box (createObj [ "parts", box parts ]) ]
    let textPart t = box {| ``type`` = "text"; text = t |}
    do! hook $ (streamEnd "repeat-ws" [| textPart "first" |], helpers ["pending"]) |> unbox<JS.Promise<unit>>
    do! hook $ (streamEnd "repeat-ws" [| textPart "second" |], helpers ["pending"]) |> unbox<JS.Promise<unit>>
    check "repeated todo nudge suppressed" (nudges.Count = 1)
    do! hook $ (streamEnd "repeat-ws" [| textPart "cleared" |], helpers []) |> unbox<JS.Promise<unit>>
    do! hook $ (streamEnd "repeat-ws" [| textPart "reopened" |], helpers ["pending"]) |> unbox<JS.Promise<unit>>
    check "todo nudge re-allowed after clear" (nudges.Count = 2)
}

let syntaxWrapperSpec (reg: obj) = promise {
    let wrappers = unbox<obj[]> (get reg "wrappers")
    let sw = wrappers |> Array.find (fun w -> str w "targetTool" = "file_edit_replace_string")
    check "syntax wrapper exists" (not (isNullish sw))
    let mockEdit = createObj [ "execute", box (System.Func<obj, JS.Promise<string>>(fun _ ->
        (promise { return "File written" }))) ]
    let wrapped = (get sw "wrapper") $ (mockEdit, createObj [ "cwd", box "/tmp" ])
    let! _ = (get wrapped "execute") $ (createObj [ "file_path", box "nonexistent.js" ]) |> unbox<JS.Promise<string>>
    check "syntax wrapper returns result" true
}

let todoWriteWrapperSpec (reg: obj) = promise {
    let wrappers = unbox<obj[]> (get reg "wrappers")
    let tw = wrappers |> Array.find (fun w -> str w "targetTool" = "todo_write")
    check "todo_write wrapper exists" (not (isNullish tw))
    let mockTodo = createObj [ "execute", box (System.Func<obj, JS.Promise<string>>(fun _ ->
        (promise { return "Todos updated" }))) ]
    let wrapped = (get tw "wrapper") $ (mockTodo, createObj [])
    let! result = (get wrapped "execute") $ (createObj []) |> unbox<JS.Promise<string>>
    check "todo_write wrapper appends reverie nudge" (result.Contains "Think thrice")
}

let toolExecuteAfterSpec (p: obj) = promise {
    let output = createObj [ "output", box "Todos updated" ]
    do! (get p "tool.execute.after") $ (createObj [ "tool", box "todowrite"; "sessionID", box "test-ws"; "callID", box "todo-1" ], output) |> unbox<JS.Promise<unit>>
    check "tool.execute.after appends reverie nudge" ((unbox<string> (get output "output")).Contains "Think thrice")
}

let abortedRetrySpec () = promise {
    let promptCalls = ResizeArray<obj>()
    let mkClient () =
        createObj [ "session", box (createObj [
            "todo", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                (promise { return box {| data = [| box {| id = "todo-1"; content = "task"; status = "in_progress" |} |] |} })))
            "messages", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                (promise { return box {| data = [| box {|
                    info = box {| role = "assistant"; agent = "manager"; finish = "stop"; time = box {| completed = 1 |} |}
                    parts = [| box {| ``type`` = "text"; text = "working" |} |]
                |} |] |} })))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg ->
                (promise { promptCalls.Add(arg) })))
        ]) ]
    let! workspaceDir = mkdtempAsync "aborted-retry-"
    let! p = plugin (box {| directory = workspaceDir; client = mkClient () |})
    let eventHook = get p "event"
    let mkEvent typ props =
        box {| event = box {| ``type`` = typ; properties = props |} |}
    do! eventHook $ (mkEvent "session.next.step.failed" (box {| sessionID = "resume-ws"; error = box {| ``type`` = "unknown"; message = "Aborted" |} |})) |> unbox<JS.Promise<unit>>
    do! eventHook $ (mkEvent "session.idle" (box {| sessionID = "resume-ws" |})) |> unbox<JS.Promise<unit>>
    do! Promise.sleep 0
    check "aborted retry does not nudge before new prompt" (promptCalls.Count = 0)
    do! eventHook $ (mkEvent "session.next.prompted" (box {| sessionID = "resume-ws"; prompt = box {| text = "continue" |} |})) |> unbox<JS.Promise<unit>>
    do! eventHook $ (mkEvent "session.idle" (box {| sessionID = "resume-ws" |})) |> unbox<JS.Promise<unit>>
    do! Promise.sleep 0
    check "session.next.prompted resumes todo nudge" (promptCalls.Count = 1)
    do! rmAsync workspaceDir
}

let repeatedAssistantSpec () = promise {
    let mutable messages = [|
        box {| info = box {| role = "assistant"; agent = "manager"; finish = "stop"; time = box {| completed = 1 |} |}
               parts = [| box {| ``type`` = "text"; text = "still working" |} |] |}
    |]
    let promptCalls = ResizeArray<obj>()
    let mkClient () =
        createObj [ "session", box (createObj [
            "todo", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                (promise { return box {| data = [| box {| id = "todo-1"; content = "task"; status = "in_progress" |} |] |} })))
            "messages", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                (promise { return box {| data = messages |} })))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg ->
                (promise { promptCalls.Add(arg) })))
        ]) ]
    let! workspaceDir = mkdtempAsync "repeated-assistant-"
    let! p = plugin (box {| directory = workspaceDir; client = mkClient () |})
    do! (get p "event") $ (box {| event = box {| ``type`` = "session.idle"; properties = box {| sessionID = "same-text-ws" |} |} |}) |> unbox<JS.Promise<unit>>
    do! Promise.sleep 0
    check "same text first assistant turn nudges" (promptCalls.Count = 1)
    messages <- Array.append messages [|
        box {| info = box {| role = "assistant"; agent = "manager"; finish = "stop"; time = box {| completed = 2 |} |}
               parts = [| box {| ``type`` = "text"; text = "still working" |} |] |}
    |]
    let! p2 = plugin (box {| directory = workspaceDir; client = mkClient () |})
    do! (get p2 "event") $ (box {| event = box {| ``type`` = "session.idle"; properties = box {| sessionID = "same-text-ws" |} |} |}) |> unbox<JS.Promise<unit>>
    do! Promise.sleep 0
    check "same text new assistant turn nudges again" (promptCalls.Count = 2)
    do! rmAsync workspaceDir
}

let reusedSessionSpec () = promise {
    let mutable messages = [|
        box {| info = box {| role = "assistant"; agent = "manager"; finish = "stop"; time = box {| completed = 1 |} |}
               parts = [| box {| ``type`` = "text"; text = "reopened work" |} |] |}
    |]
    let promptCalls = ResizeArray<obj>()
    let mkClient () =
        createObj [ "session", box (createObj [
            "todo", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                (promise { return box {| data = [| box {| id = "todo-1"; content = "task"; status = "in_progress" |} |] |} })))
            "messages", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                (promise { return box {| data = messages |} })))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg ->
                (promise { promptCalls.Add(arg) })))
        ]) ]
    let! workspaceDir = mkdtempAsync "reused-session-"
    let! p = plugin (box {| directory = workspaceDir; client = mkClient () |})
    let eventHook = get p "event"
    do! eventHook $ (box {| event = box {| ``type`` = "session.idle"; properties = box {| sessionID = "reused-session" |} |} |}) |> unbox<JS.Promise<unit>>
    do! Promise.sleep 0
    do! eventHook $ (box {| event = box {| ``type`` = "session.deleted"; properties = box {| info = box {| id = "reused-session" |} |} |} |}) |> unbox<JS.Promise<unit>>
    messages <- [|
        box {| info = box {| role = "assistant"; agent = "manager"; finish = "stop"; time = box {| completed = 1 |} |}
               parts = [| box {| ``type`` = "text"; text = "reopened work" |} |] |}
    |]
    let! p2 = plugin (box {| directory = workspaceDir; client = mkClient () |})
    do! (get p2 "event") $ (box {| event = box {| ``type`` = "session.idle"; properties = box {| sessionID = "reused-session" |} |} |}) |> unbox<JS.Promise<unit>>
    do! Promise.sleep 0
    check "reused session nudges after session.deleted" (promptCalls.Count = 2)
    do! rmAsync workspaceDir
}

let run () : JS.Promise<unit> =
    promise {
        let reg = createRegistration (createObj [])
        do! eventHookSpec reg
        do! repeatedTodoNudgeSpec reg
        do! syntaxWrapperSpec reg
        do! todoWriteWrapperSpec reg
        let! workspaceDir = mkdtempAsync "tool-execute-after-"
        let! p = plugin (box {| directory = workspaceDir |})
        do! toolExecuteAfterSpec p
        do! rmAsync workspaceDir
        do! abortedRetrySpec ()
        do! repeatedAssistantSpec ()
        do! reusedSessionSpec ()
    }

