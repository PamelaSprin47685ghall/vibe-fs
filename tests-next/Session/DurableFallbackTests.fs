namespace Wanxiangshu.Next.Tests.Session

open System
open System.Threading.Tasks
open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tests.JournalTests
open JournalTestSupport


module private DurableFallbackTestSupport =
    let mutable private seqCounter = 0

    let recordFailure (journalPort: FallbackJournalPort) (sessionId: SessionId) (reason: string) =
        seqCounter <- seqCounter + 1
        let n = seqCounter

        let fact =
            AgentFact.FallbackFailureRecorded
                {| SessionId = sessionId
                   LogicalRunId = "run-test"
                   AuthorityRootUserMessageId = "root-test"
                   Reason = reason
                   AssistantMessageId = sprintf "test-msg-%s-%d" (SessionId.value sessionId) n
                   ProviderAttempt = sprintf "test-attempt-%d" n |}

        match journalPort.AppendFact (StreamId.Session sessionId) fact with
        | Ok updated -> Ok(updated, DurableFallback.nextDecision sessionId updated)
        | Error err -> Error err

module DurableFallbackTests =

    [<Fact>]
    let ``currentState_on_empty_projection_returns_initial_state`` () =
        let sid = SessionId.create "s-empty"
        let proj = Fold.empty
        let state = DurableFallback.currentState sid proj
        Assert.equal (0uy, state.Offset)

        let decision = DurableFallback.nextDecision sid proj
        Assert.equal (FallbackDecision.NextAttempt { Offset = 0uy }, decision)
        Assert.False(DurableFallback.isDead sid proj)

    [<Fact>]
    let ``recordFailure_cycles_modulo_4_never_dead`` () =
        withTempDir (fun tempDir ->
            task {
                let runtimeId = RuntimeId.create "rt-df-1"
                let sid = SessionId.create "s-fb-1"
                use journal = AgentJournal.create tempDir runtimeId 100 DateTimeOffset.UtcNow
                let port = FallbackJournalPort.fromAgentJournal journal

                let expectedAfter1to4 =
                    [ (1uy, ModelSide.A)
                      (2uy, ModelSide.B)
                      (3uy, ModelSide.B)
                      (0uy, ModelSide.A) ]

                for i in 1..12 do
                    match DurableFallbackTestSupport.recordFailure port sid (sprintf "err-%d" i) with
                    | Ok(proj, decision) ->
                        let state = DurableFallback.currentState sid proj
                        let expectedOffset = byte (i % 4)

                        Assert.equal (expectedOffset, state.Offset)
                        Assert.equal (Fallback.currentSide state, DurableFallback.currentSide sid proj)
                        Assert.equal (FallbackDecision.NextAttempt { Offset = expectedOffset }, decision)
                        Assert.False(DurableFallback.isDead sid proj)

                        if i <= 4 then
                            let expOff, expSide = expectedAfter1to4.[i - 1]
                            Assert.equal (expOff, state.Offset)
                            Assert.equal (expSide, DurableFallback.currentSide sid proj)

                        if i = 4 || i = 8 || i = 12 then
                            Assert.equal (0uy, state.Offset)
                            Assert.equal (ModelSide.A, DurableFallback.currentSide sid proj)
                            Assert.equal (FallbackDecision.NextAttempt { Offset = 0uy }, decision)
                            Assert.False(DurableFallback.isDead sid proj)
                    | Error err -> Assert.True(false, sprintf "Expected Ok, got %s" err)
            })

    [<Fact>]
    let ``append_before_return_and_boot_fold_proves_durable_cumulative_behavior`` () =
        withTempDir (fun tempDir ->
            task {
                let runtimeId = RuntimeId.create "rt-df-2"
                let sid = SessionId.create "s-fb-durable"
                let journal1 = AgentJournal.create tempDir runtimeId 200 DateTimeOffset.UtcNow
                let port1 = FallbackJournalPort.fromAgentJournal journal1

                // Append 3 failures in first process run → Offset 3 / B
                let! _ = Task.FromResult(DurableFallbackTestSupport.recordFailure port1 sid "Err 1")
                let! _ = Task.FromResult(DurableFallbackTestSupport.recordFailure port1 sid "Err 2")
                let! _ = Task.FromResult(DurableFallbackTestSupport.recordFailure port1 sid "Err 3")

                // Dispose journal (simulating process exit)
                (journal1 :> IDisposable).Dispose()

                // Reopen from disk using Boot
                let bootSnap = Boot.boot tempDir
                let bootedProj = Fold.apply Fold.empty bootSnap.Envelopes

                let bootedState = DurableFallback.currentState sid bootedProj
                Assert.equal (3uy, bootedState.Offset)
                Assert.equal (ModelSide.B, DurableFallback.currentSide sid bootedProj)

                let bootedDecision = DurableFallback.nextDecision sid bootedProj
                Assert.equal (FallbackDecision.NextAttempt { Offset = 3uy }, bootedDecision)

                use journal2 =
                    AgentJournal.createFromBoot
                        tempDir
                        (RuntimeId.create "rt-df-2-reopen")
                        201
                        DateTimeOffset.UtcNow
                        bootSnap

                let port2 = FallbackJournalPort.fromAgentJournal journal2

                match DurableFallbackTestSupport.recordFailure port2 sid "Err 4" with
                | Ok(proj4, _) -> Assert.equal (0uy, (DurableFallback.currentState sid proj4).Offset)
                | Error err -> Assert.True(false, sprintf "Expected Ok, got %s" err)
            })

    [<Fact>]
    let ``after_four_failures_fifth_unique_attempt_still_appends_and_advances`` () =
        withTempDir (fun tempDir ->
            task {
                let sid = SessionId.create "s-fb-continue"

                use journal =
                    AgentJournal.create tempDir (RuntimeId.create "rt-continue-1") 401 DateTimeOffset.UtcNow

                let port = FallbackJournalPort.fromAgentJournal journal

                for i in 1..4 do
                    match DurableFallbackTestSupport.recordFailure port sid (sprintf "err-%d" i) with
                    | Ok _ -> ()
                    | Error err -> Assert.True(false, sprintf "Expected Ok, got %s" err)

                let beforeProj = AgentJournal.snapshot journal
                Assert.equal (0uy, (DurableFallback.currentState sid beforeProj).Offset)
                Assert.False(DurableFallback.isDead sid beforeProj)

                let beforeCount = (Boot.boot tempDir).Envelopes.Length

                match DurableFallbackTestSupport.recordFailure port sid "err-5" with
                | Ok(proj5, _) ->
                    let afterCount = (Boot.boot tempDir).Envelopes.Length
                    Assert.True(afterCount > beforeCount, "5th unique attempt should append")
                    Assert.equal (1uy, (DurableFallback.currentState sid proj5).Offset)
                | Error err -> Assert.True(false, sprintf "Expected Ok, got %s" err)
            })

    [<Fact>]
    let ``success_does_not_clear_facts`` () =
        withTempDir (fun tempDir ->
            task {
                let runtimeId = RuntimeId.create "rt-df-3"
                let sid = SessionId.create "s-fb-success"
                use journal = AgentJournal.create tempDir runtimeId 300 DateTimeOffset.UtcNow
                let port = FallbackJournalPort.fromAgentJournal journal

                let! _ = Task.FromResult(DurableFallbackTestSupport.recordFailure port sid "Transient Error")
                let snap1 = AgentJournal.snapshot journal

                let state1 = DurableFallback.currentState sid snap1
                Assert.equal (1uy, state1.Offset)

                // No facts are cleared on success; querying snapshot returns identical durable facts
                let snap2 = AgentJournal.snapshot journal
                let state2 = DurableFallback.currentState sid snap2
                Assert.equal (state1, state2)
                Assert.equal (1uy, state2.Offset)
            })
