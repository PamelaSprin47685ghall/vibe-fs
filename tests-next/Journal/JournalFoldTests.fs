namespace Wanxiangshu.Next.Tests.JournalTests

open System
open System.IO
open Xunit
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open JournalTestSupport

module JournalFoldTests =

    let private createTestEnv seq dt fact rt turnId =
        { RuntimeId = rt
          LocalSeq = LocalSeq.create seq
          ObservedAt = dt
          EventId = EventId.create ("e" + string seq)
          Stream = StreamId.Session(SessionId.create "s1")
          TurnId = Some turnId
          Fact = fact }

    [<Fact>]
    let Fold_applies_TodoChanged () =
        let rt = RuntimeId.create "rt-fold"
        let t0 = DateTimeOffset.UtcNow

        let env1: Envelope =
            { RuntimeId = rt
              LocalSeq = LocalSeq.create 1L
              ObservedAt = t0
              EventId = EventId.create "e1"
              Stream = StreamId.Workspace
              TurnId = None
              Fact =
                Fact.Runtime(
                    RuntimeStarted
                        {| RuntimeId = rt
                           ProcessId = 1
                           StartedAt = t0 |}
                ) }

        let env2: Envelope =
            { RuntimeId = rt
              LocalSeq = LocalSeq.create 2L
              ObservedAt = t0.AddSeconds(1.0)
              EventId = EventId.create "e2"
              Stream = StreamId.Workspace
              TurnId = None
              Fact = Fact.Todo(TodoChanged {| Snapshot = { Items = [ "item1" ] } |}) }

        let env3: Envelope =
            { RuntimeId = rt
              LocalSeq = LocalSeq.create 3L
              ObservedAt = t0.AddSeconds(2.0)
              EventId = EventId.create "e3"
              Stream = StreamId.Workspace
              TurnId = None
              Fact = Fact.Todo(TodoChanged {| Snapshot = { Items = [ "item1"; "item2" ] } |}) }

        let proj = Fold.apply Fold.empty [ env1; env2; env3 ]
        Assert.Equal(Some rt, proj.RuntimeId)
        Assert.True(proj.Todos.IsSome)
        let items = proj.Todos.Value.Items
        Assert.Equal(2, items.Length)
        Assert.Equal("item1", items.[0])
        Assert.Equal("item2", items.[1])

    [<Fact>]
    let Fold_rebuilds_HistoricalPrompts_from_Prompt_facts () =
        let rt = RuntimeId.create "rt-prompt-fold"
        let turnId = TurnId.create "t1"
        let keyStr = "s1:t1:ContinueTodo:model1:1:none:hash123"
        let msgIdU, msgIdA = MessageId.create "u1", MessageId.create "a1"
        let t0 = DateTimeOffset.UtcNow
        let delivered: PromptOutcome = Fact.Delivered msgIdA

        let reqFact =
            Fact.Prompt(
                PromptRequested
                    {| PromptKey = keyStr
                       TurnId = turnId
                       Purpose = "ContinueTodo" |}
            )

        let subFact =
            Fact.Prompt(
                PromptSubmitted
                    {| PromptKey = keyStr
                       MessageId = msgIdU |}
            )

        let termFact =
            Fact.Prompt(
                PromptTerminal
                    {| PromptKey = keyStr
                       Outcome = delivered
                       AssistantMessageId = Some msgIdA |}
            )

        let env1 = createTestEnv 1L t0 reqFact rt turnId
        let env2 = createTestEnv 2L (t0.AddSeconds 1.0) subFact rt turnId
        let env3 = createTestEnv 3L (t0.AddSeconds 2.0) termFact rt turnId

        let proj = Fold.apply Fold.empty [ env1; env2; env3 ]

        Assert.True(Map.containsKey keyStr proj.HistoricalPrompts)
        let history = Map.find keyStr proj.HistoricalPrompts
        Assert.Equal(Some msgIdU, history.UserMessageId)
        Assert.Equal(Some msgIdA, history.AssistantMessageId)
        Assert.Equal(Some delivered, history.Outcome)
