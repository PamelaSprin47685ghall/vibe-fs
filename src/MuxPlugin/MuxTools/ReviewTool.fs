module VibeFs.MuxPlugin.MuxTools.ReviewTool

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.AgentRole
open VibeFs.Kernel.HostKernel
open VibeFs.Kernel.Prompts
open VibeFs.Mux.Contract
open VibeFs.MuxPlugin.Delegate
open VibeFs.MuxPlugin.MuxTools.Shared

let submitReviewTool (deps: obj) (reviewStore: VibeFs.Kernel.ReviewRuntime.ReviewStore) : ToolDefinition =
    { name = "submit_review"
      description = "Submit completed work for review. Creates a reviewer sub-agent that examines the changes against evaluation criteria and returns PASS or actionable feedback. Only works when session is in active loop mode."
      parameters = mkSchema (createObj [ "report", box (strProp "Detailed report of what was done"); "affectedFiles", box (strArrayProp "List of file paths that were modified or created") ]) [| "report"; "affectedFiles" |]
      execute = fun config args ->
          if strField config "workspaceId" = None then resolveStr "submit_review requires workspaceId"
          else
              let report = defaultArg (strField args "report") ""
              let affectedFiles = requireStrArray args "affectedFiles" |> List.ofArray
              let workspaceId = Dyn.str config "workspaceId"
              if not (reviewStore.tryLockReview workspaceId) then
                  if reviewStore.isReviewActive workspaceId then resolveStr "A review is already in progress for this session."
                  else resolveStr "You do not need review. Just continue with your work."
              else
                  async {
                      try
                          let originalTask = defaultArg (reviewStore.getReviewTask workspaceId) ""
                          let taskSection = if originalTask = "" then "" else "\n=== Original Task ===\n\n" + originalTask
                          let reviewPrompt = Prompts.agentReportReviewInstructions + "\n\n=== Change Report ===\n\n" + report + "\n\n=== Affected Files ===\n\n" + String.concat "\n" affectedFiles + "\n" + taskSection
                          let experiments = createObj [ "subagentRole", box "reviewer"; "toolPolicy", box (createObj [ "disabledTools", box ((subagentToolPolicy Reviewer).disabledTools |> Array.ofList) ]) ]
                          let opts = createObj [ "aiSettingsAgentId", box "plan"; "experiments", box experiments ]
                          let! reviewReport = delegateToSubAgent deps config "explore" reviewPrompt "Review" (Some opts) |> Async.AwaitPromise
                          let trimmed = reviewReport.Trim()
                          let rejectMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"\b(REJECT|FAIL|DENIED|DO NOT ACCEPT)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                          let passMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"\bPASS\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                          let isPass =
                              if rejectMatch.Success then
                                  false
                              elif not passMatch.Success || passMatch.Index >= 200 then
                                  false
                              else
                                  let afterPass = trimmed.Substring(passMatch.Index + 4).Trim()
                                  not (System.Text.RegularExpressions.Regex.IsMatch(afterPass, @"\bFAIL\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                          if isPass then
                              reviewStore.deactivateReview workspaceId
                              return "Review passed. Loop mode ended."
                          else
                              return "Review feedback:\n\n" + reviewReport + "\n\nAddress the feedback above. loop mode is still active; fix the issues and call submit_review again."
                      finally
                          reviewStore.unlockReview workspaceId
                  } |> Async.StartAsPromise
      condition = None }
