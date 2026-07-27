namespace Wanxiangshu.Next.Tests.JournalTests

open System
open Xunit
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal

module AgentFactsOrchestratorBarrierTests =

    let private createSessionEnv seq dt agentFact rt sid =
        { RuntimeId = rt
          LocalSeq = LocalSeq.create seq
          ObservedAt = dt
          EventId = EventId.create ("evt-" + string seq)
          Stream = StreamId.Session sid
          TurnId = None
          Fact = Fact.Agent agentFact }

    [<Fact>]
    let Review_barrier_started_resets_confirmation_and_requires_fresh_verdicts () =
        let rt = RuntimeId.create "rt-barrier-review"
        let sid = SessionId.create "session-barrier-review"
        let treeHash = "tree-same-hash"
        let t0 = DateTimeOffset.UtcNow

        let fact1 =
            AgentFact.ReviewVerdictRecorded
                {| ManagerSessionId = sid
                   ReviewerSessionId = sid
                   ProviderRunId = "pr-1"
                   RootUserMessageId = None
                   ToolCallId = "call-1"
                   GitTreeHash = treeHash
                   Verdict = ReviewGuardVerdict.Perfect |}

        let confirmFact1 =
            AgentFact.GuardPromptAccepted
                {| TargetSessionId = sid
                   GuardKey = "guard-key-1"
                   HostMessageId = "msg-confirm-1" |}

        let fact2 =
            AgentFact.ReviewVerdictRecorded
                {| ManagerSessionId = sid
                   ReviewerSessionId = sid
                   ProviderRunId = "pr-2"
                   RootUserMessageId = Some "msg-confirm-1"
                   ToolCallId = "call-2"
                   GitTreeHash = treeHash
                   Verdict = ReviewGuardVerdict.Perfect |}

        let barrierFact =
            AgentFact.ReviewBarrierStarted
                {| ManagerSessionId = sid
                   BarrierKey = "post-rebase" |}

        let fact3 =
            AgentFact.ReviewVerdictRecorded
                {| ManagerSessionId = sid
                   ReviewerSessionId = sid
                   ProviderRunId = "pr-3"
                   RootUserMessageId = None
                   ToolCallId = "call-3"
                   GitTreeHash = treeHash
                   Verdict = ReviewGuardVerdict.Perfect |}

        let confirmFact2 =
            AgentFact.GuardPromptAccepted
                {| TargetSessionId = sid
                   GuardKey = "guard-key-2"
                   HostMessageId = "msg-confirm-2" |}

        let fact4 =
            AgentFact.ReviewVerdictRecorded
                {| ManagerSessionId = sid
                   ReviewerSessionId = sid
                   ProviderRunId = "pr-4"
                   RootUserMessageId = Some "msg-confirm-2"
                   ToolCallId = "call-4"
                   GitTreeHash = treeHash
                   Verdict = ReviewGuardVerdict.Perfect |}

        let envs =
            [ fact1; confirmFact1; fact2; barrierFact; fact3; confirmFact2; fact4 ]
            |> List.mapi (fun i f -> createSessionEnv (int64 (i + 1)) (t0.AddSeconds(float i)) f rt sid)

        let proj = AgentFacts.apply AgentFacts.empty envs
        let rg = proj.Sessions.[sid].ReviewGuard.Value

        Assert.True(rg.IsConfirmed)
        Assert.Equal(2, rg.ConsecutivePerfects)
        Assert.Equal(Some "post-rebase", rg.CurrentBarrierKey)
        Assert.Equal(Some(GitTreeHash.create treeHash), rg.LastGitTreeHash)

    [<Fact>]
    let Review_barrier_started_alone_leaves_guard_unconfirmed () =
        let rt = RuntimeId.create "rt-barrier-alone"
        let sid = SessionId.create "session-barrier-alone"
        let treeHash = "tree-hash-x"
        let t0 = DateTimeOffset.UtcNow

        let fact1 =
            AgentFact.ReviewVerdictRecorded
                {| ManagerSessionId = sid
                   ReviewerSessionId = sid
                   ProviderRunId = "pr-1"
                   RootUserMessageId = None
                   ToolCallId = "call-1"
                   GitTreeHash = treeHash
                   Verdict = ReviewGuardVerdict.Perfect |}

        let fact2 =
            AgentFact.ReviewVerdictRecorded
                {| ManagerSessionId = sid
                   ReviewerSessionId = sid
                   ProviderRunId = "pr-2"
                   RootUserMessageId = None
                   ToolCallId = "call-2"
                   GitTreeHash = treeHash
                   Verdict = ReviewGuardVerdict.Perfect |}

        let barrierFact =
            AgentFact.ReviewBarrierStarted
                {| ManagerSessionId = sid
                   BarrierKey = "pre-rebase" |}

        let envs =
            [ fact1; fact2; barrierFact ]
            |> List.mapi (fun i f -> createSessionEnv (int64 (i + 1)) (t0.AddSeconds(float i)) f rt sid)

        let proj = AgentFacts.apply AgentFacts.empty envs
        let rg = proj.Sessions.[sid].ReviewGuard.Value

        Assert.False(rg.IsConfirmed)
        Assert.Equal(0, rg.ConsecutivePerfects)
        Assert.Equal(Some "pre-rebase", rg.CurrentBarrierKey)
