module Wanxiangshu.Tests.IntegrationEventTestsOpencode

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Kernel.LoopMessages
open Wanxiangshu.Kernel.PromptFragments
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Tests.IntegrationMuxSetup
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

let opencodeCompactingDoesNotEmitPromptSpec () = promise {
    let sessionID = "opencode-compact-session"
    let promptCalls = ResizeArray<obj>()
    let mkClient () =
        createObj [ "session", box (createObj [
            "todo", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                promise { return box {| data = [||] |} }))
            "messages", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                promise { return box {| data = [||] |} }))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg ->
                promise { promptCalls.Add(arg) }))
        ]) ]
    let! workspaceDir = mkdtempAsync "opencode-compact-"
    let! p = plugin (box {| directory = workspaceDir; client = mkClient () |})
    let compacting = get p "experimental.session.compacting"
    let messageInfo id role agent = createObj [ "id", box id; "agent", box agent; "sessionID", box sessionID; "role", box role ]
    let textMessage id role agent text = createObj [ "info", box (messageInfo id role agent); "parts", box [| createObj [ "type", box "text"; "text", box text ] |] ]
    let todoState report content status priority =
        createObj [
            "status", box "completed"
            "input", box (createObj [ "ahaMoments", box report; "changesAndReasons", box ""; "gotchas", box ""; "lessonsAndConventions", box ""; "plan", box ""; "todos", box [| createObj [ "content", box content; "status", box status; "priority", box priority ] |] ])
            "output", box (createObj [ "success", box true; "count", box 1 ])
            "error", box ""
        ]
    let toolMessage id callID report content status priority =
        createObj [
            "info", box (messageInfo id "assistant" "manager")
            "parts", box [| createObj [ "type", box "tool"; "tool", box "todowrite"; "callID", box callID; "state", box (todoState report content status priority) ] |]
        ]
    let messages =
        [| textMessage "compact-user-1" "user" "manager" "plan phase"
           toolMessage "compact-1" "compact-call-a" "planned compact phase" "Plan change" "in_progress" "high"
           textMessage "compact-user-2" "user" "manager" "implement phase"
           toolMessage "compact-2" "compact-call-b" "implemented compact phase" "Implement change" "completed" "high" |]
    let output = createObj [ "messages", box messages ]
    do! compacting $ (createObj [ "sessionID", box sessionID ], output) |> unbox<JS.Promise<unit>>
    check "compacting hook does not emit prompt" (promptCalls.Count = 0)
    let transformed = unbox<obj[]> (get output "messages")
    check "compacting hook still projects backlog" (transformed.Length > 0)
    do! rmAsync workspaceDir
}

let opencodeNudgeAfterCompactionEmitsAnchorPromptSpec () = promise {
    let sessionID = "opencode-compaction-nudge"
    let promptCalls = ResizeArray<obj>()
    let messages =
        [| box (createObj [
            "info", box (createObj [ "role", box "assistant"; "agent", box "compaction"; "finish", box "stop"; "sessionID", box sessionID; "time", box (createObj [ "completed", box 1 ]) ])
            "parts", box [| createObj [ "type", box "text"; "text", box "Compacted summary of previous work." ] |]
        ]) |]
    let mkClient () =
        createObj [ "session", box (createObj [
            "todo", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                promise { return box {| data = [||] |} }))
            "messages", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                promise { return box {| data = messages |} }))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg ->
                promise { promptCalls.Add(arg) }))
        ]) ]
    let! workspaceDir = mkdtempAsync "opencode-compaction-nudge-"
    let! p = plugin (box {| directory = workspaceDir; client = mkClient () |})
    let eventHook = get p "event"
    do! eventHook $ (box {| event = box {| ``type`` = "session.idle"; properties = box {| sessionID = sessionID |} |} |}) |> unbox<JS.Promise<unit>>
    do! Promise.sleep 0
    check "nudge after compaction with no durable content emits no prompt" (promptCalls.Count = 0)
    do! rmAsync workspaceDir
}

let opencodeCompactionAnchorUsesPriorAgentSpec () = promise {
    let sessionID = "opencode-compaction-anchor-agent"
    let promptCalls = ResizeArray<obj>()
    let mkClient () =
        createObj [ "session", box (createObj [
            "todo", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                promise { return box {| data = [||] |} }))
            "messages", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                let managerMsg =
                    let anchorBlock = "---\ntask: implemented-feature\n---\nWork completed."
                    box (createObj [
                        "info", box (createObj [ "role", box "assistant"; "agent", box "manager"; "finish", box "stop"; "sessionID", box sessionID; "time", box (createObj [ "completed", box 1 ]) ])
                        "parts", box [| box {| ``type`` = "text"; text = anchorBlock |} |]
                    ])
                let compactionMsg =
                    box (createObj [
                        "info", box (createObj [ "role", box "assistant"; "agent", box "compaction"; "finish", box "stop"; "sessionID", box sessionID; "time", box (createObj [ "completed", box 2 ]) ])
                        "parts", box [| box {| ``type`` = "text"; text = "Compacted summary of previous work." |} |]
                    ])
                promise { return box {| data = [| managerMsg; compactionMsg |] |} }))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg ->
                promise { promptCalls.Add(arg) }))
        ]) ]
    let! workspaceDir = mkdtempAsync "opencode-compaction-anchor-agent-"
    let! p = plugin (box {| directory = workspaceDir; client = mkClient () |})
    let eventHook = get p "event"
    do! eventHook $ (box {| event = box {| ``type`` = "session.idle"; properties = box {| sessionID = sessionID |} |} |}) |> unbox<JS.Promise<unit>>
    do! Promise.sleep 0
    check "compaction anchor prompt is emitted" (promptCalls.Count = 1)
    let body = get promptCalls.[0] "body"
    check "anchor prompt carries prior real agent" (str body "agent" = "manager")
    do! rmAsync workspaceDir
}
