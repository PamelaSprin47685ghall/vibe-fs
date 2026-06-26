module VibeFs.Omp.ReviewToolsLoop

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.ReviewPrompts
open VibeFs.Kernel.ReviewSession.Types
open VibeFs.Omp.Codec
open VibeFs.Omp.ReviewLoop
module Dyn = VibeFs.Shell.Dyn
open VibeFs.Shell.ReviewRuntime

let loopCommand = "loop"

let handleLoopReviewCommand (pi: obj) (store: ReviewStore) (args: string) (ctx: obj) : JS.Promise<unit> =
    promise {
        let task = args.Trim()
        match getSessionIdFromContext ctx with
        | None -> ()
        | Some sessionId ->
            let notify = Dyn.get (Dyn.get ctx "ui") "notify"
            let notifyInfo (msg: string) =
                if Dyn.typeIs notify "function" then
                    emitJsExpr (notify, box msg, box "info") "if (typeof $0 === 'function') $0($1, $2)" |> ignore
            if task = "" then notifyInfo "loop-review needs a task. Try /loop-review <task>."
            elif store.isReviewActive sessionId then notifyInfo "loop mode is already active."
            else
                let! result = runPreReviewerSession pi ctx store task
                match result with
                | Accepted -> notifyInfo $"Pre-review passed. Task \"{task}\" already meets criteria — no loop needed."
                | Terminated -> notifyInfo "Pre-review could not complete."
                | Rejected feedback ->
                    store.activateReview(sessionId, task, System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                    pi?sendMessage(
                        createObj [
                            "customType", box "kunwei-loop-precheck"
                            "content",
                                box(
                                    String.concat
                                        "\n"
                                        [ $"Task (loop): {task}"; ""; "=== Pre-review Feedback ==="; ""; feedback
                                          ""; "Address the feedback above, then call submit_review with a detailed report and affected files." ])
                            "display", box true
                        ],
                        createObj [ "triggerTurn", box true ])
                    notifyInfo "Pre-review found issues. Loop mode is active — address feedback and submit_review."
    }

let handleLoopCommand (pi: obj) (store: ReviewStore) (args: string) (ctx: obj) : JS.Promise<unit> =
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
                store.deactivateReview sessionId
                notifyInfo "loop mode cancelled."
            elif store.isReviewActive sessionId then notifyInfo "loop mode is already active."
            else
                store.activateReview(sessionId, task, System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                pi?sendMessage(
                    createObj [
                        "customType", box "kunwei-loop-activate"
                        "content",
                            box(
                                String.concat
                                    "\n"
                                    [ $"Task (loop): {task}"; ""; "Loop mode is active."
                                      "Complete the task, then call submit_review with a detailed report and affected files."
                                      "A reviewer will inspect the work and either accept it or return actionable feedback." ])
                        "display", box true
                    ],
                    createObj [ "triggerTurn", box true ])
                notifyInfo "loop mode is active. Finish the task and call submit_review."
    }