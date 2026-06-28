module Wanxiangshu.Tests.OmpSessionCompactingAnchorTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.OmpPluginTestsHarness
open Wanxiangshu.Omp.Plugin
open Wanxiangshu.Omp.SessionCompacting
open Wanxiangshu.Shell.Dyn
module Dyn = Wanxiangshu.Shell.Dyn

/// Context includes the fixed compaction-anchor body.
let sessionCompactingContextContainsSeeAbove () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! wanxiangshuExtension pi
    let userEntry =
        createObj [
            "id", box "msg-user-1"
            "info", box(createObj [ "role", box "user" ])
            "parts", box [| createObj [ "type", box "text"; "text", box "start work" ] |]
        ]
    let todoEntry =
        createObj [
            "id", box "msg-todo-1"
            "info", box(createObj [ "role", box "assistant" ])
            "parts",
                box [|
                    createObj [
                        "type", box "tool"
                        "tool", box "todowrite"
                        "callID", box "call-tw-1"
                        "state", box(createObj [
                            "status", box "completed"
                            "output", box "Todos updated."
                            "error", box ""
                            "input", box(createObj [ "completedWorkReport", box "Completed task A" ])
                        ])
                    ]
                |]
        ]
    let event = createObj [ "sessionId", box "test-see-above"; "messages", box [| userEntry; todoEntry |] ]
    let! result = sessionCompactingHandler pi event (createObj [ "cwd", box "/tmp" ])
    let context = Dyn.get result "context"
    if Dyn.isArray context then
        let arr = unbox<string array> context
        let joined = System.String.Join("\n", arr)
        check "context contains See above body" (joined.Contains "See above for some messages before compaction.")
}

/// Context contains both the backlog block and extracted front-matter fence strings.
let sessionCompactingContextHasMultipleFrontMatterBlocks () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! wanxiangshuExtension pi
    let userEntry =
        createObj [
            "id", box "msg-user-1"
            "info", box(createObj [ "role", box "user" ])
            "parts",
                box [|
                    createObj [
                        "type", box "text"
                        "text",
                            box "---\ntask: review-login\n---\nStart reviewing login."
                    ]
                |]
        ]
    let todoEntry =
        createObj [
            "id", box "msg-todo-1"
            "info", box(createObj [ "role", box "assistant" ])
            "parts",
                box [|
                    createObj [
                        "type", box "tool"
                        "tool", box "todowrite"
                        "callID", box "call-tw-1"
                        "state", box(createObj [
                            "status", box "completed"
                            "output", box "Todos updated."
                            "error", box ""
                            "input", box(createObj [ "completedWorkReport", box "Completed task A" ])
                        ])
                    ]
                |]
        ]
    let event = createObj [ "sessionId", box "test-multi-blocks"; "messages", box [| userEntry; todoEntry |] ]
    let! result = sessionCompactingHandler pi event (createObj [ "cwd", box "/tmp" ])
    let context = Dyn.get result "context"
    if Dyn.isArray context then
        let arr = unbox<string array> context
        let joined = System.String.Join("\n", arr)
        let fenceCount = joined.Split([| "---" |], System.StringSplitOptions.None).Length - 1
        check "context has backlog + extracted task block" (fenceCount >= 2)
        check "context contains extracted task" (joined.Contains "review-login")
}
