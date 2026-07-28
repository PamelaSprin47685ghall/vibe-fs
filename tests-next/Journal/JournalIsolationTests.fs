namespace Wanxiangshu.Next.Tests.JournalTests

open System
open Fable.Core
open Fable.Core.JsInterop
open Xunit
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session

module private NodeFsIsolation =
    [<Import("appendFileSync", "node:fs")>]
    let appendFileSync (path: string, content: string) : unit = jsNative

    [<Import("readFileSync", "node:fs")>]
    let readFileSync (path: string, encoding: string) : string = jsNative

    [<Import("readdirSync", "node:fs")>]
    let readdirSync (path: string) : string array = jsNative

    [<Import("join", "node:path")>]
    let pathJoin (a: string, b: string) : string = jsNative

module JournalIsolationTests =

    [<Fact>]
    let ``Two runtimes same session both files written third boot merges both`` () =
        JournalTestSupport.withTempDir (fun tempDir ->
            task {
                let runtimeA = RuntimeId.create "rtA"
                let runtimeB = RuntimeId.create "rtB"
                let session1 = SessionId.create "s1"

                let writerA, initEnvA =
                    JournalWriter.create tempDir runtimeA 100 DateTimeOffset.UtcNow

                let writerB, initEnvB =
                    JournalWriter.create tempDir runtimeB 101 DateTimeOffset.UtcNow

                let factA =
                    Fact.Agent(
                        AgentFact.FallbackFailureRecorded
                            {| SessionId = session1
                               LogicalRunId = "run-test"
                               AuthorityRootUserMessageId = "root-test"
                               Reason = "errA"
                               AssistantMessageId = "test-msg-1"
                               ProviderAttempt = "1" |}
                    )

                let factB =
                    Fact.Agent(
                        AgentFact.FallbackFailureRecorded
                            {| SessionId = session1
                               LogicalRunId = "run-test"
                               AuthorityRootUserMessageId = "root-test"
                               Reason = "errB"
                               AssistantMessageId = "test-msg-2"
                               ProviderAttempt = "2" |}
                    )

                let commitA = writerA.Append (StreamId.Session session1) None factA
                let commitB = writerB.Append (StreamId.Session session1) None factB

                Assert.True(
                    match commitA with
                    | Committed _ -> true
                    | _ -> false
                )

                Assert.True(
                    match commitB with
                    | Committed _ -> true
                    | _ -> false
                )

                (writerA :> IDisposable).Dispose()
                (writerB :> IDisposable).Dispose()

                let bootSnapshot = Boot.boot tempDir
                let proj = Fold.apply Fold.empty bootSnapshot.Envelopes

                Assert.True(proj.AgentProjections.Sessions.ContainsKey session1)

                let runtimes =
                    bootSnapshot.Envelopes
                    |> List.map (fun e -> RuntimeId.value e.RuntimeId)
                    |> Set.ofList

                Assert.True(Set.contains "rtA" runtimes)
                Assert.True(Set.contains "rtB" runtimes)
            })

    [<Fact>]
    let ``Runtime A cannot see B appends mid life`` () =
        JournalTestSupport.withTempDir (fun tempDir ->
            task {
                let runtimeA = RuntimeId.create "rtA"
                let runtimeB = RuntimeId.create "rtB"
                let session1 = SessionId.create "s1"

                let writerB, _ = JournalWriter.create tempDir runtimeB 100 DateTimeOffset.UtcNow

                let factB1 =
                    Fact.Agent(
                        AgentFact.FallbackFailureRecorded
                            {| SessionId = session1
                               LogicalRunId = "run-test"
                               AuthorityRootUserMessageId = "root-test"
                               Reason = "errB1"
                               AssistantMessageId = "test-msg-3"
                               ProviderAttempt = "3" |}
                    )

                let _ = writerB.Append StreamId.Workspace None factB1

                let bootSnapshotA = Boot.boot tempDir
                let initialLength = bootSnapshotA.Envelopes.Length

                let factB2 =
                    Fact.Agent(
                        AgentFact.FallbackFailureRecorded
                            {| SessionId = session1
                               LogicalRunId = "run-test"
                               AuthorityRootUserMessageId = "root-test"
                               Reason = "errB2"
                               AssistantMessageId = "test-msg-4"
                               ProviderAttempt = "4" |}
                    )

                let _ = writerB.Append StreamId.Workspace None factB2

                Assert.Equal(initialLength, bootSnapshotA.Envelopes.Length)

                (writerB :> IDisposable).Dispose()

                let bootSnapshotC = Boot.boot tempDir
                Assert.Equal(initialLength + 1, bootSnapshotC.Envelopes.Length)
            })

    [<Fact>]
    let ``Partial trailing line ignored writer file unchanged by boot`` () =
        JournalTestSupport.withTempDir (fun tempDir ->
            task {
                let runtimeA = RuntimeId.create "rtA"
                let session1 = SessionId.create "s1"
                let writerA, _ = JournalWriter.create tempDir runtimeA 100 DateTimeOffset.UtcNow

                let factA =
                    Fact.Agent(
                        AgentFact.FallbackFailureRecorded
                            {| SessionId = session1
                               LogicalRunId = "run-test"
                               AuthorityRootUserMessageId = "root-test"
                               Reason = "err1"
                               AssistantMessageId = "test-msg-5"
                               ProviderAttempt = "5" |}
                    )

                let _ = writerA.Append StreamId.Workspace None factA
                (writerA :> IDisposable).Dispose()

                let fileA = NodeFsIsolation.pathJoin (tempDir, "rtA.ndjson")

                let partialContent =
                    "{\"RuntimeId\":\"rtA\",\"LocalSeq\":3,\"ObservedAt\":\"2026-01-01T00:00:00Z\",\"EventId\":\"evt_partial"

                NodeFsIsolation.appendFileSync (fileA, partialContent)

                let fileBytesBefore = NodeFsIsolation.readFileSync (fileA, "utf-8")
                let fileLengthBefore = fileBytesBefore.Length

                let bootSnapshot = Boot.boot tempDir

                Assert.Equal(2, bootSnapshot.Envelopes.Length)

                let fileBytesAfter = NodeFsIsolation.readFileSync (fileA, "utf-8")
                let fileLengthAfter = fileBytesAfter.Length

                Assert.Equal(fileLengthBefore, fileLengthAfter)
                Assert.Equal(fileBytesBefore, fileBytesAfter)
            })

    [<Fact>]
    let ``No lockfile created under temp dir`` () =
        JournalTestSupport.withTempDir (fun tempDir ->
            task {
                let runtimeA = RuntimeId.create "rtA"
                let session1 = SessionId.create "s1"
                let writerA, _ = JournalWriter.create tempDir runtimeA 100 DateTimeOffset.UtcNow

                let factA =
                    Fact.Agent(
                        AgentFact.FallbackFailureRecorded
                            {| SessionId = session1
                               LogicalRunId = "run-test"
                               AuthorityRootUserMessageId = "root-test"
                               Reason = "err1"
                               AssistantMessageId = "test-msg-6"
                               ProviderAttempt = "6" |}
                    )

                let _ = writerA.Append StreamId.Workspace None factA

                let allFiles = NodeFsIsolation.readdirSync tempDir

                let lockFiles =
                    allFiles
                    |> Array.filter (fun f -> f.EndsWith(".lock") || f.Contains("lockfile"))

                Assert.Empty(lockFiles)

                (writerA :> IDisposable).Dispose()
                return ()
            })
