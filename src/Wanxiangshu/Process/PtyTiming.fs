namespace Wanxiangshu.Process

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Kernel

/// Compatibility alias — prefer Kernel.IDeadlineHandle (G4R-CE S1 transition).
type ITimerHandle = IDeadlineHandle

/// Virtual-clock control surface for ITimerPort contract tests.
type VirtualTimerPort =
    { Port: ITimerPort
      Advance: int -> unit
      NowMs: unit -> int }

/// Deterministic clock control surface for IClockPort contract tests.
type VirtualClockPort =
    { Port: IClockPort
      AdvanceMs: int -> unit
      Set: DateTimeOffset -> unit }

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

    /// Production wall clock (physical adapter — Kernel contract only in Temporal.fs).
    let nodeClockPort () : IClockPort =
        { new IClockPort with
            member _.UtcNow() = DateTimeOffset.UtcNow }

    /// Virtual clock for tests: Advance fires due handles; Cancel/Dispose → zero callbacks.
    let createVirtualTimerPort () : VirtualTimerPort =
        // DSL-MUTABLE: resource — virtual clock cursor
        let mutable nowMs = 0
        // DSL-MUTABLE: cancellation — port disposed latch
        let mutable disposed = false
        // DSL-MUTABLE: resource — monotonic handle id counter
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

                    { new IDeadlineHandle with
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
                        AsyncSupport.trySetResult completion () |> ignore

        { Port = port
          Advance = advance
          NowMs = fun () -> nowMs }

    /// Deterministic IClockPort for tests (independent of virtual timer cursor).
    let createVirtualClockPort () : VirtualClockPort =
        // DSL-MUTABLE: resource — virtual wall-clock cursor
        let mutable now = DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero)

        let port =
            { new IClockPort with
                member _.UtcNow() = now }

        { Port = port
          AdvanceMs =
            fun ms ->
                now <- now.AddMilliseconds(float (max 0 ms))
          Set = fun value -> now <- value }
