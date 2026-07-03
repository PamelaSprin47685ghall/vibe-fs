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
open Wanxiangshu.Shell.ReviewRuntime
open Wanxiangshu.Shell.ToolRuntimeContext
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OpencodeClientCodec
open Wanxiangshu.Shell.EventLogRuntime

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
                    promise {
                        if sessionID = "" then return submitReviewNotNeeded
                        else
                            do! syncReviewFromEventLog store runtime.Execution.Directory sessionID
                            let activeTask = store.getReviewTask sessionID
                            match activeTask with
                            | None -> return submitReviewNotNeeded
                            | Some task when not (store.tryLockReview sessionID) -> return opencodeSubmitReviewInProgress
                            | Some task ->
                                let abort =
                                    match runtime.AbortSignal with
                                    | Some s -> s
                                    | None -> null
                                try
                                    if submitReviewIsWip decoded.Wip then
                                        do! appendSubmitReviewWipRecorded runtime.Execution.Directory sessionID |> Promise.map ignore
                                        return submitReviewWipAcknowledgment
                                    else
                                        let! result =
                                            runSubmitReview registry client store runtime.Execution.Directory sessionID decoded.Report decoded.AffectedFiles task abort
                                        let verdict, fb = verdictStringFromReviewResult result
                                        do! appendReviewVerdict runtime.Execution.Directory sessionID verdict fb |> Promise.map ignore
                                        match result with
                                        | Accepted _
                                        | Terminated -> store.deactivateReview sessionID
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
                        do! syncReviewFromEventLog store directory sessionID
                        let! texts = Wanxiangshu.Opencode.SessionIo.readSessionTexts client sessionID directory
                        let doubleCheckDone = hasDoubleCheckAnchor texts
                        match decideReviewSubmission decoded.Verdict decoded.Feedback doubleCheckDone with
                        | AskDoubleCheck ->
                            let task = store.getReviewTask sessionID |> Option.defaultValue ""
                            return doubleCheckPrompt task
                        | Finalize result ->
                            let verdict, fb = verdictStringFromReviewResult result
                            do! appendReviewVerdict directory sessionID verdict fb |> Promise.map ignore
                            return if store.resolvePendingReview (sessionID, result) then "Verdict submitted." else "No active review to resolve."
            })
