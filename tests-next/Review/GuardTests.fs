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
                   UserPromptText = None
                   UserMessageId = None
                   ToolCallId = "tc1"
                   GitTreeHash = treeHash
                   Verdict = ReviewGuardVerdict.Perfect |}

        let fact2 =
            AgentFact.ReviewVerdictRecorded
                {| ManagerSessionId = sid
                   ReviewerSessionId = revSid
                   ProviderRunId = "pr-2"
                   UserPromptText = None
                   UserMessageId = None
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
                   UserPromptText = None
                   UserMessageId = None
                   ToolCallId = "tc1"
                   GitTreeHash = treeHash
                   Verdict = ReviewGuardVerdict.Perfect |}

        // chat.message accepts the ReviewConfirmation before the async send
        // callback persists GuardPromptAccepted. The accepted continuation is
        // already sufficient causal proof for the second PERFECT.
        let confirmClaim =
            AgentFact.PluginPromptClaimed
                {| PromptKey = "confirm-key"
                   SessionId = revSid
                   LogicalRunId = "review-run"
                   AuthorityRootUserMessageId = "review-root"
                   ContinuationKind = "ReviewConfirmation"
                   EffectiveAgent = Some "fast-reviewer" |}

        let confirmAccepted =
            AgentFact.PluginPromptAccepted
                {| PromptKey = "confirm-key"
                   SessionId = revSid
                   HostMessageId = "confirm-200" |}

        let fact2 =
            AgentFact.ReviewVerdictRecorded
                {| ManagerSessionId = sid
                   ReviewerSessionId = revSid
                   ProviderRunId = "pr-2"
                   UserPromptText = None
                   UserMessageId = Some "confirm-200"
                   ToolCallId = "tc2"
                   GitTreeHash = treeHash
                   Verdict = ReviewGuardVerdict.Perfect |}

        let proj1 = AgentFacts.foldAgentFact AgentFacts.empty fact1
        let rg1 = proj1.Sessions.[sid].ReviewGuard.Value
        Assert.Equal(1, rg1.ConsecutivePerfects)
        Assert.False(rg1.IsConfirmed)

        let projPrompt =
            AgentFacts.foldAgentFact (AgentFacts.foldAgentFact proj1 confirmClaim) confirmAccepted

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
                   UserPromptText = None
                   UserMessageId = None
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
                   UserPromptText = None
                   UserMessageId = None
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
                   UserPromptText = Some "unrelated-user-message"
                   UserMessageId = None
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
                   UserPromptText = None
                   UserMessageId = None
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
                   UserPromptText =
                    Some
                        "Nope, let's re-evaluate: does it really fully satisfy the original task without cutting corners?"
                   UserMessageId = Some "confirm-a"
                   ToolCallId = "tc2"
                   GitTreeHash = "treeA"
                   Verdict = ReviewGuardVerdict.Perfect |}

        let fact3 =
            AgentFact.ReviewVerdictRecorded
                {| ManagerSessionId = sid
                   ReviewerSessionId = revSid
                   ProviderRunId = "pr-3"
                   UserPromptText = None
                   UserMessageId = None
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
