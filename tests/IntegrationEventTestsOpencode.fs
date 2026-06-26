module Wanxiangshu.Tests.IntegrationEventTestsOpencode

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Kernel.LoopMessages
open Wanxiangshu.Kernel.PromptFragments
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Opencode.Plugin
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OpencodeSessionEventCodec

let toolExecuteAfterSpec (p: obj) = promise {
    let output = createObj [ "output", box "Todos updated" ]
    do! (get p "tool.execute.after") $ (createObj [ "tool", box "todowrite"; "sessionID", box "test-ws"; "callID", box "todo-1" ], output) |> unbox<JS.Promise<unit>>
    check "tool.execute.after includes meditator hint" (hasExactHint (unbox<string> (get output "output")) hintMeditator)
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

let opencodeLoopNudgeSpec () = promise {
    let sessionID = "opencode-loop-nudge-ws"
    let promptCalls = ResizeArray<obj>()
    let mkClient () =
        createObj [ "session", box (createObj [
            "todo", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                (promise { return box {| data = [||] |} })))
            "messages", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                (promise { return box {| data = [||] |} })))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg ->
                (promise { promptCalls.Add(arg) })))
        ]) ]
    let! workspaceDir = mkdtempAsync "opencode-loop-nudge-"
    let! p = plugin (box {| directory = workspaceDir; client = mkClient () |})
    let cmdHook = get p "command.execute.before"
    let eventHook = get p "event"
    let cmdOut = createObj []
    do! cmdHook $ (createObj [ "command", box "loop"; "sessionID", box sessionID; "arguments", box "Ship the fix" ], cmdOut) |> unbox<JS.Promise<unit>>
    do! eventHook $ (box {| event = box {| ``type`` = "session.idle"; properties = box {| sessionID = sessionID |} |} |}) |> unbox<JS.Promise<unit>>
    do! Promise.sleep 0
    let nudgeText =
        if promptCalls.Count = 0 then ""
        else
            let body = get promptCalls.[0] "body"
            getPartsText (get body "parts")
    check "with-review idle emits loop nudge" (promptCalls.Count = 1 && nudgeText = loopNudgePrompt)
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

let opencodeFreshChatMessageRearmsLoopNudgeSpec () = promise {
    let sessionID = "opencode-fresh-chat-ws"
    let promptCalls = ResizeArray<obj>()
    let mutable messages : obj array = [||]
    let mkClient () =
        createObj [ "session", box (createObj [
            "todo", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                (promise { return box {| data = [||] |} })))
            "messages", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                (promise { return box {| data = messages |} })))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg ->
                (promise { promptCalls.Add(arg) })))
        ]) ]
    let! workspaceDir = mkdtempAsync "opencode-fresh-chat-"
    let! p = plugin (box {| directory = workspaceDir; client = mkClient () |})
    let cmdHook = get p "command.execute.before"
    let eventHook = get p "event"
    let chatHook = get p "chat.message"
    let cmdOut = createObj []
    do! cmdHook $ (createObj [ "command", box "loop"; "sessionID", box sessionID; "arguments", box "Ship the fix" ], cmdOut) |> unbox<JS.Promise<unit>>
    do! eventHook $ (box {| event = box {| ``type`` = "session.idle"; properties = box {| sessionID = sessionID |} |} |}) |> unbox<JS.Promise<unit>>
    do! Promise.sleep 0
    let textOf i =
        if promptCalls.Count <= i then ""
        else
            let body = get promptCalls.[i] "body"
            getPartsText (get body "parts")
    check "first with-review idle emits loop nudge" (promptCalls.Count = 1 && textOf 0 = loopNudgePrompt)
    do! chatHook $ (createObj [ "sessionID", box sessionID; "agent", box "manager" ],
                    createObj [ "parts", box [| box {| ``type`` = "text"; text = "still working on it" |} |] ]) |> unbox<JS.Promise<unit>>
    do! Promise.sleep 0
    messages <- Array.append messages [|
        box {| info = box {| role = "assistant"; agent = "manager"; finish = "stop"; time = box {| completed = 2 |} |}
               parts = [| box {| ``type`` = "text"; text = "still working on it" |} |] |}
    |]
    do! eventHook $ (box {| event = box {| ``type`` = "session.idle"; properties = box {| sessionID = sessionID |} |} |}) |> unbox<JS.Promise<unit>>
    do! Promise.sleep 0
    check "new assistant turn in history re-arms loop nudge on next idle"
        (promptCalls.Count = 2 && textOf 1 = loopNudgePrompt)
    do! rmAsync workspaceDir
}