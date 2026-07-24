namespace Wanxiangshu.Next.Tests.JournalTests

open System
open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel.Outcome
open JournalTestSupport

module AgentJournalTests =

    [<Fact>]
    let RuntimeStarted_and_AgentFact_visible_immediately_and_survive_reopen () =
        withTempDir (fun tempDir ->
            task {
                let runtimeId = RuntimeId.create "rt-agent-journal-1"
                let processId = 1234
                let now = DateTimeOffset.UtcNow
                let sid = SessionId.create "s1"

                use journal = AgentJournal.create tempDir runtimeId processId now

                Assert.Equal(runtimeId, AgentJournal.runtimeId journal)
                Assert.False(AgentJournal.isPoisoned journal)

                let initSnap = AgentJournal.snapshot journal
                Assert.Equal(Some runtimeId, initSnap.RuntimeId)
                Assert.True(initSnap.AgentProjections.Sessions.IsEmpty)

                let fact =
                    AgentFact.FallbackFailureRecorded
                        {| SessionId = sid
                           Reason = "Timeout" |}

                let result = AgentJournal.appendAgent StreamId.Workspace None fact journal

                match result with
                | Ok updatedProj -> Assert.True(updatedProj.AgentProjections.Sessions.ContainsKey sid)
                | Error failure -> Assert.True(false, sprintf "Expected Ok, got Error: %A" failure)

                let currentSnap = AgentJournal.snapshot journal
                Assert.True(currentSnap.AgentProjections.Sessions.ContainsKey sid)

                (journal :> IDisposable).Dispose()

                let bootSnap = Boot.boot tempDir
                Assert.Equal(2, bootSnap.Envelopes.Length)

                let reopenedProj = Fold.apply Fold.empty bootSnap.Envelopes
                Assert.Equal(Some runtimeId, reopenedProj.RuntimeId)
                Assert.True(reopenedProj.AgentProjections.Sessions.ContainsKey sid)
            })

    [<Fact>]
    let Failed_append_does_not_update_projection () =
        withTempDir (fun tempDir ->
            task {
                let runtimeId = RuntimeId.create "rt-agent-journal-2"
                let processId = 5678
                let now = DateTimeOffset.UtcNow
                let sid = SessionId.create "s2"

                let journal = AgentJournal.create tempDir runtimeId processId now
                let baselineSnap = AgentJournal.snapshot journal

                Assert.False(AgentJournal.isPoisoned journal)

                (journal :> IDisposable).Dispose()

                let fact = AgentFact.FallbackFailureRecorded {| SessionId = sid; Reason = "Err" |}

                let result = AgentJournal.appendAgent StreamId.Workspace None fact journal

                match result with
                | Error failure ->
                    Assert.False(String.IsNullOrWhiteSpace(EventId.value failure.EventId))

                    match failure.Failure with
                    | WriteFailed _ -> ()
                    | _ -> Assert.True(false, sprintf "Expected WriteFailed, got %A" failure.Failure)
                | Ok _ -> Assert.True(false, "Expected Error result on disposed writer, got Ok")

                let snapAfterFail = AgentJournal.snapshot journal
                Assert.Equal(baselineSnap, snapAfterFail)
                Assert.False(snapAfterFail.AgentProjections.Sessions.ContainsKey sid)
            })

    [<Fact>]
    let Durable_fact_payloads_fold_into_projection () =
        withTempDir (fun tempDir ->
            task {
                let runtimeId = RuntimeId.create "rt-agent-journal-3"
                let now = DateTimeOffset.UtcNow
                let sid = SessionId.create "s3"
                let mgrId = "mgr-1"
                let candId = "cand-1"

                use journal = AgentJournal.create tempDir runtimeId 9999 now

                let reviewFact =
                    AgentFact.ReviewVerdictRecorded
                        {| ManagerSessionId = sid
                           ReviewerSessionId = sid
                           ToolCallId = "call-123"
                           GitTreeHash = "hash123"
                           Verdict = ReviewGuardVerdict.Perfect |}

                let res1 = AgentJournal.appendAgent (StreamId.Session sid) None reviewFact journal
                Assert.True(Result.isOk res1)

                let compFact =
                    AgentFact.CompanionBaselineSet
                        {| SessionId = sid
                           Projection = "{\"k\":\"v\"}" |}

                let res2 = AgentJournal.appendAgent (StreamId.Session sid) None compFact journal
                Assert.True(Result.isOk res2)

                let orchFact =
                    AgentFact.OrchestratorPublished
                        {| ManagerId = mgrId
                           CandidateId = candId
                           CommitHash = "commitABC" |}

                let res3 = AgentJournal.appendAgent StreamId.Workspace None orchFact journal
                Assert.True(Result.isOk res3)

                let snap = AgentJournal.snapshot journal
                Assert.True(snap.AgentProjections.Sessions.ContainsKey sid)
                let sessionProj = snap.AgentProjections.Sessions.[sid]
                Assert.True(sessionProj.ReviewGuard.IsSome)
                Assert.True(sessionProj.Companion.IsSome)
                Assert.Equal(Some "{\"k\":\"v\"}", sessionProj.Companion.Value.LastSuccessfulProjection)

                let orchProj = snap.AgentProjections.Orchestrator
                Assert.True(orchProj.Managers.ContainsKey(ManagerId.create mgrId))
            })

    [<Fact>]
    let Boot_history_is_loaded_into_new_journal_snapshot () =
        withTempDir (fun tempDir ->
            task {
                let previousRuntimeId = RuntimeId.create "rt-agent-journal-boot-previous"
                let currentRuntimeId = RuntimeId.create "rt-agent-journal-boot-current"
                let sessionId = SessionId.create "s-boot-history"

                let fact =
                    AgentFact.FallbackFailureRecorded
                        {| SessionId = sessionId
                           Reason = "external startup fact" |}

                use previousJournal =
                    AgentJournal.create tempDir previousRuntimeId 4321 DateTimeOffset.UtcNow

                Assert.True(Result.isOk (AgentJournal.appendAgent StreamId.Workspace None fact previousJournal))
                (previousJournal :> IDisposable).Dispose()

                let boot = Boot.boot tempDir

                use currentJournal =
                    AgentJournal.createFromBoot tempDir currentRuntimeId 4322 DateTimeOffset.UtcNow boot

                let snapshot = AgentJournal.snapshot currentJournal
                Assert.Equal(Some currentRuntimeId, snapshot.RuntimeId)
                Assert.True(snapshot.AgentProjections.Sessions.ContainsKey sessionId)
            })
