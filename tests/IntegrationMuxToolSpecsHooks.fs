module Wanxiangshu.Tests.IntegrationMuxToolSpecsHooks

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Tests.IntegrationToolSetup
open Wanxiangshu.Tests.IntegrationMuxSetup
open Wanxiangshu.Mux.Plugin
open Wanxiangshu.Shell.Dyn

let muxEventHookAbortDeactivatesReviewSpec () = promise {
    let reg = sharedMuxRegistration ()
    let sessionID = "mux-abort-session"
    muxActivateReviewForTest reg sessionID "review-task"
    check "mux event hook abort starts with active review" (muxIsReviewActiveForTest reg sessionID)
    let eventHook = get reg "eventHook"
    if isNullish eventHook then
        check "mux registration exposes eventHook" false
    else
        let event = createObj [ "type", box "stream-abort"; "workspaceId", box sessionID ]
        do! (eventHook $ (event, createObj [])) |> unbox<JS.Promise<unit>>
        check "mux event hook abort deactivates review" (not (muxIsReviewActiveForTest reg sessionID))
}

let muxEventHookAbortCleansUpKnowledgeGraphJobSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-kg-abort-cleanup-"
    let reg = sharedMuxRegistration ()
    let sessionID = "mux-kg-abort-session"
    let payload =
        createObj
            [ "summary", box "fix bug"
              "entity", box [| "bug-123" |]
              "fact", box "fixed in commit abc" ]
    registerMuxKnowledgeGraphJobForTest reg sessionID workspaceDir "append" payload
    let eventHook = get reg "eventHook"
    if isNullish eventHook then
        check "mux event hook abort exposes eventHook for KG cleanup" false
    else
        let event = createObj [ "type", box "stream-abort"; "workspaceId", box sessionID ]
        do! (eventHook $ (event, createObj [])) |> unbox<JS.Promise<unit>>
        let runtime = muxKnowledgeGraphRuntime reg
        let hasJob = get runtime "hasJobForTesting" |> unbox<System.Func<string, bool>>
        check "mux event hook abort cleans up knowledge graph job" (not (hasJob.Invoke(sessionID)))
    do! rmAsync workspaceDir
}

let muxToolExecuteBeforeSetsUiLabelSpec () = promise {
    let reg = sharedMuxRegistration ()
    let before = get reg "tool.execute.before"
    check "mux registration exposes tool.execute.before" (not (isNullish before))
    let intentOne =
        createObj
            [ "objective", box "Refactor module X"
              "background", box "cleanup"
              "targets", box [| createObj [ "file", box "src/x.ts"; "guide", box "split file" ] |] ]
    let intentTwo =
        createObj
            [ "objective", box "Add tests"
              "background", box "coverage"
              "targets", box [| createObj [ "file", box "src/x.test.ts"; "guide", box "add cases" ] |] ]
    let args = createObj [ "intents", box [| intentOne; intentTwo |] ]
    let input = createObj [ "tool", box "coder"; "args", box args ]
    do! (before $ (input, createObj [ "args", box args ])) |> unbox<JS.Promise<unit>>
    let ui = str args "_ui"
    check "mux tool.execute.before sets _ui label" (ui.Contains "Refactor module X" && ui.Contains "Add tests")
}

let muxSystemTransformClearsOutputLengthSpec () = promise {
    let reg = sharedMuxRegistration ()
    let transform = get reg "systemTransform"
    check "mux registration exposes systemTransform" (not (isNullish transform))
    let system = createObj [ "content", box "long system prompt"; "length", box 1000 ]
    let output = createObj [ "system", box system ]
    do! (transform $ (createObj [], output)) |> unbox<JS.Promise<unit>>
    check "mux systemTransform preserves system when deps has no directory" ((unbox<int> (get system "length")) = 1000)
}