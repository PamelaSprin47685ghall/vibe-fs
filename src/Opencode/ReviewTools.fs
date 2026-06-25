module VibeFs.Opencode.ReviewTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Shell

open VibeFs.Kernel.ReviewSession
open VibeFs.Kernel.ReviewVerdict
open VibeFs.Kernel.ReviewPrompts
open VibeFs.Kernel.LoopMessages
open VibeFs.Kernel.ToolCatalog
open VibeFs.Kernel.ToolCopy
open VibeFs.Opencode.ToolSchema
open VibeFs.Opencode.SessionIo
open VibeFs.Shell.ToolExecute
open VibeFs.Kernel.ToolResult
open VibeFs.Opencode.ReviewerLoop
open VibeFs.Shell.PromiseStr
open VibeFs.Shell.ReviewToolsCodec
open VibeFs.Shell.ToolRuntimeContext
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.Dyn
open VibeFs.Shell.OpencodeClientCodec

let private formatReviewResult = VibeFs.Kernel.ReviewPrompts.formatReviewResult

let submitReviewTool (registry: ChildAgentRegistry) (ctx: obj) (store: VibeFs.Shell.ReviewRuntime.ReviewStore) : obj =
    define submitReview
        (box {| report = strReq Params.submitReviewReport; affectedFiles = strArrayOpt Params.submitReviewAffectedFiles |})
        (fun args context ->
            match decodeSubmitReviewArgs args with
            | Error e -> resolveStr (wireDecodeFailure "submit_review" e)
            | Ok decoded ->
                match getClientFromPluginCtx ctx with
                | Error e -> resolveStr (wireEncodeToolError "OpencodeClient" e)
                | Ok client ->
                    let runtime = fromOpencode context (pluginDirectoryFromCtx ctx)
                    let sessionID = runtime.Execution.SessionId
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

let submitReviewResultTool (ctx: obj) (store: VibeFs.Shell.ReviewRuntime.ReviewStore) : obj =
    define submitReviewResult
        (box {| verdict = enumReq [| "PASS"; "REJECT" |] Params.returnReviewerVerdict
                feedback = strOpt Params.returnReviewerFeedback |})
        (fun args context ->
            let runtime = fromOpencode context (pluginDirectoryFromCtx ctx)
            let sessionID =
                let id = runtime.Execution.SessionId
                if id = "" then "loop" else id
            let directory = runtime.Execution.Directory
            promise {
                match decodeReturnReviewerArgs args with
                | Error e -> return wireDecodeFailure "return_reviewer" e
                | Ok decoded ->
                    match getClientFromPluginCtx ctx with
                    | Error e -> return wireEncodeToolError "OpencodeClient" e
                    | Ok client ->
                        let! texts = VibeFs.Opencode.SessionIo.readSessionTexts client sessionID directory
                        let doubleCheckDone = hasDoubleCheckAnchor texts
                        match decideReviewSubmission decoded.Verdict decoded.Feedback doubleCheckDone with
                        | AskDoubleCheck ->
                            let task = defaultArg (inferReviewTaskFromTexts texts) ""
                            return doubleCheckPrompt task
                        | Finalize result ->
                            return if store.resolvePendingReview (sessionID, result) then "Verdict submitted." else "No active review to resolve."
            })