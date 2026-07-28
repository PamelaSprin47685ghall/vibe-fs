namespace Wanxiangshu.Next.Tests.SessionTests

open System
open Xunit
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport

module ReviewerHostTests =

    [<Fact>]
    let ``ReviewerHost_deduplicates_verdict_and_confirms_two_distinct_perfects`` () =
        withTempDir (fun directory ->
            task {
                let manager = SessionId.create "review-manager"
                let reviewer = SessionId.create "reviewer"
                let now = DateTimeOffset.UtcNow

                use journal =
                    AgentJournal.createFromBoot
                        directory
                        (RuntimeId.create "review-runtime")
                        1
                        now
                        (Boot.boot directory)

                let host = ReviewerHost(journal, manager, reviewer)

                let expect expected result =
                    match result with
                    | Ok actual -> Assert.Equal(expected, actual)
                    | Error error -> Assert.True(false, sprintf "Unexpected journal error: %s" error)

                expect
                    ReviewFinishResult.NeedsReview
                    (host.RecordVerdict("call-1", "tree-a", ReviewGuardVerdict.Perfect, "run-1"))

                expect
                    ReviewFinishResult.NeedsReview
                    (host.RecordVerdict("call-1", "tree-a", ReviewGuardVerdict.Perfect, "run-1"))

                // ReviewGuard's confirm-perfect nudge after the first PERFECT installs the
                // content marker; without the marker text the second PERFECT must
                // not confirm (fail-closed, KISS-N07).
                let confirmFact =
                    AgentFact.GuardPromptAccepted
                        {| TargetSessionId = manager
                           GuardKey = "confirm-perfect:tree-a"
                           HostMessageId = "confirm-a" |}

                let _ = AgentJournal.appendAgent (StreamId.Session manager) None confirmFact journal

                expect
                    ReviewFinishResult.Confirmed
                    (host.RecordVerdict(
                        "call-2",
                        "tree-a",
                        ReviewGuardVerdict.Perfect,
                        "run-2",
                        "PERFECT requires confirmation. Re-read the current tree and call verdict(PERFECT) again to confirm."
                    ))

                expect
                    ReviewFinishResult.NeedsReview
                    (host.RecordVerdict("call-3", "tree-b", ReviewGuardVerdict.Perfect, "run-3"))

                expect
                    ReviewFinishResult.NeedsReview
                    (host.RecordVerdict("call-4", "tree-b", ReviewGuardVerdict.Revise, "run-4"))

                // A replay of an earlier tool call must remain a no-op even after newer verdicts.
                expect
                    ReviewFinishResult.NeedsReview
                    (host.RecordVerdict("call-1", "tree-b", ReviewGuardVerdict.Perfect, "run-5"))
            })

    [<Fact>]
    let ``SubmitVerdict_reads_tree_from_port_and_appends_to_journal`` () =
        withTempDir (fun directory ->
            task {
                let manager = SessionId.create "review-manager-port"
                let reviewer = SessionId.create "reviewer-port"
                let now = DateTimeOffset.UtcNow
                let mutable treeHash = "tree-a"

                use journal =
                    AgentJournal.createFromBoot
                        directory
                        (RuntimeId.create "review-runtime-port")
                        1
                        now
                        (Boot.boot directory)

                let gitTreePort = { GetTreeHash = fun () -> treeHash }
                let host = ReviewerHost(journal, manager, reviewer, gitTreePort)

                Assert.Equal(
                    Ok ReviewFinishResult.NeedsReview,
                    host.SubmitVerdict("call-1", ReviewGuardVerdict.Perfect, providerRunId = "run-1")
                )

                let firstProjection = AgentJournal.snapshot journal

                let firstGuard =
                    firstProjection.AgentProjections.Sessions.[manager].ReviewGuard.Value

                Assert.Equal(Some(GitTreeHash.create "tree-a"), firstGuard.LastGitTreeHash)
                Assert.Equal([ "call-1" ], firstGuard.RecentToolCallIds)

                Assert.Equal(
                    Ok ReviewFinishResult.NeedsReview,
                    host.SubmitVerdict("call-1", ReviewGuardVerdict.Perfect, providerRunId = "run-1")
                )

                treeHash <- "tree-b"

                Assert.Equal(
                    Ok ReviewFinishResult.NeedsReview,
                    host.SubmitVerdict("call-1", ReviewGuardVerdict.Perfect, providerRunId = "run-1")
                )

                let duplicateProjection = AgentJournal.snapshot journal

                let duplicateGuard =
                    duplicateProjection.AgentProjections.Sessions.[manager].ReviewGuard.Value

                Assert.Equal(Some(GitTreeHash.create "tree-a"), duplicateGuard.LastGitTreeHash)
                Assert.Equal([ "call-1" ], duplicateGuard.RecentToolCallIds)

                treeHash <- "tree-a"

                // ReviewGuard's confirm-perfect nudge is the only source of a valid
                // confirmation message id; without it, "call-2" would not confirm.
                let confirmFact =
                    AgentFact.GuardPromptAccepted
                        {| TargetSessionId = manager
                           GuardKey = "confirm-perfect:tree-a"
                           HostMessageId = "confirm-a" |}

                let _ = AgentJournal.appendAgent (StreamId.Session manager) None confirmFact journal

                Assert.Equal(
                    Ok ReviewFinishResult.Confirmed,
                    host.SubmitVerdict(
                        "call-2",
                        ReviewGuardVerdict.Perfect,
                        providerRunId = "run-2",
                        userPromptText = "PERFECT requires confirmation. Re-read the current tree and call verdict(PERFECT) again to confirm."
                    )
                )

                Assert.Equal(ReviewFinishResult.Confirmed, host.TryFinish())

                treeHash <- "tree-b"
                Assert.Equal(ReviewFinishResult.NeedsReview, host.TryFinish())
            })
