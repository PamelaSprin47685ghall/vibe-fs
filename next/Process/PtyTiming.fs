namespace Wanxiangshu.Next.Process

open System.Threading.Tasks
open Fable.Core.JsInterop

module PtyTiming =
    let timerTask (milliseconds: int) : Task<unit> =
        let completion = TaskCompletionSource<unit>()

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
