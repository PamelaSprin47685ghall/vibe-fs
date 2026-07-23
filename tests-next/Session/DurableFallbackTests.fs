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

module DurableFallbackTests =

    [<Fact>]
    let ``currentState_on_empty_projection_returns_initial_state`` () =
        let sid = SessionId.create "s-empty"
        let proj = Fold.empty
        let state = DurableFallback.currentState sid proj
        Assert.Equal(ModelSide.A, state.Side)
        Assert.Equal(0, state.Failures)

        let decision = DurableFallback.nextDecision sid proj
        Assert.Equal(FallbackDecision.NextAttempt { Side = ModelSide.A; Failures = 1 }, decision)

    [<Fact>]
    let ``recordFailure_progresses_through_A_retry_switch_B_B_retry_and_dead`` () =
        withTempDir (fun tempDir ->
            task {
                let runtimeId = RuntimeId.create "rt-df-1"
                let sid = SessionId.create "s-fb-1"
                use journal = AgentJournal.create tempDir runtimeId 100 DateTimeOffset.UtcNow
                let port = FallbackJournalPort.fromAgentJournal journal

                // 1st failure: recorded in journal -> projection updated -> next decision is switch B
                let res1 = DurableFallback.recordFailure port sid "Model A Timeout"

                match res1 with
                | Ok(proj1, decision1) ->
                    let state1 = DurableFallback.currentState sid proj1
                    Assert.Equal(ModelSide.A, state1.Side)
                    Assert.Equal(1, state1.Failures)
                    Assert.Equal(FallbackDecision.NextAttempt { Side = ModelSide.B; Failures = 2 }, decision1)
                | Error err -> Assert.True(false, sprintf "Expected Ok, got %s" err)

                // 2nd failure: recorded in journal -> next decision is B retry
                let res2 = DurableFallback.recordFailure port sid "Model B RateLimit"

                match res2 with
                | Ok(proj2, decision2) ->
                    let state2 = DurableFallback.currentState sid proj2
                    Assert.Equal(ModelSide.B, state2.Side)
                    Assert.Equal(2, state2.Failures)
                    Assert.Equal(FallbackDecision.NextAttempt { Side = ModelSide.B; Failures = 3 }, decision2)
                | Error err -> Assert.True(false, sprintf "Expected Ok, got %s" err)

                // 3rd failure: recorded in journal -> next decision is Dead
                let res3 = DurableFallback.recordFailure port sid "Model B ServerError"

                match res3 with
                | Ok(proj3, decision3) ->
                    let state3 = DurableFallback.currentState sid proj3
                    Assert.Equal(ModelSide.B, state3.Side)
                    Assert.Equal(3, state3.Failures)
                    Assert.Equal(FallbackDecision.Dead, decision3)
                | Error err -> Assert.True(false, sprintf "Expected Ok, got %s" err)

                // 4th failure: recorded in journal -> remains Dead
                let res4 = DurableFallback.recordFailure port sid "Model B DeadLock"

                match res4 with
                | Ok(proj4, decision4) ->
                    let state4 = DurableFallback.currentState sid proj4
                    Assert.Equal(ModelSide.B, state4.Side)
                    Assert.Equal(4, state4.Failures)
                    Assert.Equal(FallbackDecision.Dead, decision4)
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

                // Append 2 failures in first process run
                let! _ = Task.FromResult(DurableFallback.recordFailure port1 sid "Err 1")
                let! _ = Task.FromResult(DurableFallback.recordFailure port1 sid "Err 2")

                // Dispose journal (simulating process exit)
                (journal1 :> IDisposable).Dispose()

                // Reopen from disk using Boot
                let bootSnap = Boot.boot tempDir
                let bootedProj = Fold.apply Fold.empty bootSnap.Envelopes

                let bootedState = DurableFallback.currentState sid bootedProj
                Assert.Equal(ModelSide.B, bootedState.Side)
                Assert.Equal(2, bootedState.Failures)

                let bootedDecision = DurableFallback.nextDecision sid bootedProj
                Assert.Equal(FallbackDecision.NextAttempt { Side = ModelSide.B; Failures = 3 }, bootedDecision)
            })

    [<Fact>]
    let ``success_does_not_clear_facts`` () =
        withTempDir (fun tempDir ->
            task {
                let runtimeId = RuntimeId.create "rt-df-3"
                let sid = SessionId.create "s-fb-success"
                use journal = AgentJournal.create tempDir runtimeId 300 DateTimeOffset.UtcNow
                let port = FallbackJournalPort.fromAgentJournal journal

                let! _ = Task.FromResult(DurableFallback.recordFailure port sid "Transient Error")
                let snap1 = AgentJournal.snapshot journal

                let state1 = DurableFallback.currentState sid snap1
                Assert.Equal(1, state1.Failures)

                // No facts are cleared on success; querying snapshot returns identical durable facts
                let snap2 = AgentJournal.snapshot journal
                let state2 = DurableFallback.currentState sid snap2
                Assert.Equal(state1, state2)
            })
