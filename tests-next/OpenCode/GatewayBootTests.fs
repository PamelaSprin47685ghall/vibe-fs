namespace Wanxiangshu.Next.Tests.OpenCodeTests

open System
open System.IO
open System.Threading
open Xunit
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport

module GatewayBootTests =

    [<Fact>]
    let Gateway_boots_and_initializes_journal () =
        withTempDir (fun dir ->
            let startRes =
                Gateway.start dir CancellationToken.None |> fun t -> t.GetAwaiter().GetResult()

            match startRes with
            | Error err -> Assert.True(false, sprintf "Gateway start failed: %A" err)
            | Ok g ->
                let writerOpt = g.JournalWriter
                Assert.True(writerOpt.IsSome)
                let writer = writerOpt.Value
                Assert.Equal(writer.LocalSeq, g.RuntimeSnapshot.OwnLocalSeq)
                Assert.True(g.RuntimeSnapshot.OwnRuntimeId.IsSome)
                Assert.Equal(g.RuntimeId, g.RuntimeSnapshot.OwnRuntimeId.Value)

                let runtimesDir = Path.Combine(dir, ".wanxiangshu-next", "runtimes")
                let bootSnapshot = Boot.boot runtimesDir

                let ownEnvelopes =
                    bootSnapshot.Envelopes |> List.filter (fun env -> env.RuntimeId = g.RuntimeId)

                Assert.Single(ownEnvelopes) |> ignore

                match ownEnvelopes.[0].Fact with
                | Fact.Runtime(RuntimeStarted _) -> ()
                | _ -> Assert.True(false, "Expected RuntimeStarted fact for own runtime")

                g.DisposeAsync().AsTask().GetAwaiter().GetResult())

    [<Fact>]
    let Gateway_dispose_releases_writer () =
        withTempDir (fun dir ->
            let startRes =
                Gateway.start dir CancellationToken.None |> fun t -> t.GetAwaiter().GetResult()

            match startRes with
            | Error err -> Assert.True(false, sprintf "Gateway start failed: %A" err)
            | Ok g ->
                let writerOpt = g.JournalWriter
                Assert.True(writerOpt.IsSome)
                let writer = writerOpt.Value

                g.DisposeAsync().AsTask().GetAwaiter().GetResult()

                let todoFact = Fact.Todo(TodoChanged {| Snapshot = { Items = [ "t1" ] } |})
                let res = writer.Append StreamId.Workspace None todoFact

                match res with
                | CommitUnknown(eventId, WriteFailed msg) ->
                    Assert.False(String.IsNullOrWhiteSpace(EventId.value eventId))
                    Assert.Contains("disposed", msg, StringComparison.OrdinalIgnoreCase)
                | _ ->
                    Assert.True(
                        false,
                        sprintf "Expected CommitUnknown when writing to disposed Gateway writer, got %A" res
                    ))
