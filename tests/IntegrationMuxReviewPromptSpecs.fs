module Wanxiangshu.Tests.IntegrationMuxReviewPromptSpecs

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TestWorkspace
open Wanxiangshu.Tests.EventLogTestSeed
open Wanxiangshu.Tests.IntegrationToolSetup
open Wanxiangshu.Tests.IntegrationMuxSetup

open Wanxiangshu.Runtime.LoopMessages
open Wanxiangshu.Hosts.Mux.Plugin
open Wanxiangshu.Runtime.Dyn


let muxSubmitReviewPromptFormatSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "mux-submit-review-prompt-"
        let sessionID = "mux-review-prompt"
        do! seedLoopActivated workspaceDir sessionID "Implement feature X"

        let reg =
            createRegistration (
                muxDepsWithChatHistory
                    sessionID
                    [| box (buildLoopMessage "Implement feature X" [ "With-Review Mode is active." ]) |]
            )

        let submitTool = muxToolByName reg "submit_review"

        if isNullish submitTool then
            check "mux registration exposes submit_review tool" false
        else
            let prompts = ResizeArray<string>()

            let taskService =
                mockMuxTaskServiceReturningVerdicts prompts [ "PERFECT"; "PERFECT" ]

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
            let promptText = if prompts.Count > 0 then prompts.[0] else ""
            check "submit_review prompt uses front-matter" (promptText.StartsWith "---")
            check "submit_review prompt drops role field" (not (promptText.Contains "role:"))
            check "submit_review prompt drops call_id field" (not (promptText.Contains "call_id"))
            check "submit_review prompt does not ask for tool-level callId" (not (promptText.Contains "callId"))
            check "submit_review prompt reuses review criteria" (promptText.Contains "# Evaluation Criteria")
            check "submit_review prompt uses agent_report protocol" (promptText.Contains "agent_report")
            check "submit_review prompt drops legacy divider" (not (promptText.Contains "==="))
            check "submit_review accepts two-round PERFECT verdict" (result.Contains "verdict: accepted")

        do! rmAsync workspaceDir
    }

let muxAgentReportWrapperFormatsVerdictSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "mux-agent-report-wrapper-"
        let reg = sharedMuxRegistration ()
        let wrappers = unbox<obj[]> (get reg "wrappers")

        let agentReportWrapper =
            wrappers
            |> Array.tryFind (fun w -> str w "targetTool" = "agent_report")
            |> Option.defaultValue null

        if isNullish agentReportWrapper then
            check "agent_report wrapper is registered" false
        else
            let capturedUpstream = ResizeArray<obj>()

            let mockAgentReportTool =
                createObj
                    [ "execute",
                      box (
                          System.Func<obj, obj, JS.Promise<obj>>(fun upstreamArgs _opts ->
                              promise {
                                  capturedUpstream.Add(upstreamArgs)
                                  return box {| success = true |}
                              })
                      ) ]

            let wrapped =
                (get agentReportWrapper "wrapper")
                $ (mockAgentReportTool, createObj [ "subagentRole", box "reviewer" ])

            let wrapperSchema = get wrapped "parameters"

            let required =
                if isNullish wrapperSchema then
                    [||]
                else
                    let req = get wrapperSchema "required"
                    if isArray req then unbox<string[]> req else [||]

            check "agent_report wrapper schema no longer requires callId" (not (required |> Array.contains "callId"))
            check "agent_report wrapper schema requires verdict" (required |> Array.contains "verdict")
            check "agent_report wrapper schema requires feedback" (required |> Array.contains "feedback")

            let! passResult =
                (get wrapped "execute")
                $ (createObj [ "verdict", box "PERFECT"; "feedback", box "" ], createObj [])
                |> unbox<JS.Promise<obj>>

            check "agent_report wrapper PERFECT returns success" (truthy (get passResult "success"))
            let passReport = get passResult "report"

            check
                "agent_report wrapper PERFECT attaches PERFECT markdown"
                (not (isNullish passReport) && (str passReport "reportMarkdown" = "PERFECT"))

            check
                "agent_report wrapper forwards PERFECT markdown upstream"
                (capturedUpstream.Count = 1
                 && (str capturedUpstream.[0] "reportMarkdown" = "PERFECT"))

            let! reviseResult =
                (get wrapped "execute")
                $ (createObj [ "verdict", box "REVISE"; "feedback", box "needs work" ], createObj [])
                |> unbox<JS.Promise<obj>>

            let reviseReport = get reviseResult "report"

            check
                "agent_report wrapper REVISE embeds feedback in markdown"
                (not (isNullish reviseReport)
                 && (str reviseReport "reportMarkdown").Contains "needs work")

            check
                "agent_report wrapper REVISE markdown starts with REVISE"
                ((str (capturedUpstream.[1]) "reportMarkdown").StartsWith "REVISE")

        do! rmAsync workspaceDir
    }

let muxSubmitReviewUsesRolledBackHistoryTaskSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "mux-submit-review-history-rollback-"
        let sessionID = "mux-submit-review-history-rollback"
        do! seedLoopActivated workspaceDir sessionID "First task"
        let history = ResizeArray<obj>()
        history.Add(box "---\ntask: First task\n---\nWith-Review Mode is active.")
        let deps = muxMutableDepsWithChatHistory sessionID history
        let reg = createRegistration deps
        let submitTool = muxToolByName reg "submit_review"

        if isNullish submitTool then
            check "mux registration exposes submit_review tool" false
        else
            let prompts = ResizeArray<string>()
            let taskService = mockMuxTaskServiceReturningVerdicts prompts [ "REVISE: not done" ]

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

            let! _ = ((get submitTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
            let promptText = if prompts.Count > 0 then prompts.[0] else ""

            check
                "submit_review uses rolled-back history task instead of stale store task"
                (promptText.Contains "First task" && not (promptText.Contains "Second task"))

        do! rmAsync workspaceDir
    }
