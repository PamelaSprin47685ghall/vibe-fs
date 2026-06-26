module Wanxiangshu.Opencode.ReviewTools

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Shell

open Wanxiangshu.Kernel.ReviewSession
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Kernel.ReviewVerdict
open Wanxiangshu.Kernel.ReviewPrompts
open Wanxiangshu.Kernel.LoopMessages
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Kernel.ToolCopy
open Wanxiangshu.Opencode.ToolSchema
open Wanxiangshu.Opencode.SessionIo
open Wanxiangshu.Shell.ToolExecute
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Opencode.ReviewerLoop
open Wanxiangshu.Shell.PromiseStr
open Wanxiangshu.Shell.ReviewToolsCodec
open Wanxiangshu.Shell.ToolRuntimeContext
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OpencodeClientCodec

let private formatReviewResult = Wanxiangshu.Kernel.ReviewPrompts.formatReviewResult

let submitReviewTool (registry: ChildAgentRegistry) (ctx: obj) (store: Wanxiangshu.Shell.ReviewRuntime.ReviewStore) : obj =
    define submitReview
        (box {| report = strReq Params.submitReviewReport; affectedFiles = strArrayOpt Params.submitReviewAffectedFiles; wip = boolOptional Params.submitReviewWip |})
        (fun args context ->
            match decodeSubmitReviewArgs args with
            | Error e -> resolveStr (wireDecodeFailure "submit_review" e)
            | Ok decoded ->
                match getClientFromPluginCtx ctx with
                | Error e -> resolveStr (wireEncodeToolError "OpencodeClient" e)
                | Ok client ->
                    let runtime = fromOpencode context (pluginDirectoryFromCtx ctx)
                    let sessionID = Wanxiangshu.Kernel.Domain.Id.sessionIdValue runtime.Execution.SessionId
                    if sessionID = "" || not (store.isReviewActive sessionID) then
                        resolveStr submitReviewNotNeeded
                    elif not (store.tryLockReview sessionID) then
                        resolveStr opencodeSubmitReviewInProgress
                    else
                        let abort =
                            match runtime.AbortSignal with
                            | Some s -> s
                            | None -> null
                        promise {
                            try
                                if submitReviewIsWip decoded.Wip then
                                    return submitReviewWipAcknowledgment
                                else
                                    let task = defaultArg (store.getReviewTask sessionID) ""
                                    let! result =
                                        runSubmitReview
                                            registry
                                            client
                                            store
                                            runtime.Execution.Directory
                                            sessionID
                                            decoded.Report
                                            decoded.AffectedFiles
                                            task
                                            abort
                                    match result with
                                    | Accepted
                                    | Terminated ->
                                        store.deactivateReview sessionID
                                    | Rejected _ -> ()
                                    return formatReviewResult result
                            finally
                                store.unlockReview sessionID
                        })

let submitReviewResultTool (ctx: obj) (store: Wanxiangshu.Shell.ReviewRuntime.ReviewStore) : obj =
    define submitReviewResult
        (box {| verdict = enumReq [| "PASS"; "REJECT" |] Params.returnReviewerVerdict
                feedback = strOpt Params.returnReviewerFeedback |})
        (fun args context ->
            let runtime = fromOpencode context (pluginDirectoryFromCtx ctx)
            let sessionID =
                let id = Wanxiangshu.Kernel.Domain.Id.sessionIdValue runtime.Execution.SessionId
                if id = "" then "loop" else id
            let directory = runtime.Execution.Directory
            promise {
                match decodeReturnReviewerArgs args with
                | Error e -> return wireDecodeFailure "return_reviewer" e
                | Ok decoded ->
                    match getClientFromPluginCtx ctx with
                    | Error e -> return wireEncodeToolError "OpencodeClient" e
                    | Ok client ->
                        let! texts = Wanxiangshu.Opencode.SessionIo.readSessionTexts client sessionID directory
                        let doubleCheckDone = hasDoubleCheckAnchor texts
                        match decideReviewSubmission decoded.Verdict decoded.Feedback doubleCheckDone with
                        | AskDoubleCheck ->
                            let task = defaultArg (inferReviewTaskFromTexts texts) ""
                            return doubleCheckPrompt task
                        | Finalize result ->
                            return if store.resolvePendingReview (sessionID, result) then "Verdict submitted." else "No active review to resolve."
            })