module VibeFs.Tests.IntegrationEventTestsMux

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.IntegrationMuxSetup
open VibeFs.Kernel.LoopMessages
open VibeFs.Kernel.ReviewPrompts
open VibeFs.Kernel.PromptFragments
open VibeFs.Mux.Plugin
open VibeFs.Shell.Dyn

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
    do! Promise.sleep 0
    do! hook $ (streamEnd "repeat-ws" [| textPart "first" |], helpers ["pending"]) |> unbox<JS.Promise<unit>>
    do! Promise.sleep 0
    check "todo nudge dedupes from history after synthetic nudge" (nudges.Count = 1)
    history <- Array.append history [| muxTextMessage "repeat-assistant-2" "assistant" "second" |]
    do! hook $ (streamEnd "repeat-ws" [| textPart "second" |], helpers ["pending"]) |> unbox<JS.Promise<unit>>
    do! Promise.sleep 0
    check "fresh assistant output re-allows todo nudge" (nudges.Count = 2)
}

let reviewerRejectRenudgesLoopSpec () = promise {
    let sessionID = "review-reject-ws"
    let mutable history = [| muxTextMessage "review-assistant-1" "assistant" "implemented first pass" |]
    let reg =
        createRegistration
            (createObj [
                "loadConfigOrDefault", box (fun () -> createObj [])
                "findWorkspaceEntry", box (System.Func<obj, string, obj>(fun _ _ -> createObj [ "workspace", null ]))
                "resolveAgentFrontmatter", box (System.Func<obj, obj, string, JS.Promise<obj>>(fun _ _ _ -> Promise.lift (createObj [])))
                "getChatHistory", box (System.Func<string, JS.Promise<obj array>>(fun workspaceId -> promise { return if workspaceId = sessionID then history else [||] }))
            ])
    muxActivateReviewForTest reg sessionID "Implement feature X"
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
    do! Promise.sleep 0
    check "active review emits loop nudge" (nudges.Count = 1 && nudges.[0] = loopNudgePrompt)
    history <- Array.append history [| muxTextMessage "review-assistant-2" "assistant" "verdict: rejected\nfeedback: needs rework" |]
    do! hook $ (streamEnd "verdict: rejected\nfeedback: needs rework", helpers) |> unbox<JS.Promise<unit>>
    do! Promise.sleep 0
    check "reviewer reject reopens loop nudge on fresh assistant output" (nudges.Count = 2 && nudges.[1] = loopNudgePrompt)
}

let muxSubmitReviewWipDoesNotSuppressLoopNudgeSpec () = promise {
    let sessionID = "review-wip-nudge-ws"
    let mutable history = [| muxTextMessage "review-wip-assistant-1" "assistant" "implemented first pass" |]
    let reg =
        createRegistration
            (createObj [
                "loadConfigOrDefault", box (fun () -> createObj [])
                "findWorkspaceEntry", box (System.Func<obj, string, obj>(fun _ _ -> createObj [ "workspace", null ]))
                "resolveAgentFrontmatter", box (System.Func<obj, obj, string, JS.Promise<obj>>(fun _ _ _ -> Promise.lift (createObj [])))
                "getChatHistory", box (System.Func<string, JS.Promise<obj array>>(fun workspaceId -> promise { return if workspaceId = sessionID then history else [||] }))
            ])
    muxActivateReviewForTest reg sessionID "Implement feature X"
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
    do! Promise.sleep 0
    check "active review emits first loop nudge" (nudges.Count = 1 && nudges.[0] = loopNudgePrompt)
    history <-
        Array.append history
            [| muxDynamicToolMessage "review-wip-tool" "submit_review" "wip-call" (createObj []) (box submitReviewWipAcknowledgment) |]
    do! hook $ (streamEnd "continued after wip report", helpers) |> unbox<JS.Promise<unit>>
    do! Promise.sleep 0
    check "wip submit_review does not permanently suppress loop nudge" (nudges.Count = 2 && nudges.[1] = loopNudgePrompt)
}