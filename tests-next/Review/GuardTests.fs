namespace Wanxiangshu.Next.Tests.ReviewTests

open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal

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
                   ProviderRunId = "pr-1"
                   RootUserMessageId = None
                   ToolCallId = "tc1"
                   GitTreeHash = treeHash
                   Verdict = ReviewGuardVerdict.Perfect |}

        let fact2 =
            AgentFact.ReviewVerdictRecorded
                {| ManagerSessionId = sid
                   ReviewerSessionId = revSid
                   ProviderRunId = "pr-2"
                   RootUserMessageId = None
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
                   ProviderRunId = "pr-1"
                   RootUserMessageId = None
                   ToolCallId = "tc1"
                   GitTreeHash = treeHash
                   Verdict = ReviewGuardVerdict.Perfect |}

        // ReviewGuard sends a confirmation nudge after the first PERFECT; its
        // HostMessageId (GuardPromptAccepted) is the only valid causal proof
        // that a second PERFECT is a real confirmation round-trip, not two
        // independent calls (KISS-N07, fail-closed).
        let confirmPrompt =
            AgentFact.GuardPromptAccepted
                {| TargetSessionId = sid
                   GuardKey = "confirm-perfect:tree200"
                   HostMessageId = "confirm-200" |}

        let fact2 =
            AgentFact.ReviewVerdictRecorded
                {| ManagerSessionId = sid
                   ReviewerSessionId = revSid
                   ProviderRunId = "pr-2"
                   RootUserMessageId = Some "confirm-200"
                   ToolCallId = "tc2"
                   GitTreeHash = treeHash
                   Verdict = ReviewGuardVerdict.Perfect |}

        let proj1 = AgentFacts.foldAgentFact AgentFacts.empty fact1
        let rg1 = proj1.Sessions.[sid].ReviewGuard.Value
        Assert.Equal(1, rg1.ConsecutivePerfects)
        Assert.False(rg1.IsConfirmed)

        let projPrompt = AgentFacts.foldAgentFact proj1 confirmPrompt

        let proj2 = AgentFacts.foldAgentFact projPrompt fact2
        let rg2 = proj2.Sessions.[sid].ReviewGuard.Value
        Assert.Equal(2, rg2.ConsecutivePerfects)
        Assert.True(rg2.IsConfirmed)

    [<Fact>]
    let ``Pure fold: second perfect without confirmation prompt does not confirm`` () =
        let sid = SessionId.create "mgr-2b"
        let revSid = SessionId.create "rev-1"
        let treeHash = "tree201"

        let fact1 =
            AgentFact.ReviewVerdictRecorded
                {| ManagerSessionId = sid
                   ReviewerSessionId = revSid
                   ProviderRunId = "pr-1"
                   RootUserMessageId = None
                   ToolCallId = "tc1"
                   GitTreeHash = treeHash
                   Verdict = ReviewGuardVerdict.Perfect |}

        // No GuardPromptAccepted fact is ever appended here -- simulating a
        // second independent PERFECT call with no confirmation nudge in
        // between. This must NOT confirm (fail-closed: missing confirmation
        // message id is not equivalent to a proven round-trip).
        let fact2 =
            AgentFact.ReviewVerdictRecorded
                {| ManagerSessionId = sid
                   ReviewerSessionId = revSid
                   ProviderRunId = "pr-2"
                   RootUserMessageId = None
                   ToolCallId = "tc2"
                   GitTreeHash = treeHash
                   Verdict = ReviewGuardVerdict.Perfect |}

        let proj1 = AgentFacts.foldAgentFact AgentFacts.empty fact1
        let proj2 = AgentFacts.foldAgentFact proj1 fact2
        let rg2 = proj2.Sessions.[sid].ReviewGuard.Value
        Assert.False(rg2.IsConfirmed)

        // Even an unrelated root user message (not the confirmation prompt)
        // must not confirm.
        let fact3 =
            AgentFact.ReviewVerdictRecorded
                {| ManagerSessionId = sid
                   ReviewerSessionId = revSid
                   ProviderRunId = "pr-3"
                   RootUserMessageId = Some "unrelated-user-message"
                   ToolCallId = "tc3"
                   GitTreeHash = treeHash
                   Verdict = ReviewGuardVerdict.Perfect |}

        let proj3 = AgentFacts.foldAgentFact proj2 fact3
        Assert.False(proj3.Sessions.[sid].ReviewGuard.Value.IsConfirmed)

    [<Fact>]
    let ``Pure fold: tree change resets consecutive perfects count`` () =
        let sid = SessionId.create "mgr-3"
        let revSid = SessionId.create "rev-1"

        let fact1 =
            AgentFact.ReviewVerdictRecorded
                {| ManagerSessionId = sid
                   ReviewerSessionId = revSid
                   ProviderRunId = "pr-1"
                   RootUserMessageId = None
                   ToolCallId = "tc1"
                   GitTreeHash = "treeA"
                   Verdict = ReviewGuardVerdict.Perfect |}

        let confirmPrompt =
            AgentFact.GuardPromptAccepted
                {| TargetSessionId = sid
                   GuardKey = "confirm-perfect:treeA"
                   HostMessageId = "confirm-a" |}

        let fact2 =
            AgentFact.ReviewVerdictRecorded
                {| ManagerSessionId = sid
                   ReviewerSessionId = revSid
                   ProviderRunId = "pr-2"
                   RootUserMessageId = Some "confirm-a"
                   ToolCallId = "tc2"
                   GitTreeHash = "treeA"
                   Verdict = ReviewGuardVerdict.Perfect |}

        let fact3 =
            AgentFact.ReviewVerdictRecorded
                {| ManagerSessionId = sid
                   ReviewerSessionId = revSid
                   ProviderRunId = "pr-3"
                   RootUserMessageId = None
                   ToolCallId = "tc3"
                   GitTreeHash = "treeB"
                   Verdict = ReviewGuardVerdict.Perfect |}

        let proj2 =
            AgentFacts.foldAgentFact
                (AgentFacts.foldAgentFact (AgentFacts.foldAgentFact AgentFacts.empty fact1) confirmPrompt)
                fact2

        Assert.True(proj2.Sessions.[sid].ReviewGuard.Value.IsConfirmed)

        let proj3 = AgentFacts.foldAgentFact proj2 fact3
        let rg3 = proj3.Sessions.[sid].ReviewGuard.Value
        Assert.Equal(1, rg3.ConsecutivePerfects)
        Assert.False(rg3.IsConfirmed)
        Assert.Equal(Some(GitTreeHash.create "treeB"), rg3.LastGitTreeHash)
