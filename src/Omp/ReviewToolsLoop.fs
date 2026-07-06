module Wanxiangshu.Omp.ReviewToolsLoop

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.ReviewPrompts
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Omp.Codec
open Wanxiangshu.Omp.ExecutorTools
open Wanxiangshu.Omp.ReviewLoop
open Wanxiangshu.Omp.MessagingCodec
open Wanxiangshu.Shell.RuntimeScope
open Wanxiangshu.Shell.ReviewRuntime
open Wanxiangshu.Shell.Clock
open Wanxiangshu.Shell.EventLogRuntime
open Wanxiangshu.Shell.Dyn

let loopCommand = "loop"

let handleLoopReviewCommand (pi: obj) (store: ReviewStore) (args: string) (ctx: obj) : JS.Promise<unit> =
    promise {
        let task = args.Trim()
        match getSessionIdFromContext ctx with
        | None -> ()
        | Some sessionId ->
            let notify = get (get ctx "ui") "notify"
            let sm = get ctx "sessionManager"
            let notifyInfo (msg: string) =
                if typeIs notify "function" then
                    emitJsExpr (notify, box msg, box "info") "if (typeof $0 === 'function') $0($1, $2)" |> ignore
            if task = "" then notifyInfo "loop-review needs a task. Try /loop-review <task>."
            else
                let root = str ctx "cwd"
                do! syncReviewFromEventLog store root sessionId
                if store.getReviewTask sessionId |> Option.isSome then notifyInfo "loop mode is already active."
                else
                let! (result: ReviewResult) = runPreReviewerSession ompScope pi ctx store task
                match result with
                | Accepted _ -> notifyInfo $"Pre-review passed. Task \"{task}\" already meets criteria — no loop needed."
                | Terminated -> notifyInfo "Pre-review could not complete."
                | NeedsRevision feedback ->
                    do! appendLoopActivatedOrFail root sessionId task
                    store.activateReview(sessionId, task, getTimestampMs())
                    pi?sendMessage(
                        createObj [
                            "customType", box "wanxiangshu-loop-precheck"
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
            let notify = get (get ctx "ui") "notify"
            let sm = get ctx "sessionManager"
            let notifyInfo (msg: string) =
                if typeIs notify "function" then
                    emitJsExpr (notify, box msg, box "info") "if (typeof $0 === 'function') $0($1, $2)" |> ignore
            let root = str ctx "cwd"
            if task = "" then
                do! appendLoopCancelledOrFail root sessionId
                store.deactivateReview sessionId
                notifyInfo "loop mode cancelled."
            else
                do! syncReviewFromEventLog store root sessionId
                if store.getReviewTask sessionId |> Option.isSome then notifyInfo "loop mode is already active."
                else
                do! appendLoopActivatedOrFail root sessionId task
                store.activateReview(sessionId, task, getTimestampMs())
                pi?sendMessage(
                    createObj [
                        "customType", box "wanxiangshu-loop-activate"
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
