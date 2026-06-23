module VibeFs.Tests.IntegrationMuxReviewSpecs

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Tests.IntegrationToolSetup
open VibeFs.Tests.IntegrationMuxSetup

open VibeFs.Kernel.LoopMessages
open VibeFs.Mux.Plugin
open VibeFs.Shell.Dyn


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

let private reviewActivationHistory (task: string) : obj array =
    [| box (buildLoopMessage task [ "With-Review Mode is active." ]) |]

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
    let reg = createRegistration (muxDepsWithChatHistory sessionID (reviewActivationHistory "Implement feature X"))
    do! preparePendingReviewCallForTest reg workspaceDir sessionID
    let returnTool = muxToolByName reg "return_reviewer"
    if isNullish returnTool then
        check "mux registration exposes return_reviewer tool" false
    else
        let ctx = makeReturnReviewerContext workspaceDir sessionID
        let args = createObj [ "feedback", box "needs rework" ]
        let! result = ((get returnTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
        check "return_reviewer reject reports verdict submitted" (result.Contains "Verdict submitted.")
        let! pending = muxPendingCallIdsForTest reg
        check "return_reviewer reject resolves pending review call" (not (pending |> Array.exists (fun id -> id.StartsWith(sessionID + "-review-"))))
    do! rmAsync workspaceDir
}

let muxReturnReviewerFirstPassDoubleCheckSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-return-reviewer-first-pass-"
    let sessionID = "mux-return-reviewer-first-pass"
    let reg = createRegistration (muxDepsWithChatHistory sessionID (reviewActivationHistory "Implement feature X"))
    do! preparePendingReviewCallForTest reg workspaceDir sessionID
    let returnTool = muxToolByName reg "return_reviewer"
    if isNullish returnTool then
        check "mux registration exposes return_reviewer tool" false
    else
        let ctx = makeReturnReviewerContext workspaceDir sessionID
        let args = createObj [ "feedback", box null ]
        let! result = ((get returnTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
        check "return_reviewer first pass returns double-check prompt" (result.Contains "double-check:")
        let! pending = muxPendingCallIdsForTest reg
        check "return_reviewer first pass does not resolve pending review call" (pending |> Array.exists (fun id -> id.StartsWith(sessionID + "-review-")))
    do! rmAsync workspaceDir
}

let muxReturnReviewerSecondPassResolvesSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-return-reviewer-second-pass-"
    let sessionID = "mux-return-reviewer-second-pass"
    let anchorHistory = Array.append (reviewActivationHistory "Implement feature X") [| box "---\ndouble-check: confirmed\n---\nok" |]
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
        let! pending = muxPendingCallIdsForTest reg
        check "return_reviewer second pass resolves pending review call" (not (pending |> Array.exists (fun id -> id.StartsWith(sessionID + "-review-"))))
    do! rmAsync workspaceDir
}

let muxReturnReviewerRejectKeepsReviewActiveSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-return-reviewer-reject-keeps-"
    let sessionID = "mux-return-reviewer-reject-keeps"
    let reg = createRegistration (muxDepsWithChatHistory sessionID (reviewActivationHistory "Implement feature X"))
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

let muxSubmitReviewTerminatedCleansReviewStateSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-submit-review-terminated-"
    let sessionID = "mux-submit-review-terminated"
    let reg = createRegistration (muxDepsWithChatHistory sessionID (reviewActivationHistory "Implement feature X"))
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
