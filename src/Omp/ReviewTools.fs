module VibeFs.Omp.ReviewTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.ReviewPrompts
open VibeFs.Kernel.ReviewSession
open VibeFs.Kernel.ToolCatalog
open VibeFs.Omp.Codec
open VibeFs.Omp.MessagingCodec
open VibeFs.Omp.OmpToolSchema
open VibeFs.Omp.ReviewLoop
open VibeFs.Omp.Schema
module Dyn = VibeFs.Shell.Dyn
open VibeFs.Shell.FuzzySearch
open VibeFs.Shell.ReviewRuntime

let private loopCommand = "loop"

let private handleLoopReviewCommand (pi: obj) (store: ReviewStore) (args: string) (ctx: obj) : JS.Promise<unit> =
    promise {
        let task = args.Trim()
        match getSessionIdFromContext ctx with
        | None -> ()
        | Some sessionId ->
            let notify = Dyn.get (Dyn.get ctx "ui") "notify"
            let notifyInfo (msg: string) =
                if Dyn.typeIs notify "function" then
                    emitJsExpr (notify, box msg, box "info") "if (typeof $0 === 'function') $0($1, $2)" |> ignore
            if task = "" then
                notifyInfo "loop-review needs a task. Try /loop-review <task>."
            elif store.isReviewActive sessionId then
                notifyInfo "loop mode is already active."
            else
                let! result = runPreReviewerSession pi ctx store task
                match result with
                | Accepted ->
                    notifyInfo $"Pre-review passed. Task \"{task}\" already meets criteria — no loop needed."
                | Terminated ->
                    notifyInfo "Pre-review could not complete."
                | Rejected feedback ->
                    store.activateReview(sessionId, task, System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                    pi?sendMessage(
                        createObj [
                            "customType", box "kunwei-loop-precheck"
                            "content",
                                box(
                                    String.concat
                                        "\n"
                                        [ $"Task (loop): {task}"
                                          ""
                                          "=== Pre-review Feedback ==="
                                          ""
                                          feedback
                                          ""
                                          "Address the feedback above, then call submit_review with a detailed report and affected files." ])
                            "display", box true
                        ],
                        createObj [ "triggerTurn", box true ])
                    notifyInfo "Pre-review found issues. Loop mode is active — address feedback and submit_review."
    }

let private handleLoopCommand (pi: obj) (store: ReviewStore) (args: string) (ctx: obj) : JS.Promise<unit> =
    promise {
        let task = args.Trim()
        match getSessionIdFromContext ctx with
        | None -> ()
        | Some sessionId ->
            let notify = Dyn.get (Dyn.get ctx "ui") "notify"
            let notifyInfo (msg: string) =
                if Dyn.typeIs notify "function" then
                    emitJsExpr (notify, box msg, box "info")
                        "if (typeof $0 === 'function') $0($1, $2)"
                    |> ignore
            if task = "" then
                store.deactivateReview sessionId
                notifyInfo "loop mode cancelled."
            elif store.isReviewActive sessionId then
                notifyInfo "loop mode is already active."
            else
                store.activateReview(sessionId, task, System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                pi?sendMessage(
                    createObj [
                        "customType", box "kunwei-loop-activate"
                        "content",
                            box(
                                String.concat
                                    "\n"
                                    [ $"Task (loop): {task}"
                                      ""
                                      "Loop mode is active."
                                      "Complete the task, then call submit_review with a detailed report and affected files."
                                      "A reviewer will inspect the work and either accept it or return actionable feedback." ])
                        "display", box true
                    ],
                    createObj [ "triggerTurn", box true ])
                notifyInfo "loop mode is active. Finish the task and call submit_review."
    }

let registerLoopFeatures (pi: obj) (store: ReviewStore) : unit =
    let tb = Dyn.get pi "typebox"
    pi?registerCommand(
        loopCommand,
        createObj [
            "description", box "Enable loop review mode for the current session"
            "handler", box(fun (args: string) (ctx: obj) -> handleLoopCommand pi store args ctx |> ignore)
        ])
    pi?registerCommand("loop-review", createObj
        [ "description", box "Pre-check a task before activating loop mode (mirrors Opencode's command.execute.before)."
          "handler", box(fun (args: string) (ctx: obj) -> handleLoopReviewCommand pi store args ctx |> ignore) ])
    pi?registerTool(
        createObj [
            "name", box "submit_review"
            "label", box "Submit Review"
            "description", box (description "submit_review")
            "parameters",
                objectOf
                    [| ("report", str "Detailed description of what was changed." tb)
                       ("affectedFiles", stringArraySchema pi "Modified or created file path.")
                       ("wip", opt "Defaults to true: record progress without starting a reviewer. Set to false to start the reviewer for final review." tb bool_) |]
                    tb
            "execute",
                box(fun (_id: string) (params': obj) (_s: obj) (_u: obj) (ctx: obj) ->
                    promise {
                        match getSessionIdFromContext ctx with
                        | None -> return errorResult "Loop review is not active for this session."
                        | Some sessionId ->
                            if not (store.isReviewActive sessionId) then
                                return errorResult "Loop review is not active for this session."
                            else
                                let wip = submitReviewIsWip (optBool params' "wip")
                                if wip then
                                    return textResult submitReviewWipAcknowledgment
                                elif not (store.tryLockReview sessionId) then
                                    return errorResult "A review is already in progress."
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
                                    with ex ->
                                        loopError <- Some ex
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
                                        else
                                            return errorResult ("Review feedback:\n\n" + r.feedback.Value)
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
                            match VibeFs.Kernel.ReviewVerdict.parseVerdict (Dyn.str params' "verdict") with
                            | None -> return textResult reviewerNudgePrompt
                            | Some VibeFs.Kernel.ReviewVerdict.Pass ->
                                if not (store.resolvePendingReview(sessionId, Accepted)) then
                                    return errorResult "No pending review to resolve."
                                else
                                    return { textResult "Review submitted: accepted." with display = Some false }
                            | Some VibeFs.Kernel.ReviewVerdict.Reject ->
                                let fb = (Dyn.str params' "feedback").Trim()
                                if fb = "" then return textResult reviewerNudgePrompt
                                elif not (store.resolvePendingReview(sessionId, Rejected fb)) then
                                    return errorResult "No pending review to resolve."
                                else
                                    return { textResult "Review submitted: rejected with feedback." with display = Some false }
                    })
        ])

let registerInputHandler (pi: obj) (store: ReviewStore) : unit =
    pi?on(
        "input",
        box(fun (event: obj) (ctx: obj) ->
            promise {
                let text = (Dyn.str event "text").Trim()
                if not (text.StartsWith("/" + loopCommand)) then
                    return box null
                else
                    let rest = text.Substring(loopCommand.Length + 1).Trim()
                    do! handleLoopCommand pi store rest ctx
                    return createObj [ "handled", box true ]
            }))