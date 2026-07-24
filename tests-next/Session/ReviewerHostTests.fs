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
                    (host.RecordVerdict("call-1", "tree-a", ReviewGuardVerdict.Perfect))

                expect
                    ReviewFinishResult.NeedsReview
                    (host.RecordVerdict("call-1", "tree-a", ReviewGuardVerdict.Perfect))

                expect
                    ReviewFinishResult.Confirmed
                    (host.RecordVerdict("call-2", "tree-a", ReviewGuardVerdict.Perfect))

                expect
                    ReviewFinishResult.NeedsReview
                    (host.RecordVerdict("call-3", "tree-b", ReviewGuardVerdict.Perfect))

                expect
                    ReviewFinishResult.NeedsReview
                    (host.RecordVerdict("call-4", "tree-b", ReviewGuardVerdict.Revise))
            })
