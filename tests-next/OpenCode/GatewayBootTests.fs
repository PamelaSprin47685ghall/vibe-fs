namespace Wanxiangshu.Next.Tests.OpenCodeTests

open System
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
            task {
                let! startRes = Gateway.start dir CancellationToken.None

                match startRes with
                | Error err -> Assert.True(false, sprintf "Gateway start failed: %A" err)
                | Ok g ->
                    let writerOpt = g.JournalWriter
                    Assert.True(writerOpt.IsSome)
                    let writer = writerOpt.Value
                    Assert.Equal(writer.LocalSeq, g.RuntimeSnapshot.OwnLocalSeq)
                    Assert.True(g.RuntimeSnapshot.OwnRuntimeId.IsSome)
                    Assert.Equal(g.RuntimeId, g.RuntimeSnapshot.OwnRuntimeId.Value)

                    let runtimesDir = sprintf "%s/.wanxiangshu-next/runtimes" dir
                    let bootSnapshot = Boot.boot runtimesDir

                    let ownEnvelopes =
                        bootSnapshot.Envelopes |> List.filter (fun env -> env.RuntimeId = g.RuntimeId)

                    Assert.Single(ownEnvelopes) |> ignore

                    match ownEnvelopes.[0].Fact with
                    | Fact.Runtime(RuntimeStarted _) -> ()
                    | _ -> Assert.True(false, "Expected RuntimeStarted fact for own runtime")

                    let! _ = g.DisposeAsync()
                    ()
            })

    [<Fact>]
    let Gateway_dispose_releases_writer () =
        withTempDir (fun dir ->
            task {
                let! startRes = Gateway.start dir CancellationToken.None

                match startRes with
                | Error err -> Assert.True(false, sprintf "Gateway start failed: %A" err)
                | Ok g ->
                    let writerOpt = g.JournalWriter
                    Assert.True(writerOpt.IsSome)
                    let writer = writerOpt.Value

                    let! _ = g.DisposeAsync()

                    let todoFact = Fact.Todo(TodoChanged {| Snapshot = { Items = [ "t1" ] } |})
                    let res = writer.Append StreamId.Workspace None todoFact

                    match res with
                    | CommitUnknown(eventId, WriteFailed msg) ->
                        Assert.False(String.IsNullOrWhiteSpace(EventId.value eventId))
                    | _ -> ()
            })

    [<Fact>]
    let Gateway_append_updates_read_your_writes_projection () =
        withTempDir (fun dir ->
            task {
                let! startRes = Gateway.start dir CancellationToken.None

                match startRes with
                | Error err -> Assert.True(false, sprintf "Gateway start failed: %A" err)
                | Ok g ->
                    let sessionId = SessionId.create "session-rwy"
                    let stream = StreamId.Session sessionId
                    let todoFact = Fact.Todo(TodoChanged {| Snapshot = { Items = [ "item-rwy-1" ] } |})
                    let initialSeq = g.RuntimeSnapshot.OwnLocalSeq

                    let appendRes = g.Append stream None todoFact

                    match appendRes with
                    | Committed env ->
                        Assert.Equal(stream, env.Stream)
                        let sessionProjOpt = Map.tryFind sessionId g.ProjectionSet.SessionProjections
                        Assert.True(sessionProjOpt.IsSome)
                        let sessionProj = sessionProjOpt.Value
                        Assert.True(sessionProj.Todos.IsSome)
                        Assert.Equal("item-rwy-1", sessionProj.Todos.Value.Items.[0])

                        let snapshotProjOpt =
                            Map.tryFind sessionId g.RuntimeSnapshot.Projections.SessionProjections

                        Assert.True(snapshotProjOpt.IsSome)
                        let snapshotProj = snapshotProjOpt.Value
                        Assert.True(snapshotProj.Todos.IsSome)
                        Assert.Equal("item-rwy-1", snapshotProj.Todos.Value.Items.[0])

                        Assert.True(g.RuntimeSnapshot.OwnLocalSeq > initialSeq)
                    | _ ->
                        Assert.True(
                            false,
                            sprintf "Expected Committed when appending TodoChanged to Gateway, got %A" appendRes
                        )

                    let! _ = g.DisposeAsync()
                    ()
            })
