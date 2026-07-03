module Wanxiangshu.Mux.ReviewToolsMux

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.LoopMessages
open Wanxiangshu.Kernel.ReviewPrompts
open Wanxiangshu.Kernel.ReviewSession
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Kernel.ReviewVerdict
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Kernel.ToolCopy
open Wanxiangshu.Shell.ReviewRuntime
open Wanxiangshu.Shell.ReviewToolsCodec
open Wanxiangshu.Shell.ToolExecute
open Wanxiangshu.Shell.ToolRuntimeContext
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Mux.Delegate
open Wanxiangshu.Mux.Wrappers
open Wanxiangshu.Mux.SubagentTools
open Wanxiangshu.Shell
open Wanxiangshu.Shell.PromiseStr
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.EventLogRuntime

let private syncReviewFromEventLogDir (reviewStore: ReviewStore) (root: string) (sessionID: string) : JS.Promise<string option> =
    promise {
        do! syncReviewFromEventLog reviewStore root sessionID
        return reviewStore.getReviewTask sessionID
    }

let private reviewerOpts (toolNames: string array) : obj =
    let experiments = createObj [ "subagentRole", box "reviewer"; "toolPolicy", box (createObj [ "disabledTools", box (disabledToolsForReviewer toolNames) ]) ]
    createObj [ "aiSettingsAgentId", box "plan"; "experiments", box experiments ]

/// Run one reviewer round, delegating to a fresh reviewer sub-agent and parsing
/// its `reportMarkdown` (authored by the agent_report wrapper) into a verdict.
let private runReviewRound (deps: obj) (config: obj) (toolNames: string array) (prompt: string) : JS.Promise<ReviewResult> =
    promise {
        let! report = delegateToSubAgent deps config "explore" prompt "Review" (Some (reviewerOpts toolNames))
        return parseReviewReportMarkdown report
    }

let submitReviewTool (deps: obj) (toolNames: string array) (reviewStore: ReviewStore) : ToolDefinition =
    { name = "submit_review"
      description = description "submit_review"
      parameters = mkSchema (createObj [ "report", box (strProp Params.submitReviewReport); "affectedFiles", box (strArrayProp Params.submitReviewAffectedFiles); "wip", box (boolProp Params.submitReviewWip) ]) [| "report"; "affectedFiles" |]
      execute = fun config args ->
          match fromMuxConfig config with
          | Error (InvalidIntent (_, "workspaceId", _)) -> resolveStr muxSubmitReviewRequiresWorkspaceId
          | Error e -> resolveStr (wireEncodeToolError "MuxConfig" e)
          | Ok runtime ->
                let workspaceId = runtime.Execution.WorkspaceId |> Option.map Id.workspaceIdValue |> Option.defaultValue ""
                let root = runtime.Execution.Directory
                promise {
                  match decodeSubmitReviewArgs args with
                  | Error e -> return wireDecodeFailure "submit_review" e
                  | Ok decoded ->
                      let! resolvedTask = syncReviewFromEventLogDir reviewStore root workspaceId
                      match resolvedTask with
                      | None -> return submitReviewNotNeeded
                      | Some originalTask ->
                           if not (reviewStore.tryLockReview workspaceId) then
                               return submitReviewInProgress
                           else
                               try
                                   if submitReviewIsWip decoded.Wip then
                                       do! appendSubmitReviewWipRecorded root workspaceId |> Promise.map ignore
                                       return submitReviewWipAcknowledgment
                                   else
                                       let report = decoded.Report
                                       let affectedFiles = decoded.AffectedFiles
                                       let! round1 = runReviewRound deps config toolNames (reviewSubmissionVerdictPrompt originalTask report affectedFiles)
                                       let! verdict =
                                           match round1 with
                                           | Accepted _ -> runReviewRound deps config toolNames (reviewSubmissionDoubleCheckPrompt originalTask report affectedFiles)
                                           | other -> Promise.lift other
                                       let vStr, fb = verdictStringFromReviewResult verdict
                                       do! appendReviewVerdict root workspaceId vStr fb |> Promise.map ignore
                                       match verdict with
                                       | Accepted _ | Terminated -> reviewStore.deactivateReview workspaceId
                                        | NeedsRevision _ -> ()
                                       return formatReviewResult verdict
                               finally
                                   reviewStore.unlockReview workspaceId
              }
      condition = None }
