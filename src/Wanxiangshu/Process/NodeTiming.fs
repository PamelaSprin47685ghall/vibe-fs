namespace Wanxiangshu.Process

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Foundation

module NodeTiming =
    let timerTask (milliseconds: int) : Task<unit> =
        let completion = TaskCompletionSource<unit>()

        if milliseconds >= 1000 then
            emitJsExpr (milliseconds, (fun () -> completion.SetResult())) "setTimeout($1, $0).unref()"
            |> ignore
        else
            emitJsExpr (milliseconds, (fun () -> completion.SetResult())) "setTimeout($1, $0)"
            |> ignore

        completion.Task

    let raceExit (exitTask: Task) (milliseconds: int) : Task<bool> =
        emitJsExpr
            (exitTask, milliseconds)
            """
            new Promise((resolve, reject) => {
                let timer = null;
                $0.then(
                    () => {
                        if (timer !== null) { clearTimeout(timer); timer = null; }
                        resolve(true);
                    },
                    (err) => {
                        if (timer !== null) { clearTimeout(timer); timer = null; }
                        reject(err);
                    }
                );
                timer = setTimeout(() => {
                    timer = null;
                    resolve(false);
                }, $1);
                if ($1 >= 1000 && timer !== null && typeof timer.unref === 'function') {
                    timer.unref();
                }
            })
            """

    let nodeTimerPort () : ITimerPort =
        // DSL-MUTABLE: cancellation — port disposed latch
        let mutable disposed = false

        { new ITimerPort with
            member _.Delay(milliseconds: int) =
                let completion = TaskCompletionSource<unit>()
                // DSL-MUTABLE: cancellation — handle cancel latch
                let mutable cancelled = false

                let fire () =
                    if not cancelled && not disposed then
                        AsyncSupport.trySetResult completion () |> ignore

                let timerId: obj =
                    if milliseconds >= 1000 then
                        emitJsExpr (milliseconds, fire) "setTimeout($1, $0).unref()"
                    else
                        emitJsExpr (milliseconds, fire) "setTimeout($1, $0)"

                { new IDeadlineHandle with
                    member _.Delay = completion.Task

                    member _.Cancel() =
                        if not cancelled then
                            cancelled <- true
                            emitJsExpr timerId "clearTimeout($0)" |> ignore }

            member _.Dispose() = disposed <- true }

    let nodeClockPort () : IClockPort =
        { new IClockPort with
            member _.UtcNow() = DateTimeOffset.UtcNow }
