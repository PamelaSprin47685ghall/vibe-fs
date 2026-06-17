module VibeFs.Tests.IntegrationEventTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Kernel.Dyn
open VibeFs.Index
open VibeFs.Opencode.Plugin

let eventHookSpec (reg: obj) = async {
    let hook = get reg "eventHook"
    check "eventHook.length === 2" (unbox<int> (hook?length) = 2)
    let ehResult = hook $ (createObj [ "type", box "stream-abort"; "workspaceId", box "test-ws" ], null)
    check "eventHook returns Promise" (not (isNullish (get ehResult "then")))
    do! unbox<JS.Promise<unit>> ehResult |> Async.AwaitPromise
}

let repeatedTodoNudgeSpec (reg: obj) = async {
    let nudges = ResizeArray<string>()
    let helpers todoList =
        createObj [
            "getTodos", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                (async { return box (todoList |> List.toArray) } |> Async.StartAsPromise)))
            "nudge", box (System.Func<obj, obj, JS.Promise<bool>>(fun _ws msg ->
                (async { nudges.Add(string msg); return true } |> Async.StartAsPromise)))
        ]
    let hook = get reg "eventHook"
    let streamEnd ws parts =
        createObj [ "type", box "stream-end"; "workspaceId", box ws
                    "properties", box (createObj [ "parts", box parts ]) ]
    let textPart t = box {| ``type`` = "text"; text = t |}
    do! hook $ (streamEnd "repeat-ws" [| textPart "first" |], helpers ["pending"]) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    do! hook $ (streamEnd "repeat-ws" [| textPart "second" |], helpers ["pending"]) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "repeated todo nudge suppressed" (nudges.Count = 1)
    do! hook $ (streamEnd "repeat-ws" [| textPart "cleared" |], helpers []) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    do! hook $ (streamEnd "repeat-ws" [| textPart "reopened" |], helpers ["pending"]) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "todo nudge re-allowed after clear" (nudges.Count = 2)
}

let syntaxWrapperSpec (reg: obj) = async {
    let wrappers = unbox<obj[]> (get reg "wrappers")
    let sw = wrappers |> Array.find (fun w -> str w "targetTool" = "file_edit_replace_string")
    check "syntax wrapper exists" (not (isNullish sw))
    let mockEdit = createObj [ "execute", box (System.Func<obj, JS.Promise<string>>(fun _ ->
        (async { return "File written" } |> Async.StartAsPromise))) ]
    let wrapped = (get sw "wrapper") $ (mockEdit, createObj [ "cwd", box "/tmp" ])
    let! _ = (get wrapped "execute") $ (createObj [ "file_path", box "nonexistent.js" ]) |> unbox<JS.Promise<string>> |> Async.AwaitPromise
    check "syntax wrapper returns result" true
}

let todoWriteWrapperSpec (reg: obj) = async {
    let wrappers = unbox<obj[]> (get reg "wrappers")
    let tw = wrappers |> Array.find (fun w -> str w "targetTool" = "todo_write")
    check "todo_write wrapper exists" (not (isNullish tw))
    let mockTodo = createObj [ "execute", box (System.Func<obj, JS.Promise<string>>(fun _ ->
        (async { return "Todos updated" } |> Async.StartAsPromise))) ]
    let wrapped = (get tw "wrapper") $ (mockTodo, createObj [])
    let! result = (get wrapped "execute") $ (createObj []) |> unbox<JS.Promise<string>> |> Async.AwaitPromise
    check "todo_write wrapper appends reverie nudge" (result.Contains "Think thrice")
}

let toolExecuteAfterSpec (p: obj) = async {
    let output = createObj [ "output", box "Todos updated" ]
    do! (get p "tool.execute.after") $ (createObj [ "tool", box "todowrite"; "sessionID", box "test-ws"; "callID", box "todo-1" ], output) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "tool.execute.after appends reverie nudge" ((unbox<string> (get output "output")).Contains "Think thrice")
}

let abortedRetrySpec () = async {
    let promptCalls = ResizeArray<obj>()
    let mkClient () =
        createObj [ "session", box (createObj [
            "todo", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                (async { return box {| data = [| box {| id = "todo-1"; content = "task"; status = "in_progress" |} |] |} } |> Async.StartAsPromise)))
            "messages", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                (async { return box {| data = [| box {|
                    info = box {| role = "assistant"; agent = "manager"; finish = "stop"; time = box {| completed = 1 |} |}
                    parts = [| box {| ``type`` = "text"; text = "working" |} |]
                |} |] |} } |> Async.StartAsPromise)))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg ->
                (async { promptCalls.Add(arg) } |> Async.StartAsPromise)))
        ]) ]
    let! workspaceDir = mkdtempAsync "aborted-retry-" |> Async.AwaitPromise
    let! p = plugin (box {| directory = workspaceDir; client = mkClient () |}) |> Async.AwaitPromise
    let eventHook = get p "event"
    let mkEvent typ props =
        box {| event = box {| ``type`` = typ; properties = props |} |}
    do! eventHook $ (mkEvent "session.next.step.failed" (box {| sessionID = "resume-ws"; error = box {| ``type`` = "unknown"; message = "Aborted" |} |})) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    do! eventHook $ (mkEvent "session.idle" (box {| sessionID = "resume-ws" |})) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "aborted retry does not nudge before new prompt" (promptCalls.Count = 0)
    do! eventHook $ (mkEvent "session.next.prompted" (box {| sessionID = "resume-ws"; prompt = box {| text = "continue" |} |})) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    do! eventHook $ (mkEvent "session.idle" (box {| sessionID = "resume-ws" |})) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "session.next.prompted resumes todo nudge" (promptCalls.Count = 1)
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let repeatedAssistantSpec () = async {
    let mutable messages = [|
        box {| info = box {| role = "assistant"; agent = "manager"; finish = "stop"; time = box {| completed = 1 |} |}
               parts = [| box {| ``type`` = "text"; text = "still working" |} |] |}
    |]
    let promptCalls = ResizeArray<obj>()
    let mkClient () =
        createObj [ "session", box (createObj [
            "todo", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                (async { return box {| data = [| box {| id = "todo-1"; content = "task"; status = "in_progress" |} |] |} } |> Async.StartAsPromise)))
            "messages", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                (async { return box {| data = messages |} } |> Async.StartAsPromise)))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg ->
                (async { promptCalls.Add(arg) } |> Async.StartAsPromise)))
        ]) ]
    let! workspaceDir = mkdtempAsync "repeated-assistant-" |> Async.AwaitPromise
    let! p = plugin (box {| directory = workspaceDir; client = mkClient () |}) |> Async.AwaitPromise
    do! (get p "event") $ (box {| event = box {| ``type`` = "session.idle"; properties = box {| sessionID = "same-text-ws" |} |} |}) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "same text first assistant turn nudges" (promptCalls.Count = 1)
    messages <- Array.append messages [|
        box {| info = box {| role = "assistant"; agent = "manager"; finish = "stop"; time = box {| completed = 2 |} |}
               parts = [| box {| ``type`` = "text"; text = "still working" |} |] |}
    |]
    let! p2 = plugin (box {| directory = workspaceDir; client = mkClient () |}) |> Async.AwaitPromise
    do! (get p2 "event") $ (box {| event = box {| ``type`` = "session.idle"; properties = box {| sessionID = "same-text-ws" |} |} |}) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "same text new assistant turn nudges again" (promptCalls.Count = 2)
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let reusedSessionSpec () = async {
    let mutable messages = [|
        box {| info = box {| role = "assistant"; agent = "manager"; finish = "stop"; time = box {| completed = 1 |} |}
               parts = [| box {| ``type`` = "text"; text = "reopened work" |} |] |}
    |]
    let promptCalls = ResizeArray<obj>()
    let mkClient () =
        createObj [ "session", box (createObj [
            "todo", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                (async { return box {| data = [| box {| id = "todo-1"; content = "task"; status = "in_progress" |} |] |} } |> Async.StartAsPromise)))
            "messages", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                (async { return box {| data = messages |} } |> Async.StartAsPromise)))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg ->
                (async { promptCalls.Add(arg) } |> Async.StartAsPromise)))
        ]) ]
    let! workspaceDir = mkdtempAsync "reused-session-" |> Async.AwaitPromise
    let! p = plugin (box {| directory = workspaceDir; client = mkClient () |}) |> Async.AwaitPromise
    let eventHook = get p "event"
    do! eventHook $ (box {| event = box {| ``type`` = "session.idle"; properties = box {| sessionID = "reused-session" |} |} |}) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    do! eventHook $ (box {| event = box {| ``type`` = "session.deleted"; properties = box {| info = box {| id = "reused-session" |} |} |} |}) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    messages <- [|
        box {| info = box {| role = "assistant"; agent = "manager"; finish = "stop"; time = box {| completed = 1 |} |}
               parts = [| box {| ``type`` = "text"; text = "reopened work" |} |] |}
    |]
    let! p2 = plugin (box {| directory = workspaceDir; client = mkClient () |}) |> Async.AwaitPromise
    do! (get p2 "event") $ (box {| event = box {| ``type`` = "session.idle"; properties = box {| sessionID = "reused-session" |} |} |}) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    check "reused session nudges after session.deleted" (promptCalls.Count = 2)
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let run () : JS.Promise<unit> =
    async {
        let reg = createRegistration (createObj [])
        do! eventHookSpec reg
        do! repeatedTodoNudgeSpec reg
        do! syntaxWrapperSpec reg
        do! todoWriteWrapperSpec reg
        let! workspaceDir = mkdtempAsync "tool-execute-after-" |> Async.AwaitPromise
        let! p = plugin (box {| directory = workspaceDir |}) |> Async.AwaitPromise
        do! toolExecuteAfterSpec p
        do! rmAsync workspaceDir |> Async.AwaitPromise
        do! abortedRetrySpec ()
        do! repeatedAssistantSpec ()
        do! reusedSessionSpec ()
    }
    |> Async.StartAsPromise
