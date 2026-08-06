namespace Wanxiangshu.Process

open System.Threading.Tasks
open Fable.Core.JsInterop

module PtyTiming =
    let timerTask (milliseconds: int) : Task<unit> =
        let completion = TaskCompletionSource<unit>()

        // Long production budgets (e.g. Join default 600s) must not keep a clean
        // process alive after the real work finished: unref those timers.
        // Short unit/race budgets must KEEP the event loop: under node:test
        // concurrency an unref'd timer can fire only after the loop has already
        // drained → "Promise resolution is still pending but the event loop has
        // already resolved" (CI Node 20). Threshold = LITERAL_BUDGET_THRESHOLD.
        if milliseconds >= 1000 then
            emitJsExpr (milliseconds, (fun () -> completion.SetResult())) "setTimeout($1, $0).unref()"
            |> ignore
        else
            emitJsExpr (milliseconds, (fun () -> completion.SetResult())) "setTimeout($1, $0)"
            |> ignore

        completion.Task

    let raceExit (exitTask: Task) (milliseconds: int) : Task<bool> =
        let exited =
            task {
                do! exitTask
                return true
            }

        let elapsed =
            task {
                do! timerTask milliseconds
                return false
            }

        emitJsExpr (exited, elapsed) "Promise.race([$0, $1])"
