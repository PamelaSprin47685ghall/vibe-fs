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

let opencodeCompactingEmitsAnchorPromptSpec () = promise {
    let sessionID = "opencode-compact-emit-anchor"
    let promptCalls = ResizeArray<obj>()
    let msgStore = ResizeArray<obj>()
    let mkClient () =
        createObj [ "session", box (createObj [
            "todo", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                promise { return box {| data = [||] |} }))
            "messages", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                promise { return box {| data = msgStore.ToArray() |} }))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg ->
                promise { promptCalls.Add(arg); msgStore.Add(arg) }))
        ]) ]
    let! workspaceDir = mkdtempAsync "opencode-compact-emit-anchor-"
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
        [| textMessage "anchor-user-1" "user" "manager" "plan phase"
           toolMessage "anchor-1" "anchor-call-a" "anchor planned phase" "Anchor Plan" "in_progress" "high"
           textMessage "anchor-user-2" "user" "manager" "implement phase"
           toolMessage "anchor-2" "anchor-call-b" "anchor implemented phase" "Anchor Implement" "completed" "high" |]
    msgStore.AddRange messages
    let output = createObj [ "messages", box messages ]
    do! compacting $ (createObj [ "sessionID", box sessionID ], output) |> unbox<JS.Promise<unit>>
    check "compacting hook emits exactly one anchor prompt" (promptCalls.Count = 1)
    if promptCalls.Count > 0 then
        let promptTextStr = promptText promptCalls.[0]
        check "anchor prompt contains See above body" (promptTextStr.Contains "See above for some messages before compaction.")
        check "anchor prompt contains backlog report text (planned phase)" (promptTextStr.Contains "anchor planned phase")
        check "anchor prompt contains backlog report text (implemented phase)" (promptTextStr.Contains "anchor implemented phase")
    let transformed = unbox<obj[]> (get output "messages")
    check "compacting hook still projects backlog" (transformed.Length > 0)
    do! rmAsync workspaceDir
}

let opencodeCompactingAnchorUsesPriorAgentSpec () = promise {
    let sessionID = "opencode-compact-anchor-agent"
    let promptCalls = ResizeArray<obj>()
    let msgStore = ResizeArray<obj>()
    let anchorBlock = "---\ntask: implemented-feature\n---\nWork completed."
    let managerMsg =
        box (createObj [
            "info", box (createObj [ "role", box "assistant"; "agent", box "manager"; "finish", box "stop"; "sessionID", box sessionID; "time", box (createObj [ "completed", box 1 ]) ])
            "parts", box [| box {| ``type`` = "text"; text = anchorBlock |} |]
        ])
    let mkClient () =
        createObj [ "session", box (createObj [
            "todo", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                promise { return box {| data = [||] |} }))
            "messages", box (System.Func<unit, JS.Promise<obj>>(fun () ->
                promise { return box {| data = msgStore.ToArray() |} }))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg ->
                promise {
                    promptCalls.Add(arg)
                    msgStore.Add(userTextMessage sessionID (promptText arg))
                }))
        ]) ]
    let! workspaceDir = mkdtempAsync "opencode-compact-anchor-agent-"
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
        [| managerMsg
           textMessage "anchor-user-1" "user" "manager" "plan phase"
           toolMessage "anchor-1" "anchor-call-a" "anchor planned phase" "Anchor Plan" "in_progress" "high"
           textMessage "anchor-user-2" "user" "manager" "implement phase"
           toolMessage "anchor-2" "anchor-call-b" "anchor implemented phase" "Anchor Implement" "completed" "high" |]
    msgStore.AddRange messages
    let output = createObj [ "messages", box messages ]
    do! compacting $ (createObj [ "sessionID", box sessionID ], output) |> unbox<JS.Promise<unit>>
    check "compacting anchor prompt is emitted" (promptCalls.Count = 1)
    if promptCalls.Count > 0 then
        let body = get promptCalls.[0] "body"
        check "anchor prompt carries prior real agent" (str body "agent" = "manager")
    do! compacting $ (createObj [ "sessionID", box sessionID ], output) |> unbox<JS.Promise<unit>>
    check "compacting anchor prompt is emitted once" (promptCalls.Count = 1)
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
