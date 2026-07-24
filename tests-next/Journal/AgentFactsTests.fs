namespace Wanxiangshu.Next.Tests.JournalTests

open System
open Xunit
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal

module AgentFactsTests =

    let private createTestEnv seq dt agentFact rt (sessionId: SessionId option) =
        { RuntimeId = rt
          LocalSeq = LocalSeq.create seq
          ObservedAt = dt
          EventId = EventId.create ("evt-" + string seq)
          Stream =
            match sessionId with
            | Some sid -> StreamId.Session sid
            | None -> StreamId.Workspace
          TurnId = None
          Fact = Fact.Agent agentFact }

    [<Fact>]
    let Double_perfect_same_tree_fold () =
        let rt = RuntimeId.create "rt-review-1"
        let sid = SessionId.create "session-review"
        let treeHash = "abc123treehash"
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

        let env1 = createTestEnv 1L t0 fact1 rt (Some sid)
        let env2 = createTestEnv 2L (t0.AddSeconds 1.0) fact2 rt (Some sid)

        let proj = AgentFacts.apply AgentFacts.empty [ env1; env2 ]

        Assert.True(proj.Sessions.ContainsKey sid)
        let sessionProj = proj.Sessions.[sid]
        Assert.True(sessionProj.ReviewGuard.IsSome)
        let rg = sessionProj.ReviewGuard.Value

        Assert.Equal(2, rg.ConsecutivePerfects)
        Assert.True(rg.IsConfirmed)
        Assert.Equal(Some(GitTreeHash.create treeHash), rg.LastGitTreeHash)

    [<Fact>]
    let Revise_and_hash_change_resets_perfects () =
        let rt = RuntimeId.create "rt-review-2"
        let sid = SessionId.create "session-review-2"
        let treeHash1 = "hash-v1"
        let treeHash2 = "hash-v2"
        let t0 = DateTimeOffset.UtcNow

        // Perfect on treeHash1 -> consecutive = 1
        let fact1 =
            AgentFact.ReviewVerdictRecorded
                {| ManagerSessionId = sid
                   ReviewerSessionId = sid
                   ToolCallId = "call-1"
                   GitTreeHash = treeHash1
                   Verdict = ReviewGuardVerdict.Perfect |}

        // Revise on treeHash1 -> consecutive = 0
        let fact2 =
            AgentFact.ReviewVerdictRecorded
                {| ManagerSessionId = sid
                   ReviewerSessionId = sid
                   ToolCallId = "call-2"
                   GitTreeHash = treeHash1
                   Verdict = ReviewGuardVerdict.Revise |}

        // Perfect on treeHash2 (new hash) -> consecutive = 1
        let fact3 =
            AgentFact.ReviewVerdictRecorded
                {| ManagerSessionId = sid
                   ReviewerSessionId = sid
                   ToolCallId = "call-3"
                   GitTreeHash = treeHash2
                   Verdict = ReviewGuardVerdict.Perfect |}

        let env1 = createTestEnv 1L t0 fact1 rt (Some sid)
        let env2 = createTestEnv 2L (t0.AddSeconds 1.0) fact2 rt (Some sid)
        let env3 = createTestEnv 3L (t0.AddSeconds 2.0) fact3 rt (Some sid)

        let proj = AgentFacts.apply AgentFacts.empty [ env1; env2; env3 ]

        let rg = proj.Sessions.[sid].ReviewGuard.Value
        Assert.Equal(1, rg.ConsecutivePerfects)
        Assert.False(rg.IsConfirmed)
        Assert.Equal(Some(GitTreeHash.create treeHash2), rg.LastGitTreeHash)

    [<Fact>]
    let Fallback_cumulative_side_selection_and_death () =
        let rt = RuntimeId.create "rt-fallback-1"
        let sid = SessionId.create "session-fallback"
        let t0 = DateTimeOffset.UtcNow

        let failFact =
            AgentFact.FallbackFailureRecorded
                {| SessionId = sid
                   Reason = "Timeout" |}

        let env1 = createTestEnv 1L t0 failFact rt (Some sid)
        let env2 = createTestEnv 2L (t0.AddSeconds 1.0) failFact rt (Some sid)
        let env3 = createTestEnv 3L (t0.AddSeconds 2.0) failFact rt (Some sid)
        let env4 = createTestEnv 4L (t0.AddSeconds 3.0) failFact rt (Some sid)

        // Step 1: 1st failure -> SideA, 1 failure on current side, total 1, not dead
        let proj1 = AgentFacts.apply AgentFacts.empty [ env1 ]
        let fb1 = proj1.Sessions.[sid].Fallback.Value
        Assert.Equal(SideA, fb1.Side)
        Assert.Equal(1, fb1.FailuresOnCurrentSide)
        Assert.Equal(1, fb1.TotalFailures)
        Assert.False(fb1.IsDead)

        // Step 2: 2nd failure -> SideB, 0 failures on SideB, total 2, not dead
        let proj2 = AgentFacts.apply AgentFacts.empty [ env1; env2 ]
        let fb2 = proj2.Sessions.[sid].Fallback.Value
        Assert.Equal(SideB, fb2.Side)
        Assert.Equal(0, fb2.FailuresOnCurrentSide)
        Assert.Equal(2, fb2.TotalFailures)
        Assert.False(fb2.IsDead)

        // Step 3: 3rd failure -> SideB, 1 failure on SideB, total 3, not dead
        let proj3 = AgentFacts.apply AgentFacts.empty [ env1; env2; env3 ]
        let fb3 = proj3.Sessions.[sid].Fallback.Value
        Assert.Equal(SideB, fb3.Side)
        Assert.Equal(1, fb3.FailuresOnCurrentSide)
        Assert.Equal(3, fb3.TotalFailures)
        Assert.False(fb3.IsDead)

        // Step 4: 4th failure -> SideB, 2 failures on SideB, total 4, IS DEAD!
        let proj4 = AgentFacts.apply AgentFacts.empty [ env1; env2; env3; env4 ]
        let fb4 = proj4.Sessions.[sid].Fallback.Value
        Assert.Equal(SideB, fb4.Side)
        Assert.Equal(2, fb4.FailuresOnCurrentSide)
        Assert.Equal(4, fb4.TotalFailures)
        Assert.True(fb4.IsDead)

    [<Fact>]
    let Companion_baseline_and_replacement () =
        let rt = RuntimeId.create "rt-companion-1"
        let sid = SessionId.create "session-companion"
        let t0 = DateTimeOffset.UtcNow

        let baseFact =
            AgentFact.CompanionBaselineSet
                {| SessionId = sid
                   Projection = "{\"state\":\"base\"}" |}

        let replaceFact =
            AgentFact.CompanionCheckpointReplaced
                {| SessionId = sid
                   Content = "Updated Blog Post" |}

        let activeFact =
            AgentFact.CompanionReplacementActiveSet {| SessionId = sid; Active = true |}

        let env1 = createTestEnv 1L t0 baseFact rt (Some sid)
        let env2 = createTestEnv 2L (t0.AddSeconds 1.0) replaceFact rt (Some sid)
        let env3 = createTestEnv 3L (t0.AddSeconds 2.0) activeFact rt (Some sid)

        let proj = AgentFacts.apply AgentFacts.empty [ env1; env2; env3 ]

        let comp = proj.Sessions.[sid].Companion.Value
        Assert.Equal(Some "{\"state\":\"base\"}", comp.LastSuccessfulProjection)
        Assert.Equal(Some "Updated Blog Post", comp.CurrentB)
        Assert.True(comp.ReplacementActive)

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

        let env1 = createTestEnv 1L t0 regFact rt None
        let env2 = createTestEnv 2L (t0.AddSeconds 1.0) pubFact rt None

        let proj = AgentFacts.apply AgentFacts.empty [ env1; env2 ]

        let mgrId = ManagerId.create mgrIdStr
        let candId = CandidateId.create candIdStr
        Assert.True(proj.Orchestrator.Managers.ContainsKey mgrId)

        let mgrState = proj.Orchestrator.Managers.[mgrId]
        Assert.Equal(Some(Published(candId, "c0mm1t123")), mgrState.Status)
        Assert.Equal(Some "c0mm1t123", proj.Orchestrator.PublishedCommit)

    [<Fact>]
    let Agent_linkage_and_durable_effect_folds () =
        let rt = RuntimeId.create "rt-misc-1"
        let parentSid = SessionId.create "session-parent"
        let childId = ChildId.create "child-sub-agent"
        let effIdStr = "effect-001"
        let t0 = DateTimeOffset.UtcNow

        let linkFact =
            AgentFact.AgentLinked
                {| ParentId = parentSid
                   ChildId = childId
                   TargetAgent = "WorkerAgent" |}

        let reqEffect =
            AgentFact.DurableEffectRequested
                {| EffectId = effIdStr
                   SessionId = parentSid
                   Target = "FileSystem"
                   Payload = "Write config" |}

        let accEffect =
            AgentFact.DurableEffectAccepted
                {| EffectId = effIdStr
                   SessionId = parentSid
                   Result = "Success" |}

        let env1 = createTestEnv 1L t0 linkFact rt (Some parentSid)
        let env2 = createTestEnv 2L (t0.AddSeconds 1.0) reqEffect rt (Some parentSid)
        let env3 = createTestEnv 3L (t0.AddSeconds 2.0) accEffect rt (Some parentSid)

        let proj = AgentFacts.apply AgentFacts.empty [ env1; env2; env3 ]

        let sessionProj = proj.Sessions.[parentSid]
        Assert.Equal("WorkerAgent", sessionProj.Linkage.Value.LinkedChildren.[childId])

        let effId = EffectId.create effIdStr
        let currentEffectId, effStatus = sessionProj.Effects.Value.Current.Value
        Assert.Equal(effId, currentEffectId)
        Assert.Equal(Accepted("FileSystem", "Write config", "Success"), effStatus)

    [<Fact>]
    let Guard_prompt_accepted_fact_folds_into_accepted_keys () =
        let rt = RuntimeId.create "rt-guard-key"
        let sid = SessionId.create "session-guard"
        let t0 = DateTimeOffset.UtcNow

        let fact1 =
            AgentFact.GuardPromptAccepted
                {| TargetSessionId = sid
                   GuardKey = "key-1"
                   HostMessageId = "msg-101" |}

        let fact2 =
            AgentFact.GuardPromptAccepted
                {| TargetSessionId = sid
                   GuardKey = "key-2"
                   HostMessageId = "msg-102" |}

        let env1 = createTestEnv 1L t0 fact1 rt (Some sid)
        let env2 = createTestEnv 2L (t0.AddSeconds 1.0) fact2 rt (Some sid)

        let proj = AgentFacts.apply AgentFacts.empty [ env1; env2 ]

        let rg = proj.Sessions.[sid].ReviewGuard.Value
        Assert.Equal(Some "key-2", rg.AcceptedGuardKey)
