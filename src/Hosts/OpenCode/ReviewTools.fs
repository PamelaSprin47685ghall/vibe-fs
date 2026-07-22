module Wanxiangshu.Hosts.Opencode.ReviewTools

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime

open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Kernel.ReviewVerdict
open Wanxiangshu.Runtime.ReviewPrompts
open Wanxiangshu.Runtime.LoopMessages
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Kernel.ToolCopy
open Wanxiangshu.Hosts.Opencode.ToolSchema
open Wanxiangshu.Hosts.Opencode.SessionIo
open Wanxiangshu.Runtime.ToolExecute
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Hosts.Opencode.ReviewerLoop
open Wanxiangshu.Runtime.ReviewToolsCodec
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.ReviewEventWriter
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.RuntimeScope

let private formatReviewResult =
    Wanxiangshu.Runtime.ReviewPrompts.formatReviewResult

let private buildReviewToolRequest
    (ctx: obj)
    (args: obj)
    (context: obj)
    : Result<obj * IToolRuntimeContext * SubmitReviewArgs, string> =
    match getClientFromPluginCtx ctx with
    | Error e -> Error(wireEncodeToolError "OpencodeClient" e)
    | Ok client ->
        match decodeSubmitReviewArgs args with
        | Error e -> Error(wireDecodeFailure "submit_review" e)
        | Ok decoded ->
            let runtime = fromOpencode context (pluginDirectoryFromCtx ctx)
            Ok(client, runtime, decoded)

let private processReviewToolResponse
    (registry: ChildAgentRegistry)
    (client: obj)
    (store: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore)
    (scope: RuntimeScope)
    (sessionID: string)
    (runtime: IToolRuntimeContext)
    (decoded: SubmitReviewArgs)
    : JS.Promise<string> =
    promise {
        scope.TriggerInit(runtime.Execution.Directory)
        do! scope.WaitInit()

        if sessionID = "" then
            return submitReviewNotNeeded
        else
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
                        do! appendSubmitReviewWipRecordedOrFail runtime.Execution.Directory sessionID decoded.Report
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
    }

let submitReviewTool
    (registry: ChildAgentRegistry)
    (ctx: obj)
    (store: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore)
    (scope: RuntimeScope)
    : obj =
    define
        submitReview
        (box
            {| report = strReq Params.submitReviewReport
               affectedFiles = strArrayOpt Params.submitReviewAffectedFiles
               wip = boolOptional Params.submitReviewWip |})
        (fun args context ->
            match buildReviewToolRequest ctx args context with
            | Error e -> Promise.lift e
            | Ok(client, runtime, decoded) ->
                let sessionID =
                    Wanxiangshu.Kernel.Primitives.Identity.Id.sessionIdValue runtime.Execution.SessionId

                processReviewToolResponse registry client store scope sessionID runtime decoded)

let submitReviewResultTool
    (ctx: obj)
    (store: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore)
    (scope: RuntimeScope)
    : obj =
    define
        submitReviewResult
        (box
            {| verdict = enumReq [| "PERFECT"; "REVISE" |] Params.returnReviewerVerdict
               feedback = strOpt Params.returnReviewerFeedback |})
        (fun args context ->
            let runtime = fromOpencode context (pluginDirectoryFromCtx ctx)

            let sessionID =
                let id =
                    Wanxiangshu.Kernel.Primitives.Identity.Id.sessionIdValue runtime.Execution.SessionId

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
                        let doubleCheckDone = store.isChallengeRequested sessionID

                        match decideReviewSubmission decoded.Verdict decoded.Feedback doubleCheckDone with
                        | AskDoubleCheck ->
                            store.recordChallengeRequested sessionID
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
