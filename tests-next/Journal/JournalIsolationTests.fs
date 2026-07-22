namespace Wanxiangshu.Next.Tests.JournalTests

open System
open System.IO
open System.Security.Cryptography
open Xunit
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session

module JournalIsolationTests =

    [<Fact>]
    let ``Two runtimes same session both files written third boot merges both`` () =
        JournalTestSupport.withTempDir (fun tempDir ->
            task {
                let runtimeA = RuntimeId.create "rtA"
                let runtimeB = RuntimeId.create "rtB"

                let writerA, initEnvA =
                    JournalWriter.create tempDir runtimeA 100 DateTimeOffset.UtcNow

                let writerB, initEnvB =
                    JournalWriter.create tempDir runtimeB 101 DateTimeOffset.UtcNow

                let factA = Fact.Session(Fact.HumanTurnStarted {| TurnId = TurnId.create "turn1" |})
                let factB = Fact.Session(Fact.HumanTurnStarted {| TurnId = TurnId.create "turn2" |})

                let commitA = writerA.Append StreamId.Workspace None factA
                let commitB = writerB.Append StreamId.Workspace None factB

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

                do! (writerA :> IAsyncDisposable).DisposeAsync().AsTask()
                do! (writerB :> IAsyncDisposable).DisposeAsync().AsTask()

                let bootSnapshot = Boot.boot tempDir

                Assert.True(bootSnapshot.Envelopes.Length >= 4)

                let runtimes =
                    bootSnapshot.Envelopes
                    |> List.map (fun e -> RuntimeId.value e.RuntimeId)
                    |> Set.ofList

                Assert.True(Set.contains "rtA" runtimes)
                Assert.True(Set.contains "rtB" runtimes)
            }
            |> Async.AwaitTask
            |> Async.RunSynchronously)

    [<Fact>]
    let ``Runtime A cannot see B appends mid life`` () =
        JournalTestSupport.withTempDir (fun tempDir ->
            task {
                let runtimeA = RuntimeId.create "rtA"
                let runtimeB = RuntimeId.create "rtB"

                let writerB, _ = JournalWriter.create tempDir runtimeB 100 DateTimeOffset.UtcNow

                let factB1 =
                    Fact.Session(Fact.HumanTurnStarted {| TurnId = TurnId.create "turnB1" |})

                let _ = writerB.Append StreamId.Workspace None factB1

                let bootSnapshotA = Boot.boot tempDir
                let initialLength = bootSnapshotA.Envelopes.Length

                let factB2 =
                    Fact.Session(Fact.HumanTurnStarted {| TurnId = TurnId.create "turnB2" |})

                let _ = writerB.Append StreamId.Workspace None factB2

                Assert.Equal(initialLength, bootSnapshotA.Envelopes.Length)

                do! (writerB :> IAsyncDisposable).DisposeAsync().AsTask()

                let bootSnapshotC = Boot.boot tempDir
                Assert.Equal(initialLength + 1, bootSnapshotC.Envelopes.Length)
            }
            |> Async.AwaitTask
            |> Async.RunSynchronously)

    [<Fact>]
    let ``Partial trailing line ignored writer file unchanged by boot`` () =
        JournalTestSupport.withTempDir (fun tempDir ->
            task {
                let runtimeA = RuntimeId.create "rtA"
                let writerA, _ = JournalWriter.create tempDir runtimeA 100 DateTimeOffset.UtcNow
                let factA = Fact.Session(Fact.HumanTurnStarted {| TurnId = TurnId.create "turn1" |})
                let _ = writerA.Append StreamId.Workspace None factA
                do! (writerA :> IAsyncDisposable).DisposeAsync().AsTask()

                let fileA = Path.Combine(tempDir, "rtA.ndjson")

                let partialContent =
                    "{\"RuntimeId\":\"rtA\",\"LocalSeq\":3,\"ObservedAt\":\"2026-01-01T00:00:00Z\",\"EventId\":\"evt_partial"

                File.AppendAllText(fileA, partialContent)

                let fileBytesBefore = File.ReadAllBytes(fileA)
                let fileLengthBefore = fileBytesBefore.Length
                let hashBefore = SHA256.HashData(fileBytesBefore)

                let bootSnapshot = Boot.boot tempDir

                Assert.Equal(2, bootSnapshot.Envelopes.Length)

                let fileBytesAfter = File.ReadAllBytes(fileA)
                let fileLengthAfter = fileBytesAfter.Length
                let hashAfter = SHA256.HashData(fileBytesAfter)

                Assert.Equal(fileLengthBefore, fileLengthAfter)
                Assert.Equal<byte seq>(hashBefore, hashAfter)
            }
            |> Async.AwaitTask
            |> Async.RunSynchronously)

    [<Fact>]
    let ``No lockfile created under temp dir`` () =
        JournalTestSupport.withTempDir (fun tempDir ->
            task {
                let runtimeA = RuntimeId.create "rtA"
                let writerA, _ = JournalWriter.create tempDir runtimeA 100 DateTimeOffset.UtcNow
                let factA = Fact.Session(Fact.HumanTurnStarted {| TurnId = TurnId.create "turn1" |})
                let _ = writerA.Append StreamId.Workspace None factA

                let allFiles = Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories)

                let lockFiles =
                    allFiles
                    |> Array.filter (fun f -> f.EndsWith(".lock") || f.Contains("lockfile"))

                Assert.Empty(lockFiles)

                do! (writerA :> IAsyncDisposable).DisposeAsync().AsTask()
            }
            |> Async.AwaitTask
            |> Async.RunSynchronously)

    [<Fact>]
    let ``Prompt duplicate key historical prompts after fold`` () =
        let runtimeId = RuntimeId.create "rt1"
        let sessionId = SessionId.create "s1"
        let turnId = TurnId.create "t1"

        let promptKey =
            PromptKey.create sessionId turnId PromptPurpose.ContinueTodo None 1 None "hash1"

        let keyString = PromptKey.asString promptKey

        let envReq: Envelope =
            { RuntimeId = runtimeId
              LocalSeq = LocalSeq.create 1L
              ObservedAt = DateTimeOffset.UtcNow
              EventId = EventId.create "evt1"
              Stream = StreamId.Workspace
              TurnId = None
              Fact =
                Fact.Prompt(
                    Fact.PromptRequested
                        {| PromptKey = keyString
                           TurnId = turnId
                           Purpose = "ContinueTodo" |}
                ) }

        let envSub: Envelope =
            { RuntimeId = runtimeId
              LocalSeq = LocalSeq.create 2L
              ObservedAt = DateTimeOffset.UtcNow
              EventId = EventId.create "evt2"
              Stream = StreamId.Workspace
              TurnId = None
              Fact =
                Fact.Prompt(
                    Fact.PromptSubmitted
                        {| PromptKey = keyString
                           MessageId = MessageId.create "m1" |}
                ) }

        let envTerm: Envelope =
            { RuntimeId = runtimeId
              LocalSeq = LocalSeq.create 3L
              ObservedAt = DateTimeOffset.UtcNow
              EventId = EventId.create "evt3"
              Stream = StreamId.Workspace
              TurnId = None
              Fact =
                Fact.Prompt(
                    Fact.PromptTerminal
                        {| PromptKey = keyString
                           Outcome = PromptOutcome.Delivered(MessageId.create "m2")
                           AssistantMessageId = Some(MessageId.create "m2") |}
                ) }

        let envelopes = [ envReq; envSub; envTerm ]
        let projSet = Fold.apply Fold.empty envelopes

        Assert.True(projSet.HistoricalPrompts.ContainsKey keyString)

        let historicalIndex =
            PromptProtocol.rebuildHistoricalIndex projSet.HistoricalPrompts

        let decision =
            PromptProtocol.evaluateSendOnce historicalIndex PromptProtocol.emptyLocalProtocol promptKey

        match decision with
        | SendOnceDecision.HistoricalHit history ->
            Assert.Equal(keyString, history.Key)
            Assert.True(history.Outcome.IsSome)
        | other -> Assert.Fail(sprintf "Expected HistoricalHit, got %A" other)
