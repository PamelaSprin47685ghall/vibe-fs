namespace Wanxiangshu.Next.Tests.JournalTests

open System
open System.IO
open Xunit
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Outcome
open JournalTestSupport

module JournalWriterTests =

    [<Fact>]
    let Writer_writes_RuntimeStarted_and_appends () =
        withTempDir (fun dir ->
            let runtimeId = RuntimeId.create "rt-writer-test"
            let now = DateTimeOffset.UtcNow

            let writer, initEnv = JournalWriter.create dir runtimeId 1234 now

            using writer (fun writer ->
                Assert.Equal(LocalSeq.create 1L, initEnv.LocalSeq)

                match initEnv.Fact with
                | Fact.Runtime(RuntimeStarted _) -> ()
                | _ -> Assert.True(false, "Expected RuntimeStarted fact in initEnv")

                Assert.Equal("rt-writer-test", RuntimeId.value writer.RuntimeId)
                Assert.False(writer.IsPoisoned)

                let todoFact = Fact.Todo(TodoChanged {| Snapshot = { Items = [ "t1" ] } |})
                let result = writer.Append StreamId.Workspace None todoFact

                match result with
                | Committed env ->
                    Assert.Equal(runtimeId, env.RuntimeId)
                    Assert.Equal(LocalSeq.create 2L, env.LocalSeq)
                    Assert.Equal(StreamId.Workspace, env.Stream)
                | CommitUnknown(id, err) ->
                    Assert.True(false, sprintf "Expected Committed, got CommitUnknown: %A %A" id err)

                let snapshot = Boot.boot dir
                Assert.Equal(2, snapshot.Envelopes.Length)

                let proj = Fold.apply Fold.empty snapshot.Envelopes
                Assert.True(proj.Todos.IsSome)
                let items = proj.Todos.Value.Items
                Assert.Single(items) |> ignore
                Assert.Equal("t1", items.[0])))

    [<Fact>]
    let CreateNew_collision_fails () =
        withTempDir (fun dir ->
            let runtimeId = RuntimeId.create "rt-collision"
            let now = DateTimeOffset.UtcNow

            let writer1, _ = JournalWriter.create dir runtimeId 100 now
            use _w1 = writer1

            Assert.Throws<IOException>(fun () -> JournalWriter.create dir runtimeId 101 now |> ignore)
            |> ignore)

    [<Fact>]
    let Poisoned_writer_returns_CommitUnknown () =
        withTempDir (fun dir ->
            let runtimeId = RuntimeId.create "rt-poison"
            let now = DateTimeOffset.UtcNow
            let writer, _ = JournalWriter.create dir runtimeId 100 now

            (writer :> IDisposable).Dispose()

            let fact = Fact.Todo(TodoChanged {| Snapshot = { Items = [] } |})
            let res = writer.Append StreamId.Workspace None fact

            match res with
            | CommitUnknown(eventId, WriteFailed msg) ->
                Assert.False(String.IsNullOrWhiteSpace(EventId.value eventId))
                Assert.Contains("disposed", msg, StringComparison.OrdinalIgnoreCase)
            | _ -> Assert.True(false, "Expected CommitUnknown when writing to disposed writer"))

    [<Fact>]
    let Append_is_serialized_under_concurrency () =
        withTempDir (fun dir ->
            let runtimeId = RuntimeId.create "rt-concurrent"
            let now = DateTimeOffset.UtcNow
            let writer, _ = JournalWriter.create dir runtimeId 100 now

            using writer (fun w ->
                let count = 20

                let tasks =
                    [| 1..count |]
                    |> Array.map (fun i ->
                        System.Threading.Tasks.Task.Run(fun () ->
                            let fact =
                                Fact.Todo(TodoChanged {| Snapshot = { Items = [ sprintf "item%d" i ] } |})

                            w.Append StreamId.Workspace None fact))

                let results = System.Threading.Tasks.Task.WhenAll(tasks).GetAwaiter().GetResult()

                let seqs =
                    results
                    |> Array.choose (function
                        | Committed env -> Some(LocalSeq.value env.LocalSeq)
                        | _ -> None)
                    |> Array.sort

                Assert.Equal(count, seqs.Length)
                Assert.Equal<int64 seq>([| 2L .. 21L |], seqs)

                let snapshot = Boot.boot dir
                Assert.Equal(21, snapshot.Envelopes.Length)))
