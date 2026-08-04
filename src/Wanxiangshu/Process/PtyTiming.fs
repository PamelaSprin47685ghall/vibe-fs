namespace Wanxiangshu.Process

open System.Threading.Tasks
open Fable.Core.JsInterop

module PtyTiming =
    let timerTask (milliseconds: int) : Task<unit> =
        let completion = TaskCompletionSource<unit>()

        // The timeout arm of `raceExit` must not hold the event loop: a completion
        // that wins the race leaves this timer armed, and an armed-but-blocking
        // setTimeout keeps a clean process alive for the whole bound (measured: a
        // unit/integration child parked 10 minutes on the one-shot tool's 600s
        // timer). unref keeps the timer firing when the loop is otherwise live —
        // the PTY/child handles are — and lets a done process exit immediately.
        // Same pattern as HostSignalSubscribe's heartbeat and Watchdog._arm.
        emitJsExpr (milliseconds, (fun () -> completion.SetResult())) "setTimeout($1, $0).unref()"
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
