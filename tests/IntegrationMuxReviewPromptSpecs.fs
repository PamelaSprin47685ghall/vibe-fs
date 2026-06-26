module Wanxiangshu.Tests.IntegrationMuxReviewPromptSpecs

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Tests.IntegrationToolSetup
open Wanxiangshu.Tests.IntegrationMuxSetup

open Wanxiangshu.Mux.Plugin
open Wanxiangshu.Shell.Dyn


let muxSubmitReviewPromptFormatSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-submit-review-prompt-"
    let reg = sharedMuxRegistration ()
    let submitTool = muxToolByName reg "submit_review"
    if isNullish submitTool then
        check "mux registration exposes submit_review tool" false
    else
        let sessionID = "mux-review-prompt"
        muxActivateReviewForTest reg sessionID "Implement feature X"
        let prompts = ResizeArray<string>()
        let taskService = mockMuxTaskServiceReturningVerdicts prompts [ "PASS"; "PASS" ]
        let ctx = createObj [ "directory", box workspaceDir; "workspaceId", box sessionID; "sessionID", box sessionID; "taskService", box taskService ]
        let args = createObj [ "report", box "Changed a.ts"; "affectedFiles", box [| "a.ts" |]; "wip", box false ]
        let! result = ((get submitTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
        let promptText = if prompts.Count > 0 then prompts.[0] else ""
        check "submit_review prompt uses front-matter" (promptText.StartsWith "---")
        check "submit_review prompt carries reviewer role" (promptText.Contains "role: reviewer")
        check "submit_review prompt drops call_id field" (not (promptText.Contains "call_id"))
        check "submit_review prompt does not ask for tool-level callId" (not (promptText.Contains "callId"))
        check "submit_review prompt reuses review criteria" (promptText.Contains "# Evaluation Criteria")
        check "submit_review prompt uses agent_report protocol" (promptText.Contains "agent_report")
        check "submit_review prompt drops legacy divider" (not (promptText.Contains "==="))
        check "submit_review accepts two-round PASS verdict" (result.Contains "verdict: accepted")
    do! rmAsync workspaceDir
}

let muxAgentReportWrapperFormatsVerdictSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-agent-report-wrapper-"
    let reg = sharedMuxRegistration ()
    let wrappers = unbox<obj[]> (get reg "wrappers")
    let agentReportWrapper = wrappers |> Array.tryFind (fun w -> str w "targetTool" = "agent_report") |> Option.defaultValue null
    if isNullish agentReportWrapper then
        check "agent_report wrapper is registered" false
    else
        let capturedUpstream = ResizeArray<obj>()
        let mockAgentReportTool =
            createObj
                [ "execute",
                  box (System.Func<obj, obj, JS.Promise<obj>>(fun upstreamArgs _opts ->
                      promise {
                          capturedUpstream.Add(upstreamArgs)
                          return box {| success = true |}
                      })) ]
        let wrapped = (get agentReportWrapper "wrapper") $ (mockAgentReportTool, createObj [ "subagentRole", box "reviewer" ])
        let wrapperSchema = get wrapped "parameters"
        let required =
            if isNullish wrapperSchema then [||]
            else
                let req = get wrapperSchema "required"
                if isArray req then unbox<string[]> req else [||]
        check "agent_report wrapper schema no longer requires callId" (not (required |> Array.contains "callId"))
        check "agent_report wrapper schema requires verdict" (required |> Array.contains "verdict")
        check "agent_report wrapper schema requires feedback" (required |> Array.contains "feedback")

        let! passResult = (get wrapped "execute") $ (createObj [ "verdict", box "PASS"; "feedback", box "" ], createObj []) |> unbox<JS.Promise<obj>>
        check "agent_report wrapper PASS returns success" (truthy (get passResult "success"))
        let passReport = get passResult "report"
        check "agent_report wrapper PASS attaches PASS markdown" (not (isNullish passReport) && (str passReport "reportMarkdown" = "PASS"))
        check "agent_report wrapper forwards PASS markdown upstream" (capturedUpstream.Count = 1 && (str capturedUpstream.[0] "reportMarkdown" = "PASS"))

        let! rejectResult = (get wrapped "execute") $ (createObj [ "verdict", box "REJECT"; "feedback", box "needs work" ], createObj []) |> unbox<JS.Promise<obj>>
        let rejectReport = get rejectResult "report"
        check "agent_report wrapper REJECT embeds feedback in markdown" (not (isNullish rejectReport) && (str rejectReport "reportMarkdown").Contains "needs work")
        check "agent_report wrapper REJECT markdown starts with REJECT" ((str (capturedUpstream.[1]) "reportMarkdown").StartsWith "REJECT")
    do! rmAsync workspaceDir
}

let muxSubmitReviewUsesRolledBackHistoryTaskSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-submit-review-history-rollback-"
    let sessionID = "mux-submit-review-history-rollback"
    let history = ResizeArray<obj>()
    history.Add(box "---\ntask: First task\n---\nWith-Review Mode is active.")
    let deps = muxMutableDepsWithChatHistory sessionID history
    let reg = createRegistration deps
    let submitTool = muxToolByName reg "submit_review"
    if isNullish submitTool then
        check "mux registration exposes submit_review tool" false
    else
        muxActivateReviewForTest reg sessionID "Second task"
        let prompts = ResizeArray<string>()
        let taskService = mockMuxTaskServiceReturningVerdicts prompts [ "REJECT: not done" ]
        let ctx = createObj [ "directory", box workspaceDir; "workspaceId", box sessionID; "sessionID", box sessionID; "taskService", box taskService ]
        let args = createObj [ "report", box "Changed a.ts"; "affectedFiles", box [| "a.ts" |]; "wip", box false ]
        let! _ = ((get submitTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
        let promptText = if prompts.Count > 0 then prompts.[0] else ""
        check "submit_review uses rolled-back history task instead of stale store task" (promptText.Contains "First task" && not (promptText.Contains "Second task"))
    do! rmAsync workspaceDir
}

let muxLoopReviewPromptUsesFrontMatterSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-loop-review-prompt-"
    let prompts = ResizeArray<string>()
    let deps = minimalMuxDeps ()
    deps?("taskService") <- mockMuxTaskServiceReturningVerdicts prompts [ "PASS" ]
    let reg = createRegistration deps
    let commands = unbox<obj[]> (get reg "slashCommands")
    let loopReview = commands |> Array.find (fun command -> str command "key" = "loop-review")
    let! result = (get loopReview "execute") $ ("mux-loop-review-prompt", "Clarify rollout plan") |> unbox<JS.Promise<string>>
    let promptText = if prompts.Count > 0 then prompts.[0] else ""
    check "loop-review prompt uses front-matter" (promptText.StartsWith "---")
    check "loop-review prompt drops call_id field" (not (promptText.Contains "call_id"))
    check "loop-review prompt does not ask for tool-level callId" (not (promptText.Contains "callId"))
    check "loop-review prompt carries task" (promptText.Contains "task:" && promptText.Contains "Clarify rollout plan")
    check "loop-review prompt reuses review criteria" (promptText.Contains "# Evaluation Criteria")
    check "loop-review prompt uses agent_report protocol" (promptText.Contains "agent_report")
    check "loop-review prompt drops legacy divider" (not (promptText.Contains "==="))
    check "loop-review activates review after pass" (result.Contains "With-Review Mode is active")
    do! rmAsync workspaceDir
}
