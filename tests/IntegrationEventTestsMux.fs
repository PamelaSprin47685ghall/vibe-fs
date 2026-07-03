module Wanxiangshu.Tests.IntegrationEventTestsMux

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.IntegrationMuxSetup
open Wanxiangshu.Tests.EventLogTestSeed
open Wanxiangshu.Tests.AsyncFlush

open Wanxiangshu.Kernel.LoopMessages
open Wanxiangshu.Kernel.ReviewPrompts
open Wanxiangshu.Kernel.PromptFragments
open Wanxiangshu.Kernel.PromptFrontMatter
open Wanxiangshu.Mux.Plugin
open Wanxiangshu.Shell.Dyn

[<Emit("process.cwd()")>]
let private processCwd () : string = jsNative

let private loopAnchor task = frontMatterPrompt [ yamlField taskField task ] "With-Review Mode is active."

let repeatedTodoNudgeSpec () = promise {
    let mutable history = [| muxTextMessage "repeat-assistant-1" "assistant" "first" |]
    let nudges = ResizeArray<string>()
    let reg =
        createRegistration
            (createObj [
                "loadConfigOrDefault", box (fun () -> createObj [])
                "findWorkspaceEntry", box (System.Func<obj, string, obj>(fun _ _ -> createObj [ "workspace", null ]))
                "resolveAgentFrontmatter", box (System.Func<obj, obj, string, JS.Promise<obj>>(fun _ _ _ -> Promise.lift (createObj [])))
                "getChatHistory", box (System.Func<string, JS.Promise<obj array>>(fun workspaceId -> promise { return if workspaceId = "repeat-ws" then history else [||] }))
            ])
    let mutable nudgeCount = 0
    let helpers todoList =
        createObj [
            "getTodos", box (System.Func<obj, JS.Promise<obj>>(fun _ ->
                (promise { return box (todoList |> List.toArray) })))
            "nudge", box (System.Func<obj, obj, JS.Promise<bool>>(fun _ws msg ->
                promise {
                    nudges.Add(string msg)
                    nudgeCount <- nudgeCount + 1
                    history <- Array.append history [| muxTextMessage ($"repeat-nudge-{nudgeCount}") "user" (string msg) |]
                    return true
                }))
        ]
    let hook = get reg "eventHook"
    let streamEnd ws parts =
        createObj [ "type", box "stream-end"; "workspaceId", box ws
                    "properties", box (createObj [ "parts", box parts ]) ]
    let textPart t = box {| ``type`` = "text"; text = t |}
    do! hook $ (streamEnd "repeat-ws" [| textPart "first" |], helpers ["pending"]) |> unbox<JS.Promise<unit>>
    do! yieldMicrotask ()
    do! hook $ (streamEnd "repeat-ws" [| textPart "first" |], helpers ["pending"]) |> unbox<JS.Promise<unit>>
    do! yieldMicrotask ()
    check "todo nudge dedupes from event log integral" (nudges.Count = 1)
    history <- Array.append history [| muxTextMessage "repeat-assistant-2" "assistant" "second" |]
    do! hook $ (streamEnd "repeat-ws" [| textPart "second" |], helpers ["pending"]) |> unbox<JS.Promise<unit>>
    do! yieldMicrotask ()
    check "fresh assistant output re-allows todo nudge" (nudges.Count = 2)
}

let reviewerRejectRenudgesLoopSpec () = promise {
    let sessionID = "review-reject-ws"
    do! seedLoopActivated (processCwd ()) sessionID "Implement feature X"
    let mutable history = [| muxTextMessage "review-loop-anchor" "assistant" (loopAnchor "Implement feature X")
                             muxTextMessage "review-assistant-1" "assistant" "implemented first pass" |]
    let reg =
        createRegistration
            (createObj [
                "loadConfigOrDefault", box (fun () -> createObj [])
                "findWorkspaceEntry", box (System.Func<obj, string, obj>(fun _ _ -> createObj [ "workspace", null ]))
                "resolveAgentFrontmatter", box (System.Func<obj, obj, string, JS.Promise<obj>>(fun _ _ _ -> Promise.lift (createObj [])))
                "getChatHistory", box (System.Func<string, JS.Promise<obj array>>(fun workspaceId -> promise { return if workspaceId = sessionID then history else [||] }))
            ])
    let nudges = ResizeArray<string>()
    let mutable nudgeCount = 0
    let helpers =
        createObj [
            "getTodos", box (System.Func<obj, JS.Promise<obj>>(fun _ -> promise { return box [||] }))
            "nudge", box (System.Func<obj, obj, JS.Promise<bool>>(fun _ws msg ->
                promise {
                    nudges.Add(string msg)
                    nudgeCount <- nudgeCount + 1
                    history <- Array.append history [| muxTextMessage ($"review-nudge-{nudgeCount}") "user" (string msg) |]
                    return true
                }))
        ]
    let hook = get reg "eventHook"
    let streamEnd text =
        createObj [ "type", box "stream-end"; "workspaceId", box sessionID
                    "properties", box (createObj [ "parts", box [| box {| ``type`` = "text"; text = text |} |] ]) ]
    do! hook $ (streamEnd "implemented first pass", helpers) |> unbox<JS.Promise<unit>>
    do! yieldMicrotask ()
    check "active review emits loop nudge" (nudges.Count = 1 && nudges.[0].Contains(loopNudgePromptProse))
    history <- Array.append history [| muxTextMessage "review-assistant-2" "assistant" "verdict: rejected\nfeedback: needs rework" |]
    do! hook $ (streamEnd "verdict: rejected\nfeedback: needs rework", helpers) |> unbox<JS.Promise<unit>>
    do! yieldMicrotask ()
    check "reviewer reject reopens loop nudge on fresh assistant output" (nudges.Count = 2 && nudges.[1].Contains(loopNudgePromptProse))
}

let muxSubmitReviewWipDoesNotSuppressLoopNudgeSpec () = promise {
    let sessionID = "review-wip-nudge-ws"
    do! seedLoopActivated (processCwd ()) sessionID "Implement feature X"
    let mutable history = [| muxTextMessage "review-wip-loop-anchor" "assistant" (loopAnchor "Implement feature X")
                             muxTextMessage "review-wip-assistant-1" "assistant" "implemented first pass" |]
    let reg =
        createRegistration
            (createObj [
                "loadConfigOrDefault", box (fun () -> createObj [])
                "findWorkspaceEntry", box (System.Func<obj, string, obj>(fun _ _ -> createObj [ "workspace", null ]))
                "resolveAgentFrontmatter", box (System.Func<obj, obj, string, JS.Promise<obj>>(fun _ _ _ -> Promise.lift (createObj [])))
                "getChatHistory", box (System.Func<string, JS.Promise<obj array>>(fun workspaceId -> promise { return if workspaceId = sessionID then history else [||] }))
            ])
    let nudges = ResizeArray<string>()
    let mutable nudgeCount = 0
    let helpers =
        createObj [
            "getTodos", box (System.Func<obj, JS.Promise<obj>>(fun _ -> promise { return box [||] }))
            "nudge", box (System.Func<obj, obj, JS.Promise<bool>>(fun _ws msg ->
                promise {
                    nudges.Add(string msg)
                    nudgeCount <- nudgeCount + 1
                    history <- Array.append history [| muxTextMessage ($"review-wip-nudge-{nudgeCount}") "user" (string msg) |]
                    return true
                }))
        ]
    let hook = get reg "eventHook"
    let streamEnd text =
        createObj [ "type", box "stream-end"; "workspaceId", box sessionID
                    "properties", box (createObj [ "parts", box [| box {| ``type`` = "text"; text = text |} |] ]) ]
    do! hook $ (streamEnd "implemented first pass", helpers) |> unbox<JS.Promise<unit>>
    do! yieldMicrotask ()
    check "active review emits first loop nudge" (nudges.Count = 1 && nudges.[0].Contains(loopNudgePromptProse))
    history <-
        Array.append history
            [| muxDynamicToolMessage "review-wip-tool" "submit_review" "wip-call" (createObj []) (box submitReviewWipAcknowledgment) |]
    do! hook $ (streamEnd "continued after wip report", helpers) |> unbox<JS.Promise<unit>>
    do! yieldMicrotask ()
    check "wip submit_review does not permanently suppress loop nudge" (nudges.Count = 2 && nudges.[1].Contains(loopNudgePromptProse))
}

let muxForceStopTodoNudgeSpec () = promise {
    let sessionID = "force-stop-ws"
    let mutable history = [| muxTextMessage "force-assistant-1" "assistant" "working on it" |]
    let nudges = ResizeArray<string>()
    let reg =
        createRegistration
            (createObj [
                "loadConfigOrDefault", box (fun () -> createObj [])
                "findWorkspaceEntry", box (System.Func<obj, string, obj>(fun _ _ -> createObj [ "workspace", null ]))
                "resolveAgentFrontmatter", box (System.Func<obj, obj, string, JS.Promise<obj>>(fun _ _ _ -> Promise.lift (createObj [])))
                "getChatHistory", box (System.Func<string, JS.Promise<obj array>>(fun workspaceId -> promise { return if workspaceId = sessionID then history else [||] }))
            ])
    let mutable nudgeCount = 0
    let helpers todoList =
        createObj [
            "getTodos", box (System.Func<obj, JS.Promise<obj>>(fun _ ->
                (promise { return box (todoList |> List.toArray) })))
            "nudge", box (System.Func<obj, obj, JS.Promise<bool>>(fun _ws msg ->
                promise {
                    nudges.Add(string msg)
                    nudgeCount <- nudgeCount + 1
                    return true
                }))
        ]
    let hook = get reg "eventHook"
    let streamAbort = createObj [ "type", box "stream-abort"; "workspaceId", box sessionID ]
    let streamEnd ws parts =
        createObj [ "type", box "stream-end"; "workspaceId", box ws
                    "properties", box (createObj [ "parts", box parts ]) ]
    let textPart t = box {| ``type`` = "text"; text = t |}
    do! hook $ (streamAbort, helpers ["pending"]) |> unbox<JS.Promise<unit>>
    do! yieldMicrotask ()
    do! hook $ (streamEnd sessionID [| textPart "working on it" |], helpers ["pending"]) |> unbox<JS.Promise<unit>>
    do! yieldMicrotask ()
    check "force-stop must not send todo nudge" (nudges.Count = 0)
}
