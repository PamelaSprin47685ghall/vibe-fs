module Wanxiangshu.Hosts.Omp.ReviewToolsLoop

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.ReviewPrompts
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Hosts.Omp.Codec
open Wanxiangshu.Hosts.Omp.MessagingCodec
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.EventLogRuntime
open Wanxiangshu.Runtime.Dyn

let loopCommand = "loop"

let handleLoopCommand (pi: obj) (store: ReviewStore) (args: string) (ctx: obj) : JS.Promise<unit> =
    promise {
        let task = args.Trim()

        match getSessionIdFromContext ctx with
        | None -> ()
        | Some sessionId ->
            let notify = get (get ctx "ui") "notify"
            let root = str ctx "cwd"

            let notifyInfo (msg: string) =
                if typeIs notify "function" then
                    emitJsExpr (notify, box msg, box "info") "if (typeof $0 === 'function') $0($1, $2)"
                    |> ignore

            if task = "" then
                do! appendLoopCancelledOrFail root sessionId
                do! syncReviewFromEventLogDedicated store root sessionId
                notifyInfo "With-Review Mode cancelled."
            elif store.getReviewTask sessionId |> Option.isSome then
                notifyInfo "With-Review Mode is already active."
            else
                do! appendLoopActivatedOrFail root sessionId task
                do! syncReviewFromEventLogDedicated store root sessionId

                pi?sendMessage (
                    createObj
                        [ "customType", box "wanxiangshu-loop-activated"
                          "content",
                          box (
                              String.concat
                                  "\n"
                                  [ $"Task (loop): {task}"
                                    ""
                                    "With-Review Mode is active. Complete the task above, then call submit_review with:" ]
                          )
                          "display", box true ],
                    createObj [ "triggerTurn", box true ]
                )

                notifyInfo "With-Review Mode activated."
    }
