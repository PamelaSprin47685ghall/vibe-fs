module Wanxiangshu.Omp.ReviewToolsRegister

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.ReviewPrompts
open Wanxiangshu.Kernel.ReviewSession
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Omp.Codec
open Wanxiangshu.Omp.OmpToolSchema
open Wanxiangshu.Omp.ReviewLoop
open Wanxiangshu.Omp.ReviewToolsLoop
open Wanxiangshu.Omp.Schema
open Wanxiangshu.Shell.DynField
module Dyn = Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.ReviewRuntime

let private optBool = Wanxiangshu.Shell.DynField.optBool

let registerLoopFeatures (pi: obj) (store: ReviewStore) : unit =
    let tb = Dyn.get pi "typebox"
    pi?registerCommand(loopCommand, createObj [ "description", box "Enable loop review mode for the current session"; "handler", box(fun (args: string) (ctx: obj) -> handleLoopCommand pi store args ctx |> ignore) ])
    pi?registerCommand("loop-review", createObj [ "description", box "Pre-check a task before activating loop mode (mirrors Opencode's command.execute.before)."; "handler", box(fun (args: string) (ctx: obj) -> handleLoopReviewCommand pi store args ctx |> ignore) ])
    pi?registerTool(
        createObj [
            "name", box "submit_review"
            "label", box "Submit Review"
            "description", box (description "submit_review")
            "parameters", objectOf [| ("report", str "Detailed description of what was changed." tb); ("affectedFiles", stringArraySchema pi "Modified or created file path."); ("wip", opt "Defaults to true: record progress without starting a reviewer. Set to false to start the reviewer for final review." tb bool_) |] tb
            "execute",
                box(fun (_id: string) (params': obj) (_s: obj) (_u: obj) (ctx: obj) ->
                    promise {
                        match getSessionIdFromContext ctx with
                        | None -> return errorResult "Loop review is not active for this session."
                        | Some sessionId ->
                            if not (store.isReviewActive sessionId) then return errorResult "Loop review is not active for this session."
                            else
                                let wip = submitReviewIsWip (optBool params' "wip")
                                if wip then return textResult submitReviewWipAcknowledgment
                                elif not (store.tryLockReview sessionId) then return errorResult "A review is already in progress."
                                else
                                    let report = Dyn.str params' "report"
                                    let files =
                                        let a = Dyn.get params' "affectedFiles"
                                        if Dyn.isNullish a || not (Dyn.isArray a) then [||]
                                        else unbox<obj array> a |> Array.map string
                                    let mutable loopError : exn option = None
                                    let mutable result : JsReviewResult option = None
                                    try
                                        let! r = runReviewLoop pi ctx store sessionId report files (store.getReviewTask sessionId)
                                        result <- Some r
                                    with ex -> loopError <- Some ex
                                    store.unlockReview sessionId
                                    match loopError, result with
                                    | Some ex, _ -> return asErrorResult ex
                                    | None, None -> return errorResult "Review loop returned no result."
                                    | None, Some r ->
                                        if r.feedback.IsNone && not (defaultArg r.terminated false) then
                                            store.deactivateReview sessionId
                                            return textResult "Review passed. Loop mode ended."
                                        elif defaultArg r.terminated false then
                                            store.deactivateReview sessionId
                                            return errorResult ("Review terminated: " + defaultArg r.feedback "")
                                        else return errorResult ("Review feedback:\n\n" + r.feedback.Value)
                    })
        ])
    pi?registerTool(
        createObj [
            "name", box "return_reviewer"
            "label", box "Return Reviewer"
            "description", box (description "return_reviewer")
            "defaultInactive", box true
            "parameters", returnReviewerParameters tb
            "execute",
                box(fun (_id: string) (params': obj) (_s: obj) (_u: obj) (ctx: obj) ->
                    promise {
                        match getSessionIdFromContext ctx with
                        | None -> return errorResult "No pending review to resolve."
                        | Some sessionId ->
                            match Wanxiangshu.Kernel.ReviewVerdict.parseVerdict (Dyn.str params' "verdict") with
                            | None -> return textResult reviewerNudgePrompt
                            | Some Wanxiangshu.Kernel.ReviewVerdict.Pass ->
                                let fb = (Dyn.str params' "feedback").Trim()
                                if not (store.resolvePendingReview(sessionId, Accepted fb)) then return errorResult "No pending review to resolve."
                                else return { textResult "Review submitted: accepted." with display = Some false }
                            | Some Wanxiangshu.Kernel.ReviewVerdict.Reject ->
                                let fb = (Dyn.str params' "feedback").Trim()
                                if fb = "" then return textResult reviewerNudgePrompt
                                elif not (store.resolvePendingReview(sessionId, Rejected fb)) then return errorResult "No pending review to resolve."
                                else return { textResult "Review submitted: rejected with feedback." with display = Some false }
                    })
        ])

let registerInputHandler (pi: obj) (store: ReviewStore) : unit =
    pi?on("input", box(fun (event: obj) (ctx: obj) ->
        promise {
            let text = (Dyn.str event "text").Trim()
            if not (text.StartsWith("/" + loopCommand)) then return box null
            else
                let rest = text.Substring(loopCommand.Length + 1).Trim()
                do! handleLoopCommand pi store rest ctx
                return createObj [ "handled", box true ]
        }))