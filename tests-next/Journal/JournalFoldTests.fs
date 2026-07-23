namespace Wanxiangshu.Next.Tests.JournalTests

open System
open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal

module JournalFoldTests =

    let private makeEnv seq dt stream fact rt =
        { RuntimeId = rt
          LocalSeq = LocalSeq.create seq
          ObservedAt = dt
          EventId = EventId.create ("e" + string seq)
          Stream = stream
          TurnId = None
          Fact = fact }

    [<Fact>]
    let Fold_applies_RuntimeStarted_setting_RuntimeId () =
        let rt = RuntimeId.create "rt-fold"
        let t0 = DateTimeOffset.UtcNow

        let env1 =
            makeEnv
                1L
                t0
                StreamId.Workspace
                (Fact.Runtime(
                    RuntimeStarted
                        {| RuntimeId = rt
                           ProcessId = 1
                           StartedAt = t0 |}
                ))
                rt

        let proj = Fold.apply Fold.empty [ env1 ]
        Assert.Equal(Some rt, proj.RuntimeId)

    [<Fact>]
    let Fold_delegates_Agent_facts_to_AgentProjections () =
        let rt = RuntimeId.create "rt-agent-fold"
        let t0 = DateTimeOffset.UtcNow
        let sid = SessionId.create "s1"
        let revSid = SessionId.create "s2"
        let treeHash = "hash123"

        let env1 =
            makeEnv
                1L
                t0
                StreamId.Workspace
                (Fact.Runtime(
                    RuntimeStarted
                        {| RuntimeId = rt
                           ProcessId = 1
                           StartedAt = t0 |}
                ))
                rt

        let env2 =
            makeEnv
                2L
                (t0.AddSeconds 1.0)
                (StreamId.Session sid)
                (Fact.Agent(
                    AgentFact.ReviewVerdictRecorded
                        {| ManagerSessionId = sid
                           ReviewerSessionId = revSid
                           ToolCallId = "call-1"
                           GitTreeHash = treeHash
                           Verdict = ReviewGuardVerdict.Perfect |}
                ))
                rt

        let proj = Fold.apply Fold.empty [ env1; env2 ]
        Assert.Equal(Some rt, proj.RuntimeId)
        Assert.True(proj.AgentProjections.Sessions.ContainsKey sid)
        let rg = proj.AgentProjections.Sessions.[sid].ReviewGuard.Value
        Assert.Equal(1, rg.ConsecutivePerfects)
