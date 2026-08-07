namespace Wanxiangshu.Process

open System.Threading.Tasks
open Fable.Core.JsInterop

/// Cancelable one-shot timer handle. Cancel leaves Delay permanently pending.
type ITimerHandle =
    abstract Delay: Task<unit>
    abstract Cancel: unit -> unit

/// Central delay port (VERIFY-004): production = Node timer, test = virtual clock.
/// Long budgets (≥1000ms) unref so a clean process is not held open.
type ITimerPort =
    abstract Delay: milliseconds: int -> ITimerHandle
    abstract Dispose: unit -> unit

/// Virtual-clock control surface for ITimerPort contract tests.
type VirtualTimerPort =
    { Port: ITimerPort
      Advance: int -> unit
      NowMs: unit -> int }

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

    /// Production Node timer port: setTimeout + clearTimeout; ms ≥ 1000 → unref.
    let nodeTimerPort () : ITimerPort =
        let mutable disposed = false

        { new ITimerPort with
            member _.Delay(milliseconds: int) =
                let completion = TaskCompletionSource<unit>()
                let mutable cancelled = false

                let fire () =
                    if not cancelled && not disposed then
                        completion.TrySetResult() |> ignore

                let timerId: obj =
                    if milliseconds >= 1000 then
                        emitJsExpr (milliseconds, fire) "setTimeout($1, $0).unref()"
                    else
                        emitJsExpr (milliseconds, fire) "setTimeout($1, $0)"

                { new ITimerHandle with
                    member _.Delay = completion.Task

                    member _.Cancel() =
                        if not cancelled then
                            cancelled <- true
                            emitJsExpr timerId "clearTimeout($0)" |> ignore }

            member _.Dispose() = disposed <- true }

    /// Virtual clock for tests: Advance fires due handles; Cancel/Dispose → zero callbacks.
    let createVirtualTimerPort () : VirtualTimerPort =
        let mutable nowMs = 0
        let mutable disposed = false
        let mutable nextId = 0
        // (id, fireAtMs, tcs, cancelled ref)
        let entries = ResizeArray<int * int * TaskCompletionSource<unit> * bool ref>()

        let removeId (id: int) =
            let idx = entries |> Seq.tryFindIndex (fun (entryId, _, _, _) -> entryId = id)

            match idx with
            | Some i -> entries.RemoveAt(i)
            | None -> ()

        let port =
            { new ITimerPort with
                member _.Delay(milliseconds: int) =
                    let completion = TaskCompletionSource<unit>()
                    let cancelled = ref false
                    let id = nextId
                    nextId <- nextId + 1
                    let fireAt = nowMs + max 0 milliseconds
                    entries.Add((id, fireAt, completion, cancelled))

                    { new ITimerHandle with
                        member _.Delay = completion.Task

                        member _.Cancel() =
                            if not cancelled.Value then
                                cancelled.Value <- true
                                removeId id }

                member _.Dispose() =
                    disposed <- true
                    entries.Clear() }

        let advance (milliseconds: int) =
            if disposed then
                ()
            else
                nowMs <- nowMs + max 0 milliseconds

                let due =
                    entries
                    |> Seq.filter (fun (_, fireAt, _, cancelled) -> not cancelled.Value && fireAt <= nowMs)
                    |> Seq.toList

                for id, _, completion, cancelled in due do
                    if not cancelled.Value then
                        cancelled.Value <- true
                        removeId id
                        completion.TrySetResult() |> ignore

        { Port = port
          Advance = advance
          NowMs = fun () -> nowMs }
