module VibeFs.MuxPlugin.MuxTools.ReviewTool

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.ToolPolicy
open VibeFs.Kernel.HostKernel
open VibeFs.Kernel.ReviewSession
open VibeFs.Mux.Contract
open VibeFs.MuxPlugin.Delegate
open VibeFs.MuxPlugin.CallStore
open VibeFs.MuxPlugin.MuxPrompts
open VibeFs.MuxPlugin.MuxTools.Shared

let private dateNow () = int (System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())

let private reviewVerdictInstructions =
    "You are a reviewer evaluating whether the reported changes satisfy the original task.\n\n"
    + "Call the agent_report tool to submit your verdict. Use exactly these fields:\n"
    + "- verdict: \"PASS\" if the changes are acceptable, \"REJECT\" otherwise\n"
    + "- feedback: detailed, actionable feedback when rejecting; empty string when passing\n"
    + "- callId: the callId supplied in this prompt\n\n"
    + "Do not output free-form text as your final answer; the tool call is required."

let private disabledToolsForReviewer () : string array =
    deniedTools "reviewer" (Array.toList registeredToolNames) |> Array.ofList

let submitReviewTool (deps: obj) (callStore: CallStore) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) : ToolDefinition =
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
                          let callId = workspaceId + "-review-" + string (dateNow ())
                          let verdictPromise = registerCallWithTimeout callStore callId 300000
                          let reviewPrompt =
                              reviewVerdictInstructions
                              + "\n\n=== Change Report ===\n\n" + report
                              + "\n\n=== Affected Files ===\n\n" + String.concat "\n" affectedFiles
                              + "\n" + taskSection
                          let disabledTools = disabledToolsForReviewer ()
                          let experiments =
                              createObj
                                  [ "subagentRole", box "reviewer"
                                    "toolPolicy", box (createObj [ "disabledTools", box disabledTools ]) ]
                          let opts = createObj [ "aiSettingsAgentId", box "plan"; "experiments", box experiments ]
                          let! _ = delegateToSubAgent deps config "explore" reviewPrompt "Review" (Some opts) |> Async.AwaitPromise
                          let! verdict =
                              async {
                                  try
                                      let! args = verdictPromise |> Async.AwaitPromise
                                      let v = defaultArg (strField args "verdict") "" |> fun s -> s.Trim().ToLowerInvariant()
                                      let feedback = defaultArg (strField args "feedback") ""
                                      if v = "pass" then return Accepted
                                      elif v = "reject" then return Rejected feedback
                                      else return Rejected $"Reviewer returned unclear verdict: \"{v}\". Expected \"pass\" or \"reject\"."
                                  with ex ->
                                      return Rejected $"Reviewer timed out or failed: {ex.Message}"
                              }
                          match verdict with
                          | Accepted ->
                              reviewStore.deactivateReview workspaceId
                              return "Review passed. Loop mode ended."
                          | Rejected feedback ->
                              return "Review feedback:\n\n" + feedback + "\n\nAddress the feedback above. loop mode is still active; fix the issues and call submit_review again."
                          | Terminated ->
                              return "Review terminated without verdict. loop mode is still active; fix the issues and call submit_review again."
                      finally
                          reviewStore.unlockReview workspaceId
                  } |> Async.StartAsPromise
      condition = None }
