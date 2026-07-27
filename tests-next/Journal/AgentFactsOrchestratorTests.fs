namespace Wanxiangshu.Next.Tests.JournalTests

open System
open Xunit
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal

module AgentFactsOrchestratorTests =

    let private createTestEnv seq dt agentFact rt =
        { RuntimeId = rt
          LocalSeq = LocalSeq.create seq
          ObservedAt = dt
          EventId = EventId.create ("evt-" + string seq)
          Stream = StreamId.Workspace
          TurnId = None
          Fact = Fact.Agent agentFact }

    let private createSessionEnv seq dt agentFact rt sid =
        { RuntimeId = rt
          LocalSeq = LocalSeq.create seq
          ObservedAt = dt
          EventId = EventId.create ("evt-" + string seq)
          Stream = StreamId.Session sid
          TurnId = None
          Fact = Fact.Agent agentFact }

    [<Fact>]
    let Orchestrator_candidate_and_publish_facts () =
        let rt = RuntimeId.create "rt-orch-1"
        let mgrIdStr = "manager-alpha"
        let candIdStr = "candidate-001"
        let t0 = DateTimeOffset.UtcNow

        let regFact =
            AgentFact.OrchestratorCandidateRegistered
                {| ManagerId = mgrIdStr
                   CandidateId = candIdStr
                   Branch = "feature/agent-dsl"
                   CommitHash = "c0mm1t123" |}

        let pubFact =
            AgentFact.OrchestratorPublished
                {| ManagerId = mgrIdStr
                   CandidateId = candIdStr
                   CommitHash = "c0mm1t123" |}

        let env1 = createTestEnv 1L t0 regFact rt
        let env2 = createTestEnv 2L (t0.AddSeconds 1.0) pubFact rt

        let proj = AgentFacts.apply AgentFacts.empty [ env1; env2 ]

        let mgrId = ManagerId.create mgrIdStr
        // Terminal facts remove the manager from both active maps; only the
        // projection-level PublishedCommit marker remains.
        Assert.False(proj.Orchestrator.Managers.ContainsKey mgrId)
        Assert.False(proj.Orchestrator.ManagerJobs.ContainsKey mgrId)
        Assert.Equal(Some "c0mm1t123", proj.Orchestrator.PublishedCommit)

    [<Fact>]
    let Orchestrator_rejected_removes_manager_from_active_projection () =
        let rt = RuntimeId.create "rt-orch-rej-1"
        let mgrIdStr = "manager-beta"
        let candIdStr = "candidate-002"
        let t0 = DateTimeOffset.UtcNow

        let regFact =
            AgentFact.OrchestratorCandidateRegistered
                {| ManagerId = mgrIdStr
                   CandidateId = candIdStr
                   Branch = "feature/agent-dsl"
                   CommitHash = "deadbeef01" |}

        let rejFact =
            AgentFact.OrchestratorRejected
                {| ManagerId = mgrIdStr
                   CandidateId = candIdStr
                   Reason = "review declined" |}

        let env1 = createTestEnv 1L t0 regFact rt
        let env2 = createTestEnv 2L (t0.AddSeconds 1.0) rejFact rt

        let proj = AgentFacts.apply AgentFacts.empty [ env1; env2 ]

        let mgrId = ManagerId.create mgrIdStr
        Assert.False(proj.Orchestrator.Managers.ContainsKey mgrId)
        Assert.False(proj.Orchestrator.ManagerJobs.ContainsKey mgrId)
        Assert.Equal(None, proj.Orchestrator.PublishedCommit)

    [<Fact>]
    let Orchestrator_barrier_facts_fold_into_job () =
        let rt = RuntimeId.create "rt-bar-1"
        let mgrIdStr = "manager-gamma"
        let t0 = DateTimeOffset.UtcNow

        let facts =
            [ AgentFact.OrchestratorManagerJobCreated
                  {| ManagerId = mgrIdStr
                     WorktreePath = "/wt/g"
                     Branch = "manager/g"
                     Prompt = "p" |}
              AgentFact.OrchestratorPreRebaseReviewConfirmed
                  {| ManagerId = mgrIdStr
                     CandidateId = "cand-g"
                     CommitHash = "h1" |}
              AgentFact.OrchestratorCandidateRegistered
                  {| ManagerId = mgrIdStr
                     CandidateId = "cand-g"
                     Branch = "manager/g"
                     CommitHash = "h1" |}
              AgentFact.OrchestratorRebased
                  {| ManagerId = mgrIdStr
                     CandidateId = "cand-g"
                     RebasedCommit = "h2" |}
              AgentFact.OrchestratorConflictDetected
                  {| ManagerId = mgrIdStr
                     CandidateId = "cand-g"
                     Files = [ "x"; "y" ] |}
              AgentFact.OrchestratorPostRebaseReviewConfirmed
                  {| ManagerId = mgrIdStr
                     CandidateId = "cand-g"
                     RebasedCommit = "h2" |}
              AgentFact.OrchestratorPublishClaimed
                  {| ManagerId = mgrIdStr
                     CandidateId = "cand-g"
                     ExpectedTargetHead = "E" |} ]

        let envs =
            facts
            |> List.mapi (fun i f -> createTestEnv (int64 (i + 1)) (t0.AddSeconds(float i)) f rt)

        let proj = AgentFacts.apply AgentFacts.empty envs
        let mgrId = ManagerId.create mgrIdStr
        let job = proj.Orchestrator.ManagerJobs.[mgrId]
        Assert.Equal(Some "h1", job.PreRebaseReviewCommit)
        Assert.Equal(Some "h1", job.CandidateCommit)
        Assert.Equal(Some "h2", job.RebasedCommit)
        Assert.Equal(Some [ "x"; "y" ], job.ConflictFiles)
        Assert.Equal(Some "h2", job.PostRebaseReviewCommit)
        Assert.Equal(Some "E", job.PublishClaimHead)
        // Manager stays active until Published.
        Assert.True(proj.Orchestrator.ManagerJobs.ContainsKey mgrId)

    [<Fact>]
    let Orchestrator_published_removes_barrier_job_from_active_projection () =
        let rt = RuntimeId.create "rt-bar-2"
        let mgrIdStr = "manager-delta"
        let t0 = DateTimeOffset.UtcNow

        let facts =
            [ AgentFact.OrchestratorManagerJobCreated
                  {| ManagerId = mgrIdStr
                     WorktreePath = "/wt/d"
                     Branch = "manager/d"
                     Prompt = "p" |}
              AgentFact.OrchestratorCandidateRegistered
                  {| ManagerId = mgrIdStr
                     CandidateId = "cand-d"
                     Branch = "manager/d"
                     CommitHash = "h2" |}
              AgentFact.OrchestratorRebased
                  {| ManagerId = mgrIdStr
                     CandidateId = "cand-d"
                     RebasedCommit = "h2" |}
              AgentFact.OrchestratorPublished
                  {| ManagerId = mgrIdStr
                     CandidateId = "cand-d"
                     CommitHash = "h2" |} ]

        let envs =
            facts
            |> List.mapi (fun i f -> createTestEnv (int64 (i + 1)) (t0.AddSeconds(float i)) f rt)

        let proj = AgentFacts.apply AgentFacts.empty envs
        let mgrId = ManagerId.create mgrIdStr
        Assert.False(proj.Orchestrator.ManagerJobs.ContainsKey mgrId)
        Assert.False(proj.Orchestrator.Managers.ContainsKey mgrId)
        Assert.Equal(Some "h2", proj.Orchestrator.PublishedCommit)

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
                   ToolCallId = "call-1"
                   GitTreeHash = treeHash
                   Verdict = ReviewGuardVerdict.Perfect |}

        let fact2 =
            AgentFact.ReviewVerdictRecorded
                {| ManagerSessionId = sid
                   ReviewerSessionId = sid
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
                   ToolCallId = "call-3"
                   GitTreeHash = treeHash
                   Verdict = ReviewGuardVerdict.Perfect |}

        let fact4 =
            AgentFact.ReviewVerdictRecorded
                {| ManagerSessionId = sid
                   ReviewerSessionId = sid
                   ToolCallId = "call-4"
                   GitTreeHash = treeHash
                   Verdict = ReviewGuardVerdict.Perfect |}

        let envs =
            [ fact1; fact2; barrierFact; fact3; fact4 ]
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
                   ToolCallId = "call-1"
                   GitTreeHash = treeHash
                   Verdict = ReviewGuardVerdict.Perfect |}

        let fact2 =
            AgentFact.ReviewVerdictRecorded
                {| ManagerSessionId = sid
                   ReviewerSessionId = sid
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
        // The tree hash must clear on a barrier: the review-state reader treats
        // "same tree + zero perfects" as REVISE, so keeping the hash would make a
        // fresh barrier on an unchanged tree indistinguishable from a revision.
        Assert.Equal(None, rg.LastGitTreeHash)
