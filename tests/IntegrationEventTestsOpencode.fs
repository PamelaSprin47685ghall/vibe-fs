module Wanxiangshu.Tests.IntegrationEventTestsOpencode

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Kernel.LoopMessages
open Wanxiangshu.Kernel.PromptFragments
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Tests.IntegrationMuxSetup
open Wanxiangshu.Opencode.Plugin
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OpencodeSessionEventCodec

let private promptText (arg: obj) =
    getPartsText (get (get arg "body") "parts")

let private userTextMessage sessionID text =
    box (createObj [
        "info", box (createObj [ "role", box "user"; "sessionID", box sessionID ])
        "parts", box [| createObj [ "type", box "text"; "text", box text ] |]
    ])

let toolExecuteAfterSpec (p: obj) = promise {
    let output = createObj [ "output", box "Todos updated" ]
    do! (get p "tool.execute.after") $ (createObj [ "tool", box "todowrite"; "sessionID", box "test-ws"; "callID", box "todo-1" ], output) |> unbox<JS.Promise<unit>>
    check "tool.execute.after includes meditator hint" (hasExactHint (unbox<string> (get output "output")) hintMeditator)
}

let abortedRetrySpec () = promise {
    let promptCalls = ResizeArray<obj>()
    let mutable messages : obj array = [||]
    let mkClient () =
        createObj [ "session", box (createObj [
            "todo", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                (promise { return box {| data = [| box {| id = "todo-1"; content = "task"; status = "in_progress" |} |] |} })))
            "messages", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                (promise { return box {| data = messages |} })))
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
    do! yieldMicrotask ()
    check "aborted retry with no completed assistant does not nudge" (promptCalls.Count = 0)
    do! eventHook $ (mkEvent "session.next.prompted" (box {| sessionID = "resume-ws"; prompt = box {| text = "continue" |} |})) |> unbox<JS.Promise<unit>>
    messages <- [| box {|
        info = box {| role = "assistant"; agent = "manager"; finish = "stop"; time = box {| completed = 1 |} |}
        parts = [| box {| ``type`` = "text"; text = "working" |} |]
    |} |]
    do! eventHook $ (mkEvent "session.idle" (box {| sessionID = "resume-ws" |})) |> unbox<JS.Promise<unit>>
    do! yieldMicrotask ()
    check "new completed assistant history resumes todo nudge" (promptCalls.Count = 1)
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
    do! yieldMicrotask ()
    check "same text first assistant turn nudges" (promptCalls.Count = 1)
    messages <- Array.append messages [|
        box {| info = box {| role = "assistant"; agent = "manager"; finish = "stop"; time = box {| completed = 2 |} |}
               parts = [| box {| ``type`` = "text"; text = "still working" |} |] |}
    |]
    let! p2 = plugin (box {| directory = workspaceDir; client = mkClient () |})
    do! (get p2 "event") $ (box {| event = box {| ``type`` = "session.idle"; properties = box {| sessionID = "same-text-ws" |} |} |}) |> unbox<JS.Promise<unit>>
    do! yieldMicrotask ()
    check "same text new assistant turn nudges again" (promptCalls.Count = 2)
    do! rmAsync workspaceDir
}

let repeatedIdleBeforeHistoryPersistsNudgeSpec () = promise {
    let sessionID = "history-race-ws"
    let mutable messages = [|
        box {| info = box {| role = "assistant"; agent = "manager"; finish = "stop"; time = box {| completed = 1 |} |}
               parts = [| box {| ``type`` = "text"; text = "still working" |} |] |}
    |]
    let promptCalls = ResizeArray<obj>()
    let mkClient () =
        createObj [ "session", box (createObj [
            "todo", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                promise { return box {| data = [| box {| id = "todo-1"; content = "task"; status = "in_progress" |} |] |} }))
            "messages", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                promise { return box {| data = messages |} }))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg ->
                promise {
                    promptCalls.Add(arg)
                    messages <- Array.append messages [| userTextMessage sessionID (promptText arg) |]
                }))
        ]) ]
    let! workspaceDir = mkdtempAsync "repeated-idle-before-history-"
    let! p = plugin (box {| directory = workspaceDir; client = mkClient () |})
    let eventHook = get p "event"
    let idle = box {| event = box {| ``type`` = "session.idle"; properties = box {| sessionID = sessionID |} |} |}
    do! eventHook $ idle |> unbox<JS.Promise<unit>>
    do! yieldMicrotask ()
    do! eventHook $ idle |> unbox<JS.Promise<unit>>
    do! yieldMicrotask ()
    check "repeated idle before nudge reaches history sends once" (promptCalls.Count = 1)
    do! rmAsync workspaceDir
}

let sessionStatusIdleAndSessionIdleDedupSpec () = promise {
    let sessionID = "dedup-session"
    let mutable messages = [|
        box {| info = box {| role = "assistant"; agent = "manager"; finish = "stop"; time = box {| completed = 1 |} |}
               parts = [| box {| ``type`` = "text"; text = "still working" |} |] |}
    |]
    let promptCalls = ResizeArray<obj>()
    let mkClient () =
        createObj [ "session", box (createObj [
            "todo", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                promise { return box {| data = [| box {| id = "todo-1"; content = "task"; status = "in_progress" |} |] |} }))
            "messages", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                promise { return box {| data = messages |} }))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg ->
                promise {
                    promptCalls.Add(arg)
                    messages <- Array.append messages [| userTextMessage sessionID (promptText arg) |]
                }))
        ]) ]
    let! workspaceDir = mkdtempAsync "session-status-idle-dedup-"
    let! p = plugin (box {| directory = workspaceDir; client = mkClient () |})
    let eventHook = get p "event"
    let statusIdle = box {| event = box {| ``type`` = "session.status"; properties = box {| sessionID = sessionID; status = box {| ``type`` = "idle" |} |} |} |}
    let idle = box {| event = box {| ``type`` = "session.idle"; properties = box {| sessionID = sessionID |} |} |}
    do! eventHook $ statusIdle |> unbox<JS.Promise<unit>>
    do! yieldMicrotask ()
    do! eventHook $ idle |> unbox<JS.Promise<unit>>
    do! yieldMicrotask ()
    check "session.status idle + session.idle for same session sends nudge once" (promptCalls.Count = 1)
    do! rmAsync workspaceDir
}

let sessionStatusBusyDoesNotNudgeSpec () = promise {
    let messages = [|
        box {| info = box {| role = "assistant"; agent = "manager"; finish = "stop"; time = box {| completed = 1 |} |}
               parts = [| box {| ``type`` = "text"; text = "still working" |} |] |}
    |]
    let promptCalls = ResizeArray<obj>()
    let mkClient () =
        createObj [ "session", box (createObj [
            "todo", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                promise { return box {| data = [| box {| id = "todo-1"; content = "task"; status = "in_progress" |} |] |} }))
            "messages", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                promise { return box {| data = messages |} }))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg ->
                promise { promptCalls.Add(arg) }))
        ]) ]
    let! workspaceDir = mkdtempAsync "session-status-busy-"
    let! p = plugin (box {| directory = workspaceDir; client = mkClient () |})
    let eventHook = get p "event"
    let sessionID = "busy-session"
    let statusBusy = box {| event = box {| ``type`` = "session.status"; properties = box {| sessionID = sessionID; status = box {| ``type`` = "busy" |} |} |} |}
    do! eventHook $ statusBusy |> unbox<JS.Promise<unit>>
    do! yieldMicrotask ()
    check "session.status busy does not nudge while agent is working" (promptCalls.Count = 0)
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
    do! yieldMicrotask ()
    do! eventHook $ (box {| event = box {| ``type`` = "session.deleted"; properties = box {| info = box {| id = "reused-session" |} |} |} |}) |> unbox<JS.Promise<unit>>
    messages <- [|
        box {| info = box {| role = "assistant"; agent = "manager"; finish = "stop"; time = box {| completed = 1 |} |}
               parts = [| box {| ``type`` = "text"; text = "reopened work" |} |] |}
    |]
    let! p2 = plugin (box {| directory = workspaceDir; client = mkClient () |})
    do! (get p2 "event") $ (box {| event = box {| ``type`` = "session.idle"; properties = box {| sessionID = "reused-session" |} |} |}) |> unbox<JS.Promise<unit>>
    do! yieldMicrotask ()
    check "reused session nudges after session.deleted" (promptCalls.Count = 2)
    do! rmAsync workspaceDir
}

let opencodeForceStopTodoNudgeSpec () = promise {
    let sessionID = "force-stop-ws"
    let mutable messages = [|
        box {| info = box {| role = "assistant"; agent = "manager"; finish = "stop"; time = box {| completed = 1 |} |}
               parts = [| box {| ``type`` = "text"; text = "working on it" |} |] |}
    |]
    let promptCalls = ResizeArray<obj>()
    let mkClient () =
        createObj [ "session", box (createObj [
            "todo", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                promise { return box {| data = [| box {| id = "todo-1"; content = "task"; status = "in_progress" |} |] |} }))
            "messages", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                promise { return box {| data = messages |} }))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg ->
                promise { promptCalls.Add(arg) }))
        ]) ]
    let! workspaceDir = mkdtempAsync "force-stop-"
    let! p = plugin (box {| directory = workspaceDir; client = mkClient () |})
    let eventHook = get p "event"
    let mkEvent typ props =
        box {| event = box {| ``type`` = typ; properties = props |} |}
    do! eventHook $ (mkEvent "stream-abort" (box {| sessionID = sessionID |})) |> unbox<JS.Promise<unit>>
    do! yieldMicrotask ()
    do! eventHook $ (mkEvent "session.idle" (box {| sessionID = sessionID |})) |> unbox<JS.Promise<unit>>
    do! yieldMicrotask ()
    check "force-stop must not send todo nudge" (promptCalls.Count = 0)
    do! rmAsync workspaceDir
}
