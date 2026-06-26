module Wanxiangshu.Tests.IntegrationMuxReviewSpecs

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Tests.IntegrationToolSetup
open Wanxiangshu.Tests.IntegrationMuxSetup

open Wanxiangshu.Kernel.LoopMessages
open Wanxiangshu.Kernel.ReviewPrompts
open Wanxiangshu.Mux.Plugin
open Wanxiangshu.Shell.Dyn


let muxSubmitReviewNoActiveReviewSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-submit-review-no-active-"
    let reg = sharedMuxRegistration ()
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

let private reviewActivationHistory (task: string) : obj array =
    [| box (buildLoopMessage task [ "With-Review Mode is active." ]) |]

/// submit_review only ends With-Review Mode after BOTH rounds pass: round1 PASS
/// triggers the double-check round, and only a second PASS finalizes Accepted.
let muxSubmitReviewTwoRoundPassAcceptsSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-submit-review-two-pass-"
    let sessionID = "mux-submit-review-two-pass"
    let reg = createRegistration (muxDepsWithChatHistory sessionID (reviewActivationHistory "Implement feature X"))
    muxActivateReviewForTest reg sessionID "Implement feature X"
    let prompts = ResizeArray<string>()
    let taskService = mockMuxTaskServiceReturningVerdicts prompts [ "PASS"; "PASS" ]
    let submitTool = muxToolByName reg "submit_review"
    let ctx = createObj [ "directory", box workspaceDir; "workspaceId", box sessionID; "sessionID", box sessionID; "taskService", box taskService ]
    let args = createObj [ "report", box "Changed a.ts"; "affectedFiles", box [| "a.ts" |]; "wip", box false ]
    let! result = ((get submitTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
    check "submit_review two PASS rounds reports accepted" (result.Contains "verdict: accepted")
    check "submit_review runs a double-check round" (prompts.Count = 2)
    check "submit_review double-check round carries anchor" (prompts.[1].Contains "double-check:")
    check "submit_review two PASS rounds deactivates review" (not (muxIsReviewActiveForTest reg sessionID))
    do! rmAsync workspaceDir
}

/// A round1 REJECT finalizes immediately with feedback and keeps the review
/// active so the worker can address it; no double-check round runs.
let muxSubmitReviewRejectKeepsReviewActiveSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-submit-review-reject-"
    let sessionID = "mux-submit-review-reject"
    let reg = createRegistration (muxDepsWithChatHistory sessionID (reviewActivationHistory "Implement feature X"))
    muxActivateReviewForTest reg sessionID "Implement feature X"
    let prompts = ResizeArray<string>()
    let taskService = mockMuxTaskServiceReturningVerdicts prompts [ "REJECT: missing tests" ]
    let submitTool = muxToolByName reg "submit_review"
    let ctx = createObj [ "directory", box workspaceDir; "workspaceId", box sessionID; "sessionID", box sessionID; "taskService", box taskService ]
    let args = createObj [ "report", box "Changed a.ts"; "affectedFiles", box [| "a.ts" |]; "wip", box false ]
    let! result = ((get submitTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
    check "submit_review reject reports rejected verdict" (result.Contains "verdict: rejected")
    check "submit_review reject surfaces feedback" (result.Contains "missing tests")
    check "submit_review reject runs only one round" (prompts.Count = 1)
    check "submit_review reject keeps review session active" (muxIsReviewActiveForTest reg sessionID)
    do! rmAsync workspaceDir
}

/// A round1 PASS followed by a double-check REJECT finalizes rejected and keeps
/// the review active — the skeptical second round can still catch corner-cutting.
let muxSubmitReviewDoubleCheckRejectSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-submit-review-double-reject-"
    let sessionID = "mux-submit-review-double-reject"
    let reg = createRegistration (muxDepsWithChatHistory sessionID (reviewActivationHistory "Implement feature X"))
    muxActivateReviewForTest reg sessionID "Implement feature X"
    let prompts = ResizeArray<string>()
    let taskService = mockMuxTaskServiceReturningVerdicts prompts [ "PASS"; "REJECT: cut corners on edge cases" ]
    let submitTool = muxToolByName reg "submit_review"
    let ctx = createObj [ "directory", box workspaceDir; "workspaceId", box sessionID; "sessionID", box sessionID; "taskService", box taskService ]
    let args = createObj [ "report", box "Changed a.ts"; "affectedFiles", box [| "a.ts" |]; "wip", box false ]
    let! result = ((get submitTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
    check "submit_review double-check reject reports rejected" (result.Contains "verdict: rejected")
    check "submit_review double-check reject surfaces feedback" (result.Contains "cut corners")
    check "submit_review double-check reject ran two rounds" (prompts.Count = 2)
    check "submit_review double-check reject keeps review active" (muxIsReviewActiveForTest reg sessionID)
    do! rmAsync workspaceDir
}

/// An unrecognized reviewer report parses to Terminated, which deactivates the
/// review (no clear verdict means the loop cannot safely continue gating).
let muxSubmitReviewTerminatedCleansReviewStateSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-submit-review-terminated-"
    let sessionID = "mux-submit-review-terminated"
    let reg = createRegistration (muxDepsWithChatHistory sessionID (reviewActivationHistory "Implement feature X"))
    muxActivateReviewForTest reg sessionID "Implement feature X"
    let prompts = ResizeArray<string>()
    let taskService = mockMuxTaskServiceReturningVerdicts prompts [ "I think it looks fine" ]
    let submitTool = muxToolByName reg "submit_review"
    let ctx = createObj [ "directory", box workspaceDir; "workspaceId", box sessionID; "sessionID", box sessionID; "taskService", box taskService ]
    let args = createObj [ "report", box "Changed a.ts"; "affectedFiles", box [| "a.ts" |]; "wip", box false ]
    let! result = ((get submitTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
    check "submit_review unclear report reports terminated" (result.Contains "verdict: terminated")
    check "submit_review termination deactivates review session" (not (muxIsReviewActiveForTest reg sessionID))
    do! rmAsync workspaceDir
}

/// Omitted wip defaults to progress-only; no reviewer is spawned.
let muxSubmitReviewOmittedWipSkipsReviewerSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-submit-review-omitted-wip-"
    let sessionID = "mux-submit-review-omitted-wip"
    let reg = createRegistration (muxDepsWithChatHistory sessionID (reviewActivationHistory "Implement feature X"))
    muxActivateReviewForTest reg sessionID "Implement feature X"
    let prompts = ResizeArray<string>()
    let taskService = mockMuxTaskServiceReturningVerdicts prompts []
    let submitTool = muxToolByName reg "submit_review"
    let ctx = createObj [ "directory", box workspaceDir; "workspaceId", box sessionID; "sessionID", box sessionID; "taskService", box taskService ]
    let args = createObj [ "report", box "Partial progress"; "affectedFiles", box [| "a.ts" |] ]
    let! result = ((get submitTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
    check "submit_review omitted wip returns kernel acknowledgment" (result = submitReviewWipAcknowledgment)
    check "submit_review omitted wip does not delegate to reviewer" (prompts.Count = 0)
    check "submit_review omitted wip keeps review session active" (muxIsReviewActiveForTest reg sessionID)
    do! rmAsync workspaceDir
}

/// wip=true records progress without spawning a reviewer; With-Review Mode stays on.
let muxSubmitReviewWipSkipsReviewerSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-submit-review-wip-"
    let sessionID = "mux-submit-review-wip"
    let reg = createRegistration (muxDepsWithChatHistory sessionID (reviewActivationHistory "Implement feature X"))
    muxActivateReviewForTest reg sessionID "Implement feature X"
    let prompts = ResizeArray<string>()
    let taskService = mockMuxTaskServiceReturningVerdicts prompts []
    let submitTool = muxToolByName reg "submit_review"
    let ctx = createObj [ "directory", box workspaceDir; "workspaceId", box sessionID; "sessionID", box sessionID; "taskService", box taskService ]
    let args = createObj [ "report", box "Partial progress"; "affectedFiles", box [| "a.ts" |]; "wip", box true ]
    let! result = ((get submitTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
    check "submit_review wip returns kernel acknowledgment" (result = submitReviewWipAcknowledgment)
    check "submit_review wip does not delegate to reviewer" (prompts.Count = 0)
    check "submit_review wip keeps review session active" (muxIsReviewActiveForTest reg sessionID)
    do! rmAsync workspaceDir
}

let muxExecutorFailureDoesNotBookkeepSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-executor-fail-"
    let reg = sharedMuxRegistration ()
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
