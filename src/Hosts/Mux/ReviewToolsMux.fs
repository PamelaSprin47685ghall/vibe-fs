module Wanxiangshu.Hosts.Mux.ReviewToolsMux

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime.LoopMessages
open Wanxiangshu.Runtime.ReviewPrompts
open Wanxiangshu.Kernel.ReviewSession
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Kernel.ReviewVerdict
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Kernel.ToolCopy
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.ReviewToolsCodec
open Wanxiangshu.Runtime.ToolExecute
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Hosts.Mux.Delegate
open Wanxiangshu.Hosts.Mux.Wrappers
open Wanxiangshu.Hosts.Mux.SubagentTools
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.EventLogRuntime

let private description (name: string) : string =
    match Wanxiangshu.Kernel.ToolCatalog.description name with
    | Ok d -> d
    | Error e -> failwith e

let private reviewerOpts (toolNames: string array) : obj =
    let experiments =
        createObj
            [ "subagentRole", box "reviewer"
              "toolPolicy", box (createObj [ "disabledTools", box (disabledToolsForReviewer toolNames) ]) ]

    createObj [ "aiSettingsAgentId", box "plan"; "experiments", box experiments ]

/// Run one reviewer round, delegating to a fresh reviewer sub-agent and parsing
/// its `reportMarkdown` (authored by the agent_report wrapper) into a verdict.
let private runReviewRound
    (deps: obj)
    (config: obj)
    (toolNames: string array)
    (prompt: string)
    : JS.Promise<ReviewResult> =
    promise {
        let! report = delegateToSubAgent deps config "explore" prompt "Review" (Some(reviewerOpts toolNames))
        return parseReviewReportMarkdown report
    }

type private ReviewCandidate =
    | NoTask
    | InProgress
    | Ready of string

/// Look up the review task for the workspace and attempt to acquire the review
/// lock.  Returns the candidate status so the caller can decide whether to
/// proceed, short-circuit, or report an in-progress review.
let private collectReviewCandidates (reviewStore: ReviewStore) (workspaceId: string) : ReviewCandidate =
    match reviewStore.getReviewTask workspaceId with
    | None -> NoTask
    | Some originalTask ->
        if not (reviewStore.tryLockReview workspaceId) then
            InProgress
        else
            Ready originalTask

/// Execute the two-round review (verdict then double-check) and persist the
/// resulting verdict back to the event log.
let private applyReviewResult
    (deps: obj)
    (config: obj)
    (toolNames: string array)
    (reviewStore: ReviewStore)
    (root: string)
    (workspaceId: string)
    (originalTask: string)
    (report: string)
    (affectedFiles: string list)
    : JS.Promise<string> =
    promise {
        let! round1 =
            runReviewRound deps config toolNames (reviewSubmissionVerdictPrompt originalTask report affectedFiles)

        let! verdict =
            match round1 with
            | Accepted _ ->
                runReviewRound
                    deps
                    config
                    toolNames
                    (reviewSubmissionDoubleCheckPrompt originalTask report affectedFiles)
            | other -> Promise.lift other

        let vStr, fb = verdictStringFromReviewResult verdict
        do! appendReviewVerdictOrFail root workspaceId vStr fb
        do! syncReviewFromEventLogDedicated reviewStore root workspaceId

        return formatReviewResult verdict
    }

let submitReviewTool
    (deps: obj)
    (toolNames: string array)
    (reviewStore: ReviewStore)
    (scope: RuntimeScope)
    : ToolDefinition =
    { name = "submit_review"
      description = description "submit_review"
      parameters =
        mkSchema
            (createObj
                [ "report", box (strProp Params.submitReviewReport)
                  "affectedFiles", box (strArrayProp Params.submitReviewAffectedFiles)
                  "wip", box (boolProp Params.submitReviewWip) ])
            [| "report"; "affectedFiles" |]
      execute =
        fun config args ->
            match fromMuxConfig config with
            | Error(InvalidIntent(_, "workspaceId", _)) -> Promise.lift muxSubmitReviewRequiresWorkspaceId
            | Error e -> Promise.lift (wireEncodeToolError "MuxConfig" e)
            | Ok runtime ->
                let workspaceId =
                    runtime.Execution.WorkspaceId
                    |> Option.map Id.workspaceIdValue
                    |> Option.defaultValue ""

                let root = runtime.Execution.Directory

                promise {
                    scope.TriggerInit(root)
                    do! scope.WaitInit()

                    match decodeSubmitReviewArgs args with
                    | Error e -> return wireDecodeFailure "submit_review" e
                    | Ok decoded ->
                        match collectReviewCandidates reviewStore workspaceId with
                        | NoTask -> return submitReviewNotNeeded
                        | InProgress -> return submitReviewInProgress
                        | Ready originalTask ->
                            try
                                if submitReviewIsWip decoded.Wip then
                                    do! appendSubmitReviewWipRecordedOrFail root workspaceId decoded.Report
                                    return formatWipAcknowledgment originalTask
                                else
                                    let! result =
                                        applyReviewResult
                                            deps
                                            config
                                            toolNames
                                            reviewStore
                                            root
                                            workspaceId
                                            originalTask
                                            decoded.Report
                                            decoded.AffectedFiles

                                    return result
                            finally
                                reviewStore.unlockReview workspaceId
                }
      condition = None }
