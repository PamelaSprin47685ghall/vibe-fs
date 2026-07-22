namespace Wanxiangshu.Next.Tests

open Fable.Core
open Fable.Core.JsInterop
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.OpenCode

module EventDrivenHarness =

    [<Emit("Promise.resolve()")>]
    let yieldMicrotask () : Task<unit> = jsNative

    let rec drainMicrotasks (n: int) : Task<unit> =
        task {
            if n > 0 then
                do! yieldMicrotask ()
                do! drainMicrotasks (n - 1)
        }

    type TestEventStream<'e>() =
        let mutable nextSeq = 0UL
        let store = ResizeArray<uint64 * 'e>()
        let lockObj = obj ()

        member _.Emit(e: 'e) : uint64 =
            lock lockObj (fun () ->
                let s = nextSeq
                nextSeq <- nextSeq + 1UL
                store.Add(s, e)
                s)

        member _.Snapshot() : (uint64 * 'e) list =
            lock lockObj (fun () -> store |> Seq.toList)

        member _.Count() : int = lock lockObj (fun () -> store.Count)

        member _.Items() : 'e list =
            lock lockObj (fun () -> store |> Seq.map snd |> Seq.toList)

        member _.Clear() =
            lock lockObj (fun () ->
                nextSeq <- 0UL
                store.Clear())

    type PromptEvent = { SessionId: string; BodyText: string }

    let assertEventSequence<'e> (label: string) (bus: TestEventStream<'e>) (expected: ('e -> bool) list) : unit =
        let actual = bus.Items()

        if expected.Length > actual.Length then
            Xunit.Assert.True(
                false,
                sprintf "%s | not enough events: expected %d, got %d" label expected.Length actual.Length
            )
        else
            for i in 0 .. expected.Length - 1 do
                let matchOk = expected.[i] actual.[i]
                Xunit.Assert.True(matchOk, sprintf "%s | event[%d] mismatch" label i)

    let driveAndAssert<'e>
        (label: string)
        (bus: TestEventStream<'e>)
        (microtaskRounds: int)
        (expected: ('e -> bool) list)
        : Task<unit> =
        task {
            do! drainMicrotasks microtaskRounds
            assertEventSequence label bus expected
        }
