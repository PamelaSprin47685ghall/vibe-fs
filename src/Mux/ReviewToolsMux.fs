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

let private extractHistoryTexts (history: obj array) : string list =
    history
    |> Array.toList
    |> List.collect (fun item ->
        if Dyn.typeIs item "string" then [ string item ]
        else
            let texts = ResizeArray<string>()
            let content = Dyn.str item "content"
            if content <> "" then texts.Add(content)
            let text = Dyn.str item "text"
            if text <> "" then texts.Add(text)
            let parts = Dyn.get item "parts"
            if not (Dyn.isNullish parts) && Dyn.isArray parts then
                for p in (parts :?> obj array) do
                    let partText = Dyn.str p "text"
                    if partText <> "" then texts.Add(partText)
            List.ofSeq texts)

let private tryGetHistoryTask (deps: obj) (sessionID: string) : JS.Promise<string option option> =
    promise {
        let getHistory = if Dyn.isNullish deps then null else Dyn.get deps "getChatHistory"
        if sessionID = "" || Dyn.isNullish getHistory then
            return None
        else
            try
                let! history = unbox<JS.Promise<obj array>> (getHistory $ sessionID)
                return Some(inferReviewTaskFromTexts (extractHistoryTexts history))
            with ex ->
                return None
    }

let private syncReviewTaskFromHistory (deps: obj) (reviewStore: ReviewStore) (sessionID: string) : JS.Promise<string option> =
    promise {
        let! historyTask = tryGetHistoryTask deps sessionID
        historyTask |> Option.iter (syncReviewProjection reviewStore sessionID)
        return
            match historyTask with
            | Some task -> task
            | None -> reviewStore.getReviewTask sessionID
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
              promise {
                  match decodeSubmitReviewArgs args with
                  | Error e -> return wireDecodeFailure "submit_review" e
                  | Ok decoded ->
                      let! resolvedTask = syncReviewTaskFromHistory deps reviewStore workspaceId
                      if not (reviewStore.tryLockReview workspaceId) then
                          return
                              if reviewStore.isReviewActive workspaceId then submitReviewInProgress
                              else submitReviewNotNeeded
                      else
                          try
                              if submitReviewIsWip decoded.Wip then
                                  return submitReviewWipAcknowledgment
                              else
                                  let originalTask = defaultArg resolvedTask ""
                                  let report = decoded.Report
                                  let affectedFiles = decoded.AffectedFiles
                                  try
                                      let! round1 = runReviewRound deps config toolNames (reviewSubmissionVerdictPrompt originalTask report affectedFiles)
                                      let! verdict =
                                          match round1 with
                                          | Accepted -> runReviewRound deps config toolNames (reviewSubmissionDoubleCheckPrompt originalTask report affectedFiles)
                                          | other -> Promise.lift other
                                      match verdict with
                                      | Accepted | Terminated -> reviewStore.deactivateReview workspaceId
                                      | Rejected _ -> ()
                                      return formatReviewResult verdict
                                  with ex ->
                                      reviewStore.deactivateReview workspaceId
                                      return! Promise.reject ex
                          finally
                              reviewStore.unlockReview workspaceId
              }
      condition = None }
