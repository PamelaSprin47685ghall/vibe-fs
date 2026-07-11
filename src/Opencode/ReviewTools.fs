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
open Wanxiangshu.Shell.EventLogRuntime
open Wanxiangshu.Shell.ToolRuntimeContext
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.EventLogRuntime
open Wanxiangshu.Shell.OpencodeClientCodec
open Wanxiangshu.Shell.RuntimeScope

let private formatReviewResult = Wanxiangshu.Kernel.ReviewPrompts.formatReviewResult

let submitReviewTool
    (registry: ChildAgentRegistry)
    (ctx: obj)
    (store: Wanxiangshu.Shell.ReviewRuntime.ReviewStore)
    (scope: RuntimeScope)
    : obj =
    define
        submitReview
        (box
            {| report = strReq Params.submitReviewReport
               affectedFiles = strArrayOpt Params.submitReviewAffectedFiles
               wip = boolOptional Params.submitReviewWip |})
        (fun args context ->
            match decodeSubmitReviewArgs args with
            | Error e -> resolveStr (wireDecodeFailure "submit_review" e)
            | Ok decoded ->
                match getClientFromPluginCtx ctx with
                | Error e -> resolveStr (wireEncodeToolError "OpencodeClient" e)
                | Ok client ->
                    let runtime = fromOpencode context (pluginDirectoryFromCtx ctx)

                    let sessionID =
                        Wanxiangshu.Kernel.Domain.Id.sessionIdValue runtime.Execution.SessionId

                    promise {
                        scope.TriggerInit(runtime.Execution.Directory)
                        do! scope.WaitInit()

                        if sessionID = "" then
                            return submitReviewNotNeeded
                        else
                            let activeTask = store.getReviewTask sessionID

                            match activeTask with
                            | None -> return submitReviewNotNeeded
                            | Some task when not (store.tryLockReview sessionID) ->
                                return opencodeSubmitReviewInProgress
                            | Some task ->
                                let abort =
                                    match runtime.AbortSignal with
                                    | Some s -> s
                                    | None -> null

                                try
                                    if submitReviewIsWip decoded.Wip then
                                        do! appendSubmitReviewWipRecordedOrFail runtime.Execution.Directory sessionID
                                        return formatWipAcknowledgment task
                                    else
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

                                        let verdict, fb = verdictStringFromReviewResult result
                                        do! appendReviewVerdictOrFail runtime.Execution.Directory sessionID verdict fb
                                        do! syncReviewFromEventLogDedicated store runtime.Execution.Directory sessionID

                                        return formatReviewResult result
                                finally
                                    store.unlockReview sessionID
                    })

let submitReviewResultTool (ctx: obj) (store: Wanxiangshu.Shell.ReviewRuntime.ReviewStore) (scope: RuntimeScope) : obj =
    define
        submitReviewResult
        (box
            {| verdict = enumReq [| "PERFECT"; "REVISE" |] Params.returnReviewerVerdict
               feedback = strOpt Params.returnReviewerFeedback |})
        (fun args context ->
            let runtime = fromOpencode context (pluginDirectoryFromCtx ctx)

            let sessionID =
                let id = Wanxiangshu.Kernel.Domain.Id.sessionIdValue runtime.Execution.SessionId
                if id = "" then "loop" else id

            let directory = runtime.Execution.Directory

            promise {
                scope.TriggerInit(directory)
                do! scope.WaitInit()

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
                            let task = store.getReviewTask sessionID |> Option.defaultValue ""
                            return doubleCheckPrompt task
                        | Finalize result ->
                            let resolved = store.resolvePendingReview (sessionID, result)

                            if not resolved then
                                return "No active review to resolve."
                            else
                                let verdict, fb = verdictStringFromReviewResult result
                                do! appendReviewVerdictOrFail directory sessionID verdict fb
                                return returnReviewerVerdictSubmittedMessage
            })
