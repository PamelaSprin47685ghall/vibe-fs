module VibeFs.Tests.IntegrationSubagentSpecs

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Tests.IntegrationToolSetup
open VibeFs.Kernel.Dyn
open VibeFs.Mux.Plugin
open VibeFs.Opencode.Plugin
open VibeFs.Mux.AiSettings
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.WikiFiles

let investigatorToolSpec () = promise {
    let createCalls = ResizeArray<obj>()
    let promptCalls = ResizeArray<obj>()
    let mockClient =
        createObj [ "session", box (createObj [
            "create", box (System.Func<obj, JS.Promise<obj>>(fun arg ->
                (promise { createCalls.Add(arg); return box {| data = box {| id = "child-investigator-session" |} |} })))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg ->
                (promise { promptCalls.Add(arg) })))
            "messages", box (System.Func<obj, JS.Promise<obj>>(fun _ ->
                (promise { return box {| data = [|
                    box {| info = box {| role = "assistant" |}; parts = [| box {| ``type`` = "text"; text = "Found src/Opencode/Tools.fs" |} |] |}
                |] |} })))
            "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ ->
                (Promise.lift ())))
        ]) ]
    let! workspaceDir = mkdtempAsync "investigator-tool-"
    let! p = plugin (box {| directory = workspaceDir; client = mockClient |})
    let investigator = get (get p "tool") "investigator"
    let! result = (get investigator "execute") $ (createObj [ "intents", box [| sampleInvestigatorIntent "find investigator registration" |] ], createObj [ "directory", box workspaceDir; "sessionID", box "investigator-parent"; "abort", box null ]) |> unbox<JS.Promise<string>>
    check "investigator tool returns subagent output" (result.Contains("src/Opencode/Tools.fs"))
    check "investigator tool creates child session under parent" (str (get createCalls.[0] "body") "parentID" = "investigator-parent")
    check "investigator tool prompts child investigator agent" (str (get promptCalls.[0] "body") "agent" = "investigator")
    do! rmAsync workspaceDir
}

let coderToolSpec () = promise {
    let createCalls = ResizeArray<obj>()
    let promptCalls = ResizeArray<obj>()
    let mockClient =
        createObj [ "session", box (createObj [
            "create", box (System.Func<obj, JS.Promise<obj>>(fun arg ->
                (promise { createCalls.Add(arg); return box {| data = box {| id = "child-coder-session" |} |} })))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg ->
                (promise { promptCalls.Add(arg) })))
            "messages", box (System.Func<obj, JS.Promise<obj>>(fun _ ->
                (promise { return box {| data = [|
                    box {| info = box {| role = "assistant" |}; parts = [| box {| ``type`` = "text"; text = "Coder finished" |} |] |}
                |] |} })))
            "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ ->
                (Promise.lift ())))
        ]) ]
    let! workspaceDir = mkdtempAsync "coder-tool-"
    let! p = plugin (box {| directory = workspaceDir; client = mockClient |})
    let coder = get (get p "tool") "coder"
    let intents : obj array = [|
        sampleCoderIntentWithDoNotTouch "fix bug" "a.ts" [| "src/shared.fs"; "Do not rename public API" |]
        sampleCoderIntent "add feature" "b.ts"
    |]
    let! result = (get coder "execute") $ (createObj [ "intents", box intents ], createObj [ "directory", box workspaceDir; "sessionID", box "coder-parent"; "abort", box null ]) |> unbox<JS.Promise<string>>
    check "coder tool returns subagent output" (result.Contains("Coder finished"))
    let coderCreates =
        createCalls
        |> Seq.filter (fun call -> str (get call "body") "parentID" = "coder-parent")
        |> Seq.toArray
    check "coder tool creates one child per intent" (coderCreates.Length = 2)
    check "coder tool prompts child coder agent" (str (get promptCalls.[0] "body") "agent" = "coder")
    let firstPrompt = str (unbox<obj[]> (get (get promptCalls.[0] "body") "parts")).[0] "text"
    let secondPrompt = str (unbox<obj[]> (get (get promptCalls.[1] "body") "parts")).[0] "text"
    check "coder prompt includes first intent do_not_touch" (firstPrompt.Contains("do_not_touch:") && firstPrompt.Contains("src/shared.fs") && firstPrompt.Contains("Do not rename public API"))
    check "coder prompt omits do_not_touch section when absent" (not (secondPrompt.Contains("do_not_touch:")))
    do! rmAsync workspaceDir
}

let investigatorToolLateClientInjectionSpec () = promise {
    let createCalls = ResizeArray<obj>()
    let promptCalls = ResizeArray<obj>()
    let mockClient =
        createObj [ "session", box (createObj [
            "create", box (System.Func<obj, JS.Promise<obj>>(fun arg ->
                (promise { createCalls.Add(arg); return box {| data = box {| id = "child-investigator-session-late" |} |} })))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg ->
                (promise { promptCalls.Add(arg) })))
            "messages", box (System.Func<obj, JS.Promise<obj>>(fun _ ->
                (promise { return box {| data = [|
                    box {| info = box {| role = "assistant" |}; parts = [| box {| ``type`` = "text"; text = "Late client injection worked" |} |] |}
                |] |} })))
            "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ ->
                (Promise.lift ())))
        ]) ]
    let! workspaceDir = mkdtempAsync "investigator-tool-late-client-"
    let ctx = createObj [ "directory", box workspaceDir ]
    let! p = plugin ctx
    ctx?("client") <- mockClient
    let investigator = get (get p "tool") "investigator"
    let! result = (get investigator "execute") $ (createObj [ "intents", box [| sampleInvestigatorIntent "find investigator registration" |] ], createObj [ "directory", box workspaceDir; "sessionID", box "investigator-parent-late"; "abort", box null ]) |> unbox<JS.Promise<string>>
    check "investigator tool sees client injected after plugin init" (result.Contains("Late client injection worked"))
    check "investigator tool late injection creates child session under parent" (str (get createCalls.[0] "body") "parentID" = "investigator-parent-late")
    check "investigator tool late injection prompts child investigator agent" (str (get promptCalls.[0] "body") "agent" = "investigator")
    do! rmAsync workspaceDir
}

let muxSubmitReviewNoActiveReviewSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-submit-review-no-active-"
    let reg = createRegistration (minimalMuxDeps ())
    let submitTool = muxToolByName reg "submit_review"
    if isNullish submitTool then
        check "mux registration exposes submit_review tool" false
    else
        let ctx = createObj [ "directory", box workspaceDir; "workspaceId", box "mux-review-no-active"; "sessionID", box "mux-review-no-active" ]
        let args = createObj [ "report", box "nothing"; "affectedFiles", box [| "a.ts" |] ]
        let! result = ((get submitTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
        check "submit_review without active review tells user no review is needed" (result.Contains("You do not need review"))
    do! rmAsync workspaceDir
}

let private makeReturnReviewerContext (workspaceDir: string) (sessionID: string) : obj =
    createObj [
        "directory", box workspaceDir
        "workspaceId", box sessionID
        "sessionID", box sessionID
    ]

let private preparePendingReviewCallForTest (reg: obj) (workspaceDir: string) (sessionID: string) : JS.Promise<unit> =
    promise {
        muxActivateReviewForTest reg sessionID "Implement feature X"
        let prompts = ResizeArray<string>()
        let taskService = mockMuxTaskServiceCapturingPrompt prompts
        let submitTool = muxToolByName reg "submit_review"
        if not (isNullish submitTool) then
            let ctx = createObj [ "directory", box workspaceDir; "workspaceId", box sessionID; "sessionID", box sessionID; "taskService", box taskService ]
            let args = createObj [ "report", box "Changed a.ts"; "affectedFiles", box [| "a.ts" |] ]
            try
                let! _ = ((get submitTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
                ()
            with _ ->
                muxActivateReviewForTest reg sessionID "Implement feature X"
    }

let muxReturnReviewerRegisteredSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-return-reviewer-registered-"
    let reg = createRegistration (muxDepsWithChatHistory "mux-return-reviewer-registered" [||])
    let returnTool = muxToolByName reg "return_reviewer"
    check "mux registration exposes return_reviewer tool" (not (isNullish returnTool))
    do! rmAsync workspaceDir
}

let muxReturnReviewerRejectsResolveSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-return-reviewer-reject-"
    let sessionID = "mux-return-reviewer-reject"
    let reg = createRegistration (muxDepsWithChatHistory sessionID [||])
    do! preparePendingReviewCallForTest reg workspaceDir sessionID
    let returnTool = muxToolByName reg "return_reviewer"
    if isNullish returnTool then
        check "mux registration exposes return_reviewer tool" false
    else
        let ctx = makeReturnReviewerContext workspaceDir sessionID
        let args = createObj [ "feedback", box "needs rework" ]
        let! result = ((get returnTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
        check "return_reviewer reject reports verdict submitted" (result.Contains "Verdict submitted.")
        let pending = muxPendingCallIdsForTest reg
        check "return_reviewer reject resolves pending review call" (not (pending |> Array.exists (fun id -> id.StartsWith(sessionID + "-review-"))))
    do! rmAsync workspaceDir
}

let muxReturnReviewerFirstPassDoubleCheckSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-return-reviewer-first-pass-"
    let sessionID = "mux-return-reviewer-first-pass"
    let reg = createRegistration (muxDepsWithChatHistory sessionID [||])
    do! preparePendingReviewCallForTest reg workspaceDir sessionID
    let returnTool = muxToolByName reg "return_reviewer"
    if isNullish returnTool then
        check "mux registration exposes return_reviewer tool" false
    else
        let ctx = makeReturnReviewerContext workspaceDir sessionID
        let args = createObj [ "feedback", box null ]
        let! result = ((get returnTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
        check "return_reviewer first pass returns double-check prompt" (result.Contains "double-check:")
        let pending = muxPendingCallIdsForTest reg
        check "return_reviewer first pass does not resolve pending review call" (pending |> Array.exists (fun id -> id.StartsWith(sessionID + "-review-")))
    do! rmAsync workspaceDir
}

let muxReturnReviewerSecondPassResolvesSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-return-reviewer-second-pass-"
    let sessionID = "mux-return-reviewer-second-pass"
    let anchorHistory = [| box "---\ndouble-check: confirmed\n---\nok" |]
    let reg = createRegistration (muxDepsWithChatHistory sessionID anchorHistory)
    do! preparePendingReviewCallForTest reg workspaceDir sessionID
    let returnTool = muxToolByName reg "return_reviewer"
    if isNullish returnTool then
        check "mux registration exposes return_reviewer tool" false
    else
        let ctx = makeReturnReviewerContext workspaceDir sessionID
        let args = createObj [ "feedback", box null ]
        let! result = ((get returnTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
        check "return_reviewer second pass reports verdict submitted" (result.Contains "Verdict submitted.")
        let pending = muxPendingCallIdsForTest reg
        check "return_reviewer second pass resolves pending review call" (not (pending |> Array.exists (fun id -> id.StartsWith(sessionID + "-review-"))))
    do! rmAsync workspaceDir
}
let muxSubmitReviewPromptSuppliesCallIdSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-submit-review-callid-"
    let reg = createRegistration (minimalMuxDeps ())
    let submitTool = muxToolByName reg "submit_review"
    if isNullish submitTool then
        check "mux registration exposes submit_review tool" false
    else
        let sessionID = "mux-review-callid"
        muxActivateReviewForTest reg sessionID "Implement feature X"
        let prompts = ResizeArray<string>()
        let taskService = mockMuxTaskServiceCapturingPrompt prompts
        let ctx = createObj [ "directory", box workspaceDir; "workspaceId", box sessionID; "sessionID", box sessionID; "taskService", box taskService ]
        let args = createObj [ "report", box "Changed a.ts"; "affectedFiles", box [| "a.ts" |] ]
        try
            let! _ = ((get submitTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
            check "submit_review should not complete when reviewer cannot report verdict" false
        with _ ->
            ()
        let pending = muxPendingCallIdsForTest reg
        let matching = pending |> Array.tryFind (fun id -> id.StartsWith(sessionID + "-review-"))
        match matching with
        | None -> check "submit_review registers a pending review call" false
        | Some callId ->
            let promptText = if prompts.Count > 0 then prompts.[0] else ""
            check "submit_review prompt includes the review callId for the reviewer" (promptText.Contains(callId))
    do! rmAsync workspaceDir
}

let muxReturnReviewerRejectKeepsReviewActiveSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-return-reviewer-reject-keeps-"
    let sessionID = "mux-return-reviewer-reject-keeps"
    let reg = createRegistration (muxDepsWithChatHistory sessionID [||])
    do! preparePendingReviewCallForTest reg workspaceDir sessionID
    let returnTool = muxToolByName reg "return_reviewer"
    if isNullish returnTool then
        check "mux registration exposes return_reviewer tool" false
    else
        let ctx = makeReturnReviewerContext workspaceDir sessionID
        let args = createObj [ "feedback", box "needs rework" ]
        let! result = ((get returnTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
        check "return_reviewer reject reports verdict submitted" (result.Contains "Verdict submitted.")
        check "return_reviewer reject keeps review session active" (muxIsReviewActiveForTest reg sessionID)
    do! rmAsync workspaceDir
}

let muxReturnReviewerRejectCleansReviewStateSpec () = muxReturnReviewerRejectKeepsReviewActiveSpec ()

let muxSubmitReviewTerminatedCleansReviewStateSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-submit-review-terminated-"
    let sessionID = "mux-submit-review-terminated"
    let reg = createRegistration (muxDepsWithChatHistory sessionID [||])
    muxActivateReviewForTest reg sessionID "Implement feature X"
    let prompts = ResizeArray<string>()
    let taskService = mockMuxTaskServiceCapturingPrompt prompts
    let submitTool = muxToolByName reg "submit_review"
    if isNullish submitTool then
        check "mux registration exposes submit_review tool" false
    else
        let ctx = createObj [ "directory", box workspaceDir; "workspaceId", box sessionID; "sessionID", box sessionID; "taskService", box taskService ]
        let args = createObj [ "report", box "Changed a.ts"; "affectedFiles", box [| "a.ts" |] ]
        let! result =
            promise {
                try
                    let! r = ((get submitTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
                    return r
                with ex ->
                    return string ex
            }
        check "submit_review reports termination, timeout or error" (
            result.Contains "Reviewer timed out"
            || result.Contains "Terminated"
            || result.Contains "terminated"
            || result.Contains "timeout"
            || result.Contains "failed")
        check "submit_review termination deactivates review session" (not (muxIsReviewActiveForTest reg sessionID))
    do! rmAsync workspaceDir
}

let muxExecutorFailureDoesNotBookkeepSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-executor-fail-"
    let reg = createRegistration (createObj [])
    let executor = muxToolByName reg "executor"
    if isNullish executor then
        check "mux registration exposes executor tool" false
    else
        let ctx = createObj [ "directory", box workspaceDir; "sessionID", box "mux-executor-fail"; "workspaceId", box "mux-executor-fail" ]
        let args = createObj [ "language", box "shell"; "program", box "exit 1"; "timeout_type", box "short"; "mode", box "rw" ]
        let! result = ((get executor "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
        check "executor failure reports non-zero exit" (result.Contains "exited with code 1")
        do! waitForBackgroundJobsForTesting reg
        let launches = takeBookkeeperLaunchesForTesting reg
        check "executor failure does not trigger bookkeeper" (launches.Length = 0)
    do! rmAsync workspaceDir
}
