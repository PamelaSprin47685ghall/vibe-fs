module Wanxiangshu.Tests.IntegrationEventTestsOpencodeCompaction

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TempWorkspace
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
    let anchorBlock = "---\ntask: implemented-feature\n---\nWork completed."
    let managerMsg =
        box (createObj [
            "info", box (createObj [ "role", box "assistant"; "agent", box "manager"; "finish", box "stop"; "sessionID", box sessionID; "time", box (createObj [ "completed", box 1 ]) ])
            "parts", box [| box {| ``type`` = "text"; text = anchorBlock |} |]
        ])
    let compactionMsg =
        box (createObj [
            "info", box (createObj [ "role", box "assistant"; "agent", box "compaction"; "finish", box "stop"; "sessionID", box sessionID; "time", box (createObj [ "completed", box 2 ]) ])
            "parts", box [| box {| ``type`` = "text"; text = "Compacted summary of previous work." |} |]
        ])
    let mutable messages = [| managerMsg; compactionMsg |]
    let mkClient () =
        createObj [ "session", box (createObj [
            "todo", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                promise { return box {| data = [||] |} }))
            "messages", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                promise { return box {| data = messages |} }))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg ->
                promise {
                    promptCalls.Add(arg)
                    messages <- Array.append messages [| userTextMessage sessionID (promptText arg) |]
                }))
        ]) ]
    let! workspaceDir = mkdtempAsync "opencode-compaction-anchor-agent-"
    let! p = plugin (box {| directory = workspaceDir; client = mkClient () |})
    let eventHook = get p "event"
    do! eventHook $ (box {| event = box {| ``type`` = "session.idle"; properties = box {| sessionID = sessionID |} |} |}) |> unbox<JS.Promise<unit>>
    do! Promise.sleep 0
    check "compaction anchor prompt is emitted" (promptCalls.Count = 1)
    let body = get promptCalls.[0] "body"
    check "anchor prompt carries prior real agent" (str body "agent" = "manager")
    do! eventHook $ (box {| event = box {| ``type`` = "session.idle"; properties = box {| sessionID = sessionID |} |} |}) |> unbox<JS.Promise<unit>>
    do! Promise.sleep 0
    check "compaction anchor prompt is emitted once" (promptCalls.Count = 1)
    do! rmAsync workspaceDir
}