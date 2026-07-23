namespace Wanxiangshu.Next.Tests.JournalTests

open System
open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal

module FactCodecTests =

    [<Fact>]
    let Serialize_and_deserialize_RuntimeStarted_fact () =
        let rt = RuntimeId.create "rt-codec-1"
        let now = DateTimeOffset.UtcNow

        let fact =
            Fact.Runtime(
                RuntimeStarted
                    {| RuntimeId = rt
                       ProcessId = 1234
                       StartedAt = now |}
            )

        let json = FactCodec.serializeFact fact
        let res = FactCodec.deserializeFact json

        match res with
        | Ok(Fact.Runtime(RuntimeStarted r)) ->
            Assert.Equal(rt, r.RuntimeId)
            Assert.Equal(1234, r.ProcessId)
        | _ -> Assert.True(false, sprintf "Expected Ok RuntimeStarted, got: %A" res)

    [<Fact>]
    let Serialize_and_deserialize_AgentFact () =
        let sid = SessionId.create "s1"
        let revSid = SessionId.create "s2"

        let fact =
            Fact.Agent(
                AgentFact.ReviewVerdictRecorded
                    {| ManagerSessionId = sid
                       ReviewerSessionId = revSid
                       ToolCallId = "call123"
                       GitTreeHash = "tree123"
                       Verdict = ReviewGuardVerdict.Perfect |}
            )

        let json = FactCodec.serializeFact fact
        let res = FactCodec.deserializeFact json

        match res with
        | Ok(Fact.Agent(AgentFact.ReviewVerdictRecorded r)) ->
            Assert.Equal(sid, r.ManagerSessionId)
            Assert.Equal(revSid, r.ReviewerSessionId)
            Assert.Equal("call123", r.ToolCallId)
            Assert.Equal("tree123", r.GitTreeHash)
            Assert.Equal(ReviewGuardVerdict.Perfect, r.Verdict)
        | _ -> Assert.True(false, sprintf "Expected Ok ReviewVerdictRecorded, got: %A" res)
