module VibeFs.Tests.IntegrationMuxReviewPromptSpecs

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Tests.IntegrationToolSetup
open VibeFs.Tests.IntegrationMuxSetup

open VibeFs.Mux.Plugin
open VibeFs.Shell.Dyn


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
        let taskService =
            createObj
                [ "create",
                  box (System.Func<obj, JS.Promise<obj>>(fun input ->
                      promise {
                          let promptText = str input "prompt"
                          if promptText <> "" then prompts.Add(promptText)
                          return box {| success = true; data = box {| taskId = "reviewer-task-1"; kind = "agent" |} |}
                      }))
                  "waitForAgentReport",
                  box (System.Func<string, obj, JS.Promise<obj>>(fun _ _ ->
                      Promise.lift (box {| reportMarkdown = "reviewer running" |}))) ]
        let ctx = createObj [ "directory", box workspaceDir; "workspaceId", box sessionID; "sessionID", box sessionID; "taskService", box taskService ]
        let args = createObj [ "report", box "Changed a.ts"; "affectedFiles", box [| "a.ts" |] ]
        let submitPromise = ((get submitTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
        do! Promise.sleep 0
        let! pending = muxPendingCallIdsForTest reg
        let matching = pending |> Array.tryFind (fun id -> id.StartsWith(sessionID + "-review-"))
        match matching with
        | None -> check "submit_review registers a pending review call" false
        | Some callId ->
            let promptText = if prompts.Count > 0 then prompts.[0] else ""
            check "submit_review prompt uses front-matter" (promptText.StartsWith "---")
            check "submit_review prompt includes call_id field" (promptText.Contains("call_id: \"" + callId + "\""))
            check "submit_review prompt does not ask for tool-level callId" (not (promptText.Contains "callId"))
            check "submit_review prompt reuses review criteria" (promptText.Contains "# Evaluation Criteria")
            check "submit_review prompt uses agent_report protocol" (promptText.Contains "agent_report")
            check "submit_review prompt drops legacy divider" (not (promptText.Contains "==="))
            let! resolved = muxResolveFirstMatchingCallForTest reg (sessionID + "-review-") (createObj [ "verdict", box "PASS"; "feedback", box "" ])
            check "submit_review prompt test resolves pending review call" resolved
            let! result = submitPromise
            check "submit_review accepts resolved PASS verdict" (result.Contains "verdict: accepted")
    do! rmAsync workspaceDir
}

let muxAgentReportWrapperResolvesReviewBySessionSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-agent-report-wrapper-"
    let sessionID = "mux-agent-report-wrapper"
    let reg = createRegistration (minimalMuxDeps ())
    muxActivateReviewForTest reg sessionID "Implement feature X"
    let prompts = ResizeArray<string>()
    let taskService =
        createObj
            [ "create",
              box (System.Func<obj, JS.Promise<obj>>(fun input ->
                  promise {
                      let promptText = str input "prompt"
                      if promptText <> "" then prompts.Add(promptText)
                      return box {| success = true; data = box {| taskId = "reviewer-task-1"; kind = "agent" |} |}
                  }))
              "waitForAgentReport",
              box (System.Func<string, obj, JS.Promise<obj>>(fun _ _ ->
                  Promise.lift (box {| reportMarkdown = "reviewer running" |}))) ]
    let submitTool = muxToolByName reg "submit_review"
    if isNullish submitTool then
        check "mux registration exposes submit_review tool" false
    else
        let ctx = createObj [ "directory", box workspaceDir; "workspaceId", box sessionID; "sessionID", box sessionID; "taskService", box taskService ]
        let args = createObj [ "report", box "Changed a.ts"; "affectedFiles", box [| "a.ts" |] ]
        let submitPromise = ((get submitTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
        do! Promise.sleep 0
        let! pending = muxPendingCallIdsForTest reg
        check "submit_review registers a pending review call" (pending |> Array.exists (fun id -> id.StartsWith(sessionID + "-review-")))
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
                if isNullish wrapperSchema then
                    [||]
                else
                    let req = get wrapperSchema "required"
                    if isArray req then unbox<string[]> req else [||]
            check "agent_report wrapper schema no longer requires callId" (not (required |> Array.contains "callId"))
            check "agent_report wrapper schema requires verdict" (required |> Array.contains "verdict")
            check "agent_report wrapper schema requires feedback" (required |> Array.contains "feedback")
            let wrapperArgs = createObj [ "verdict", box "PASS"; "feedback", box "" ]
            let wrapperOpts = createObj [ "sessionID", box sessionID ]
            let! result = (get wrapped "execute") $ (wrapperArgs, wrapperOpts) |> unbox<JS.Promise<obj>>
            check "agent_report wrapper returns success" (truthy (get result "success"))
            let report = get result "report"
            check "agent_report wrapper attaches upstream report payload" (not (isNullish report) && (str report "reportMarkdown" = "PASS"))
            let! pendingAfter = muxPendingCallIdsForTest reg
            check "agent_report wrapper resolves pending review call by sessionID" (not (pendingAfter |> Array.exists (fun id -> id.StartsWith(sessionID + "-review-"))))
            check "agent_report wrapper forwarded upstream markdown payload" (capturedUpstream.Count > 0 && (str capturedUpstream.[0] "reportMarkdown" = "PASS"))
            let fallbackSessionID = sessionID + "-no-session-fallback"
            muxActivateReviewForTest reg fallbackSessionID "Implement feature Y"
            let fallbackCtx = createObj [ "directory", box workspaceDir; "workspaceId", box fallbackSessionID; "sessionID", box fallbackSessionID; "taskService", box taskService ]
            let fallbackSubmitPromise = ((get submitTool "execute") $ (fallbackCtx, args)) |> unbox<JS.Promise<string>>
            do! Promise.sleep 0
            let! pendingBeforeFallback = muxPendingCallIdsForTest reg
            let fallbackCallId = pendingBeforeFallback |> Array.tryFind (fun id -> id.StartsWith(fallbackSessionID + "-review-")) |> Option.defaultValue ""
            check "agent_report wrapper test captured fallback pending review call" (fallbackCallId <> "")
            let fallbackArgs = createObj [ "verdict", box "PASS"; "feedback", box ""; "callId", box fallbackCallId ]
            let fallbackOpts = createObj []
            let! fallbackResult = (get wrapped "execute") $ (fallbackArgs, fallbackOpts) |> unbox<JS.Promise<obj>>
            check "agent_report wrapper still succeeds without session context" (truthy (get fallbackResult "success"))
            let! pendingAfterFallback = muxPendingCallIdsForTest reg
            check "agent_report wrapper ignores tool-level callId fallback" (pendingAfterFallback |> Array.exists (fun id -> id = fallbackCallId))
            let! fallbackResolved = muxResolveFirstMatchingCallForTest reg (fallbackSessionID + "-review-") (createObj [ "verdict", box "PASS"; "feedback", box "" ])
            check "fallback review call resolved explicitly for cleanup" fallbackResolved
            let! fallbackSubmitResult = fallbackSubmitPromise
            check "fallback submit_review accepts explicit PASS verdict" (fallbackSubmitResult.Contains "verdict: accepted")
        let! submitResult = submitPromise
        check "submit_review accepts resolved PASS verdict" (submitResult.Contains "verdict: accepted")
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
        let taskService = mockMuxTaskServiceCapturingPrompt prompts
        let ctx = createObj [ "directory", box workspaceDir; "workspaceId", box sessionID; "sessionID", box sessionID; "taskService", box taskService ]
        let args = createObj [ "report", box "Changed a.ts"; "affectedFiles", box [| "a.ts" |] ]
        try
            let! _ = ((get submitTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
            ()
        with _ ->
            ()
        let promptText = if prompts.Count > 0 then prompts.[0] else ""
        check "submit_review uses rolled-back history task instead of stale store task" (promptText.Contains "First task" && not (promptText.Contains "Second task"))
    do! rmAsync workspaceDir
}

let muxLoopReviewPromptUsesFrontMatterSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-loop-review-prompt-"
    let prompts = ResizeArray<string>()
    let deps = minimalMuxDeps ()
    deps?("taskService") <-
        createObj [ "create",
                    box (System.Func<obj, JS.Promise<obj>>(fun input ->
                        promise {
                            let promptText = str input "prompt"
                            if promptText <> "" then prompts.Add(promptText)
                            return box {| success = true; data = box {| taskId = "loop-reviewer-task-1"; kind = "agent" |} |}
                        }))
                    "waitForAgentReport",
                    box (System.Func<string, obj, JS.Promise<obj>>(fun _ _ ->
                        Promise.lift (box {| reportMarkdown = "PASS" |}))) ]
    let reg = createRegistration deps
    let commands = unbox<obj[]> (get reg "slashCommands")
    let loopReview = commands |> Array.find (fun command -> str command "key" = "loop-review")
    let loopPromise = (get loopReview "execute") $ ("mux-loop-review-prompt", "Clarify rollout plan") |> unbox<JS.Promise<string>>
    do! Promise.sleep 0
    let promptText = if prompts.Count > 0 then prompts.[0] else ""
    check "loop-review prompt uses front-matter" (promptText.StartsWith "---")
    check "loop-review prompt includes call_id field" (promptText.Contains "call_id: \"")
    check "loop-review prompt does not ask for tool-level callId" (not (promptText.Contains "callId"))
    check "loop-review prompt carries task block" (promptText.Contains "task: |")
    check "loop-review prompt reuses review criteria" (promptText.Contains "# Evaluation Criteria")
    check "loop-review prompt uses agent_report protocol" (promptText.Contains "agent_report")
    check "loop-review prompt drops legacy divider" (not (promptText.Contains "==="))
    let! resolved = muxResolveFirstMatchingCallForTest reg "mux-loop-review-prompt-loop-review-" (createObj [ "verdict", box "PASS"; "feedback", box "" ])
    check "loop-review prompt test resolves pending pre-review call" resolved
    let! result = loopPromise
    check "loop-review activates review after pass" (result.Contains "With-Review Mode is active")
    do! rmAsync workspaceDir
}
