namespace Wanxiangshu.Next.Tests.JournalTests

open System
open Xunit
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Domain

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
                   ProviderRunId = "pr-1"
                   UserPromptText = None
                   UserMessageId = None
                   ToolCallId = "call-1"
                   GitTreeHash = treeHash
                   Verdict = ReviewGuardVerdict.Perfect |}

        let confirmFact =
            AgentFact.GuardPromptAccepted
                {| TargetSessionId = sid
                   GuardKey = "confirm-perfect:guard-key-1"
                   HostMessageId = "msg-confirm-1" |}

        let fact2 =
            AgentFact.ReviewVerdictRecorded
                {| ManagerSessionId = sid
                   ReviewerSessionId = sid
                   ProviderRunId = "pr-2"
                   UserPromptText =
                    Some
                        "PERFECT requires confirmation. Re-read the current tree and call verdict(PERFECT) again to confirm."
                   UserMessageId = None
                   ToolCallId = "call-2"
                   GitTreeHash = treeHash
                   Verdict = ReviewGuardVerdict.Perfect |}

        let env1 = createTestEnv 1L t0 fact1 rt (Some sid)
        let envC = createTestEnv 2L (t0.AddSeconds 0.5) confirmFact rt (Some sid)
        let env2 = createTestEnv 3L (t0.AddSeconds 1.0) fact2 rt (Some sid)

        let proj = AgentFacts.apply AgentFacts.empty [ env1; envC; env2 ]

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
                   ProviderRunId = "pr-1"
                   UserPromptText = None
                   UserMessageId = None
                   ToolCallId = "call-1"
                   GitTreeHash = treeHash1
                   Verdict = ReviewGuardVerdict.Perfect |}

        // Revise on treeHash1 -> consecutive = 0
        let fact2 =
            AgentFact.ReviewVerdictRecorded
                {| ManagerSessionId = sid
                   ReviewerSessionId = sid
                   ProviderRunId = "pr-2"
                   UserPromptText = None
                   UserMessageId = None
                   ToolCallId = "call-2"
                   GitTreeHash = treeHash1
                   Verdict = ReviewGuardVerdict.Revise |}

        // Perfect on treeHash2 (new hash) -> consecutive = 1
        let fact3 =
            AgentFact.ReviewVerdictRecorded
                {| ManagerSessionId = sid
                   ReviewerSessionId = sid
                   ProviderRunId = "pr-3"
                   UserPromptText = None
                   UserMessageId = None
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
    let ``Fallback_modulo4_cursor_advances_without_death`` () =
        let rt = RuntimeId.create "rt-fallback-1"
        let sid = SessionId.create "session-fallback"
        let t0 = DateTimeOffset.UtcNow

        let failFact attempt =
            AgentFact.FallbackFailureRecorded
                {| SessionId = sid
                   LogicalRunId = "run-test"
                   AuthorityRootUserMessageId = "root-test"
                   Reason = "Timeout"
                   AssistantMessageId = sprintf "msg-%d" attempt
                   ProviderAttempt = string attempt |}

        let env1 = createTestEnv 1L t0 (failFact 1) rt (Some sid)
        let env2 = createTestEnv 2L (t0.AddSeconds 1.0) (failFact 2) rt (Some sid)
        let env3 = createTestEnv 3L (t0.AddSeconds 2.0) (failFact 3) rt (Some sid)
        let env4 = createTestEnv 4L (t0.AddSeconds 3.0) (failFact 4) rt (Some sid)

        // Step 1: Offset 1 → SideA
        let proj1 = AgentFacts.apply AgentFacts.empty [ env1 ]
        let fb1 = proj1.Sessions.[sid].Fallback.Value
        Assert.Equal(1uy, fb1.Offset)
        Assert.Equal(AgentPairCursor.SideA, AgentPairCursor.side fb1.Offset)

        // Step 2: Offset 2 → SideB
        let proj2 = AgentFacts.apply AgentFacts.empty [ env1; env2 ]
        let fb2 = proj2.Sessions.[sid].Fallback.Value
        Assert.Equal(2uy, fb2.Offset)
        Assert.Equal(AgentPairCursor.SideB, AgentPairCursor.side fb2.Offset)

        // Step 3: Offset 3 → SideB
        let proj3 = AgentFacts.apply AgentFacts.empty [ env1; env2; env3 ]
        let fb3 = proj3.Sessions.[sid].Fallback.Value
        Assert.Equal(3uy, fb3.Offset)
        Assert.Equal(AgentPairCursor.SideB, AgentPairCursor.side fb3.Offset)

        // Step 4: Offset 0 → SideA (wrap; never dead)
        let proj4 = AgentFacts.apply AgentFacts.empty [ env1; env2; env3; env4 ]
        let fb4 = proj4.Sessions.[sid].Fallback.Value
        Assert.Equal(0uy, fb4.Offset)
        Assert.Equal(AgentPairCursor.SideA, AgentPairCursor.side fb4.Offset)

        // Step 5: continue wrap cycle → Offset 1 / SideA
        let env5 = createTestEnv 5L (t0.AddSeconds 4.0) (failFact 5) rt (Some sid)
        let proj5 = AgentFacts.apply AgentFacts.empty [ env1; env2; env3; env4; env5 ]
        let fb5 = proj5.Sessions.[sid].Fallback.Value
        Assert.equal (1uy, fb5.Offset)
        Assert.Equal(AgentPairCursor.SideA, AgentPairCursor.side fb5.Offset)

    [<Fact>]
    let Companion_advanced_and_replacement () =
        let rt = RuntimeId.create "rt-companion-1"
        let sid = SessionId.create "session-companion"
        let t0 = DateTimeOffset.UtcNow

        let advancedFact =
            AgentFact.CompanionAdvanced
                {| SessionId = sid
                   Projection = "{\"state\":\"base\"}"
                   Content = "Updated Blog Post" |}

        let activeFact =
            AgentFact.CompanionReplacementActiveSet {| SessionId = sid; Active = true |}

        let env1 = createTestEnv 1L t0 advancedFact rt (Some sid)
        let env2 = createTestEnv 2L (t0.AddSeconds 1.0) activeFact rt (Some sid)

        let proj = AgentFacts.apply AgentFacts.empty [ env1; env2 ]

        let comp = proj.Sessions.[sid].Companion.Value
        Assert.Equal(Some "{\"state\":\"base\"}", comp.LastSuccessfulProjection)
        Assert.Equal(Some "Updated Blog Post", comp.LatestB)
        Assert.True(comp.ReplacementActive)

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
                   TargetAgent = "WorkerAgent"
                   Role = Some "Coder" |}

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
