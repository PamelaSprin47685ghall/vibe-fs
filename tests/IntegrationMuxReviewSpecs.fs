module Wanxiangshu.Tests.IntegrationMuxReviewSpecs

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Tests.EventLogTestSeed
open Wanxiangshu.Tests.IntegrationToolSetup
open Wanxiangshu.Tests.IntegrationMuxSetup

open Wanxiangshu.Runtime.LoopMessages
open Wanxiangshu.Runtime.ReviewPrompts
open Wanxiangshu.Hosts.Mux.Plugin
open Wanxiangshu.Runtime.Dyn


let muxSubmitReviewNoActiveReviewSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "mux-submit-review-no-active-"
        let reg = sharedMuxRegistration ()
        let submitTool = muxToolByName reg "submit_review"

        if isNullish submitTool then
            check "mux registration exposes submit_review tool" false
        else
            let ctx =
                createObj
                    [ "directory", box workspaceDir
                      "workspaceId", box "mux-review-no-active"
                      "sessionID", box "mux-review-no-active" ]

            let args = createObj [ "report", box "nothing"; "affectedFiles", box [| "a.ts" |] ]
            let! result = ((get submitTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>

            check
                "submit_review without active review tells user no review is needed"
                (result.Contains("You do not need review"))

        do! rmAsync workspaceDir
    }

let private reviewActivationHistory (task: string) : obj array =
    [| box (buildLoopMessage task [ "With-Review Mode is active." ]) |]

/// submit_review only ends With-Review Mode after BOTH rounds accept: round1 PERFECT
/// triggers the double-check round, and only a second PERFECT finalizes Accepted.
let muxSubmitReviewTwoRoundPassAcceptsSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "mux-submit-review-two-pass-"
        let sessionID = "mux-submit-review-two-pass"
        do! seedLoopActivated workspaceDir sessionID "Implement feature X"

        let seams =
            createRegistrationWithSeams (
                muxDepsWithChatHistory sessionID (reviewActivationHistory "Implement feature X")
            )

        let reg = seams.Registration
        let reviewStore = seams.ReviewStore

        let prompts = ResizeArray<string>()

        let taskService =
            mockMuxTaskServiceReturningVerdicts prompts [ "PERFECT"; "PERFECT" ]

        let submitTool = muxToolByName reg "submit_review"

        let ctx =
            createObj
                [ "directory", box workspaceDir
                  "workspaceId", box sessionID
                  "sessionID", box sessionID
                  "taskService", box taskService ]

        let args =
            createObj
                [ "report", box "Changed a.ts"
                  "affectedFiles", box [| "a.ts" |]
                  "wip", box false ]

        let! result = ((get submitTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
        check "submit_review two PERFECT rounds reports accepted" (result.Contains "verdict: accepted")
        check "submit_review runs a double-check round" (prompts.Count = 2)
        check "submit_review double-check round carries anchor" (prompts.[1].Contains "double-check:")

        check
            "submit_review two PERFECT rounds deactivates review"
            (not (muxIsReviewActiveForTest reviewStore sessionID))

        do! rmAsync workspaceDir
    }

/// A round1 REVISE finalizes immediately with feedback and keeps the review
/// active so the worker can address it; no double-check round runs.
let muxSubmitReviewReviseKeepsReviewActiveSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "mux-submit-review-revise-"
        let sessionID = "mux-submit-review-revise"
        do! seedLoopActivated workspaceDir sessionID "Implement feature X"

        let seams =
            createRegistrationWithSeams (
                muxDepsWithChatHistory sessionID (reviewActivationHistory "Implement feature X")
            )

        let reg = seams.Registration
        let reviewStore = seams.ReviewStore

        let prompts = ResizeArray<string>()

        let taskService =
            mockMuxTaskServiceReturningVerdicts prompts [ "REVISE: missing tests" ]

        let submitTool = muxToolByName reg "submit_review"

        let ctx =
            createObj
                [ "directory", box workspaceDir
                  "workspaceId", box sessionID
                  "sessionID", box sessionID
                  "taskService", box taskService ]

        let args =
            createObj
                [ "report", box "Changed a.ts"
                  "affectedFiles", box [| "a.ts" |]
                  "wip", box false ]

        let! result = ((get submitTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
        check "submit_review revise reports needs_revision verdict" (result.Contains "verdict: needs_revision")
        check "submit_review revise surfaces feedback" (result.Contains "missing tests")
        check "submit_review revise runs only one round" (prompts.Count = 1)
        check "submit_review revise keeps review session active" (muxIsReviewActiveForTest reviewStore sessionID)
        do! rmAsync workspaceDir
    }

/// A round1 PERFECT followed by a double-check REVISE finalizes needs_revision and keeps
/// the review active — the skeptical second round can still catch corner-cutting.
let muxSubmitReviewDoubleCheckReviseSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "mux-submit-review-double-revise-"
        let sessionID = "mux-submit-review-double-revise"
        do! seedLoopActivated workspaceDir sessionID "Implement feature X"

        let seams =
            createRegistrationWithSeams (
                muxDepsWithChatHistory sessionID (reviewActivationHistory "Implement feature X")
            )

        let reg = seams.Registration
        let reviewStore = seams.ReviewStore

        let prompts = ResizeArray<string>()

        let taskService =
            mockMuxTaskServiceReturningVerdicts prompts [ "PERFECT"; "REVISE: cut corners on edge cases" ]

        let submitTool = muxToolByName reg "submit_review"

        let ctx =
            createObj
                [ "directory", box workspaceDir
                  "workspaceId", box sessionID
                  "sessionID", box sessionID
                  "taskService", box taskService ]

        let args =
            createObj
                [ "report", box "Changed a.ts"
                  "affectedFiles", box [| "a.ts" |]
                  "wip", box false ]

        let! result = ((get submitTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
        check "submit_review double-check revise reports needs_revision" (result.Contains "verdict: needs_revision")
        check "submit_review double-check revise surfaces feedback" (result.Contains "cut corners")
        check "submit_review double-check revise ran two rounds" (prompts.Count = 2)
        check "submit_review double-check revise keeps review active" (muxIsReviewActiveForTest reviewStore sessionID)
        do! rmAsync workspaceDir
    }

/// An unrecognized reviewer report parses to Terminated, which deactivates the
/// review (no clear verdict means the loop cannot safely continue gating).
let muxSubmitReviewTerminatedCleansReviewStateSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "mux-submit-review-terminated-"
        let sessionID = "mux-submit-review-terminated"
        do! seedLoopActivated workspaceDir sessionID "Implement feature X"

        let seams =
            createRegistrationWithSeams (
                muxDepsWithChatHistory sessionID (reviewActivationHistory "Implement feature X")
            )

        let reg = seams.Registration
        let reviewStore = seams.ReviewStore

        let prompts = ResizeArray<string>()

        let taskService =
            mockMuxTaskServiceReturningVerdicts prompts [ "I think it looks fine" ]

        let submitTool = muxToolByName reg "submit_review"

        let ctx =
            createObj
                [ "directory", box workspaceDir
                  "workspaceId", box sessionID
                  "sessionID", box sessionID
                  "taskService", box taskService ]

        let args =
            createObj
                [ "report", box "Changed a.ts"
                  "affectedFiles", box [| "a.ts" |]
                  "wip", box false ]

        let! result = ((get submitTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
        check "submit_review unclear report reports terminated" (result.Contains "verdict: terminated")
        check "submit_review termination keeps review session active" (muxIsReviewActiveForTest reviewStore sessionID)
        do! rmAsync workspaceDir
    }

/// Omitted wip defaults to progress-only; no reviewer is spawned.
let muxSubmitReviewOmittedWipSkipsReviewerSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "mux-submit-review-omitted-wip-"
        let sessionID = "mux-submit-review-omitted-wip"
        do! seedLoopActivated workspaceDir sessionID "Implement feature X"

        let seams =
            createRegistrationWithSeams (
                muxDepsWithChatHistory sessionID (reviewActivationHistory "Implement feature X")
            )

        let reg = seams.Registration
        let reviewStore = seams.ReviewStore

        let prompts = ResizeArray<string>()
        let taskService = mockMuxTaskServiceReturningVerdicts prompts []
        let submitTool = muxToolByName reg "submit_review"

        let ctx =
            createObj
                [ "directory", box workspaceDir
                  "workspaceId", box sessionID
                  "sessionID", box sessionID
                  "taskService", box taskService ]

        let args =
            createObj [ "report", box "Partial progress"; "affectedFiles", box [| "a.ts" |] ]

        let! result = ((get submitTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>

        check
            "submit_review omitted wip returns kernel acknowledgment"
            (result = formatWipAcknowledgment "Implement feature X")

        check "submit_review omitted wip does not delegate to reviewer" (prompts.Count = 0)
        check "submit_review omitted wip keeps review session active" (muxIsReviewActiveForTest reviewStore sessionID)
        do! rmAsync workspaceDir
    }

/// wip=true records progress without spawning a reviewer; With-Review Mode stays on.
let muxSubmitReviewWipSkipsReviewerSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "mux-submit-review-wip-"
        let sessionID = "mux-submit-review-wip"
        do! seedLoopActivated workspaceDir sessionID "Implement feature X"

        let seams =
            createRegistrationWithSeams (
                muxDepsWithChatHistory sessionID (reviewActivationHistory "Implement feature X")
            )

        let reg = seams.Registration
        let reviewStore = seams.ReviewStore

        let prompts = ResizeArray<string>()
        let taskService = mockMuxTaskServiceReturningVerdicts prompts []
        let submitTool = muxToolByName reg "submit_review"

        let ctx =
            createObj
                [ "directory", box workspaceDir
                  "workspaceId", box sessionID
                  "sessionID", box sessionID
                  "taskService", box taskService ]

        let args =
            createObj
                [ "report", box "Partial progress"
                  "affectedFiles", box [| "a.ts" |]
                  "wip", box true ]

        let! result = ((get submitTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
        check "submit_review wip returns kernel acknowledgment" (result = formatWipAcknowledgment "Implement feature X")
        check "submit_review wip does not delegate to reviewer" (prompts.Count = 0)
        check "submit_review wip keeps review session active" (muxIsReviewActiveForTest reviewStore sessionID)
        do! rmAsync workspaceDir
    }
