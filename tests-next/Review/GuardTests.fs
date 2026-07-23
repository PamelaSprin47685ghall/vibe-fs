namespace Wanxiangshu.Next.Tests.ReviewTests

open System
open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Review
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport

module GuardTests =

    [<Fact>]
    let ``Pure fold: revise resets consecutive perfects`` () =
        let sid = SessionId.create "mgr-1"
        let revSid = SessionId.create "rev-1"
        let treeHash = "tree100"

        let fact1 =
            AgentFact.ReviewVerdictRecorded
                {| ManagerSessionId = sid
                   ReviewerSessionId = revSid
                   ToolCallId = "tc1"
                   GitTreeHash = treeHash
                   Verdict = ReviewGuardVerdict.Perfect |}

        let fact2 =
            AgentFact.ReviewVerdictRecorded
                {| ManagerSessionId = sid
                   ReviewerSessionId = revSid
                   ToolCallId = "tc2"
                   GitTreeHash = treeHash
                   Verdict = ReviewGuardVerdict.Revise |}

        let proj1 = AgentFacts.foldAgentFact AgentFacts.empty fact1
        let rg1 = proj1.Sessions.[sid].ReviewGuard.Value
        Assert.Equal(1, rg1.ConsecutivePerfects)
        Assert.False(rg1.IsConfirmed)

        let proj2 = AgentFacts.foldAgentFact proj1 fact2
        let rg2 = proj2.Sessions.[sid].ReviewGuard.Value
        Assert.Equal(0, rg2.ConsecutivePerfects)
        Assert.False(rg2.IsConfirmed)

    [<Fact>]
    let ``Pure fold: first and second perfect on same tree`` () =
        let sid = SessionId.create "mgr-2"
        let revSid = SessionId.create "rev-1"
        let treeHash = "tree200"

        let fact1 =
            AgentFact.ReviewVerdictRecorded
                {| ManagerSessionId = sid
                   ReviewerSessionId = revSid
                   ToolCallId = "tc1"
                   GitTreeHash = treeHash
                   Verdict = ReviewGuardVerdict.Perfect |}

        let fact2 =
            AgentFact.ReviewVerdictRecorded
                {| ManagerSessionId = sid
                   ReviewerSessionId = revSid
                   ToolCallId = "tc2"
                   GitTreeHash = treeHash
                   Verdict = ReviewGuardVerdict.Perfect |}

        let proj1 = AgentFacts.foldAgentFact AgentFacts.empty fact1
        let rg1 = proj1.Sessions.[sid].ReviewGuard.Value
        Assert.Equal(1, rg1.ConsecutivePerfects)
        Assert.False(rg1.IsConfirmed)

        let proj2 = AgentFacts.foldAgentFact proj1 fact2
        let rg2 = proj2.Sessions.[sid].ReviewGuard.Value
        Assert.Equal(2, rg2.ConsecutivePerfects)
        Assert.True(rg2.IsConfirmed)

    [<Fact>]
    let ``Pure fold: tree change resets consecutive perfects count`` () =
        let sid = SessionId.create "mgr-3"
        let revSid = SessionId.create "rev-1"

        let fact1 =
            AgentFact.ReviewVerdictRecorded
                {| ManagerSessionId = sid
                   ReviewerSessionId = revSid
                   ToolCallId = "tc1"
                   GitTreeHash = "treeA"
                   Verdict = ReviewGuardVerdict.Perfect |}

        let fact2 =
            AgentFact.ReviewVerdictRecorded
                {| ManagerSessionId = sid
                   ReviewerSessionId = revSid
                   ToolCallId = "tc2"
                   GitTreeHash = "treeA"
                   Verdict = ReviewGuardVerdict.Perfect |}

        let fact3 =
            AgentFact.ReviewVerdictRecorded
                {| ManagerSessionId = sid
                   ReviewerSessionId = revSid
                   ToolCallId = "tc3"
                   GitTreeHash = "treeB"
                   Verdict = ReviewGuardVerdict.Perfect |}

        let proj2 =
            AgentFacts.foldAgentFact (AgentFacts.foldAgentFact AgentFacts.empty fact1) fact2

        Assert.True(proj2.Sessions.[sid].ReviewGuard.Value.IsConfirmed)

        let proj3 = AgentFacts.foldAgentFact proj2 fact3
        let rg3 = proj3.Sessions.[sid].ReviewGuard.Value
        Assert.Equal(1, rg3.ConsecutivePerfects)
        Assert.False(rg3.IsConfirmed)
        Assert.Equal(Some(GitTreeHash.create "treeB"), rg3.LastGitTreeHash)

    [<Fact>]
    let ``Duplicate guard key handled idempotently (at-most-once)`` () =
        let sid = SessionId.create "mgr-4"
        let mutable hostCalls = 0

        let hostPort: HostPort =
            { SendGuardPrompt =
                fun _ key _ ->
                    hostCalls <- hostCalls + 1
                    Ok("msg-" + string hostCalls) }

        let mutable currentProj =
            { AgentProjections = AgentFacts.empty
              RuntimeId = None }

        let journalPort: JournalPort =
            { AppendFact =
                fun _ fact ->
                    currentProj <-
                        { currentProj with
                            AgentProjections = AgentFacts.foldAgentFact currentProj.AgentProjections fact }

                    Ok currentProj }

        let key = "prompt-guard-key-1"

        // First call: sends prompt to host port and appends fact
        let res1 =
            Guard.guardMissingVerdict hostPort journalPort sid key "Please review" currentProj

        Assert.True(Result.isOk res1)
        let (proj1, msgId1) = res1 |> Result.defaultWith (fun _ -> failwith "unexpected")
        Assert.Equal(1, hostCalls)
        Assert.Equal(Some "msg-1", msgId1)
        Assert.True(Set.contains key proj1.AgentProjections.Sessions.[sid].ReviewGuard.Value.AcceptedGuardKeys)

        // Second call with same key: skips host port call (idempotent)
        let res2 =
            Guard.guardMissingVerdict hostPort journalPort sid key "Please review" proj1

        Assert.True(Result.isOk res2)
        let (_, msgId2) = res2 |> Result.defaultWith (fun _ -> failwith "unexpected")
        Assert.Equal(1, hostCalls) // Host call count did not increase!
        Assert.Equal(None, msgId2)

    [<Fact>]
    let ``Durable port test: append-before-return and double perfect finish allowance`` () =
        withTempDir (fun tempDir ->
            task {
                let runtimeId = RuntimeId.create "rt-guard-durable"
                let sid = SessionId.create "mgr-durable"
                let revSid = SessionId.create "rev-durable"
                let treeHash = "tree-durable-100"
                let now = DateTimeOffset.UtcNow

                use journal = AgentJournal.create tempDir runtimeId 1001 now
                let journalPort = JournalPort.fromAgentJournal journal

                let mutable currentTreeHash = treeHash
                let gitPort: GitPort = { GetTreeHash = fun () -> currentTreeHash }

                // Initial finish check -> NeedsReview
                let initialFinish = Guard.tryFinish gitPort sid (AgentJournal.snapshot journal)
                Assert.Equal(ReviewFinishResult.NeedsReview, initialFinish)

                // Record 1st verdict -> appends to journal and returns projection immediately
                let res1 =
                    Guard.recordVerdict journalPort sid revSid "tc-1" treeHash ReviewGuardVerdict.Perfect

                Assert.True(Result.isOk res1)
                let proj1 = Result.defaultWith (fun _ -> failwith "unexpected") res1

                // Prove append happened before return: snapshot matches returned projection
                Assert.Equal(proj1, AgentJournal.snapshot journal)
                Assert.Equal(ReviewFinishResult.NeedsReview, Guard.tryFinish gitPort sid proj1)

                // Record 2nd verdict on same tree -> confirmed
                let res2 =
                    Guard.recordVerdict journalPort sid revSid "tc-2" treeHash ReviewGuardVerdict.Perfect

                Assert.True(Result.isOk res2)
                let proj2 = Result.defaultWith (fun _ -> failwith "unexpected") res2
                Assert.Equal(proj2, AgentJournal.snapshot journal)

                // Double perfect on same tree -> Confirmed!
                Assert.Equal(ReviewFinishResult.Confirmed, Guard.tryFinish gitPort sid proj2)

                // Git tree hash changes -> tryFinish returns NeedsReview without creating any fact
                currentTreeHash <- "tree-durable-200"
                Assert.Equal(ReviewFinishResult.NeedsReview, Guard.tryFinish gitPort sid proj2)
            })
