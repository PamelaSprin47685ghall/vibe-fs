module Wanxiangshu.Tests.IntegrationMuxToolSpecsHooks

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Tests.IntegrationToolSetup
open Wanxiangshu.Tests.IntegrationMuxSetup
open Wanxiangshu.Mux.Plugin
open Wanxiangshu.Shell.Dyn
module Dyn = Wanxiangshu.Shell.Dyn

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

let muxToolSchemasAreCleanStaticallyButInjectedDynamicallySpec () = promise {
    let reg = sharedMuxRegistration ()
    let tools = unbox<obj[]> (get reg "tools")
    let findTool (name: string) =
        tools |> Array.tryFind (fun t -> Dyn.str t "name" = name)
    let staticRequired (toolDef: obj) : string array =
        if isNullish toolDef then [||]
        else
            let params_ = Dyn.get toolDef "parameters"
            if isNullish params_ then [||]
            else
                let req = Dyn.get params_ "required"
                if Dyn.isArray req then unbox<string[]> req else [||]
    let staticProperties (toolDef: obj) : obj =
        if isNullish toolDef then null
        else
            let params_ = Dyn.get toolDef "parameters"
            if isNullish params_ then null else Dyn.get params_ "properties"
    // coder: no warn_tdd in raw static BuiltinTools schema
    let staticCoder = Wanxiangshu.Mux.SubagentTools.coderTool (createObj []) [| "coder" |]
    let staticCoderProps = staticProperties (box staticCoder)
    check "coder static BuiltinTools schema has no warn_tdd" (isNullish (Dyn.get staticCoderProps "warn_tdd"))
    // registered coder: warn_tdd injected into schema properties and required
    let coder = findTool "coder"
    check "coder tool exists" (not (isNullish coder))
    let coderProps = staticProperties coder
    check "registered coder schema has warn_tdd" (not (isNullish (Dyn.get coderProps "warn_tdd")))
    check "registered coder required has warn_tdd" (staticRequired coder |> Array.contains "warn_tdd")
    // executor: no warn or warn_tdd in raw static BuiltinTools schema
    let staticExec = Wanxiangshu.Mux.BuiltinTools.executorTool (createObj []) [| "executor" |] (Wanxiangshu.Shell.RuntimeScope.create ())
    let staticExecProps = staticProperties (box staticExec)
    check "executor static BuiltinTools schema has no warn" (isNullish (Dyn.get staticExecProps "warn"))
    check "executor static BuiltinTools schema has no warn_tdd" (isNullish (Dyn.get staticExecProps "warn_tdd"))
    // registered executor: warn and warn_tdd injected
    let executor = findTool "executor"
    check "executor tool exists" (not (isNullish executor))
    let execProps = staticProperties executor
    check "registered executor schema has warn" (not (isNullish (Dyn.get execProps "warn")))
    check "registered executor schema has warn_tdd" (not (isNullish (Dyn.get execProps "warn_tdd")))
    check "registered executor required has warn" (staticRequired executor |> Array.contains "warn")
    check "registered executor required has warn_tdd" (staticRequired executor |> Array.contains "warn_tdd")
    // write (staticWrite): no warn_tdd in raw BuiltinTools.writeTool schema
    let staticWrite = Wanxiangshu.Mux.BuiltinTools.writeTool (createObj [])
    let staticWriteProps = staticProperties (box staticWrite)
    check "staticWrite has no warn_tdd" (isNullish (Dyn.get staticWriteProps "warn_tdd"))
    // registered write: warn_tdd injected into schema properties and required
    let write = findTool "write"
    check "write tool exists" (not (isNullish write))
    let writeProps = staticProperties write
    check "registered write schema has warn_tdd" (not (isNullish (Dyn.get writeProps "warn_tdd")))
    check "registered write required has warn_tdd" (staticRequired write |> Array.contains "warn_tdd")
    // dynamic injection hook must be present
    let hook = get reg "tool.execute.before"
    check "tool.execute.before hook is present for dynamic warn/warn_tdd injection" (not (isNullish hook))
}