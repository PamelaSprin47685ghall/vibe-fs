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

    let private makeEnv seq dt stream fact rt =
        { RuntimeId = rt
          LocalSeq = LocalSeq.create seq
          ObservedAt = dt
          EventId = EventId.create ("e" + string seq)
          Stream = stream
          TurnId = None
          Fact = fact }

    [<Fact>]
    let Fold_isolates_TodoChanged_and_ReviewApplied_by_SessionId () =
        let rt = RuntimeId.create "rt-session-isolation"
        let t0 = DateTimeOffset.UtcNow
        let sA, sB = SessionId.create "session-a", SessionId.create "session-b"

        let envs =
            [ makeEnv 1L t0 (StreamId.Session sA) (Fact.Todo(TodoChanged {| Snapshot = { Items = [ "task-a1" ] } |})) rt
              makeEnv
                  2L
                  (t0.AddSeconds 1.0)
                  (StreamId.Session sB)
                  (Fact.Todo(TodoChanged {| Snapshot = { Items = [ "task-b1" ] } |}))
                  rt
              makeEnv
                  3L
                  (t0.AddSeconds 2.0)
                  (StreamId.Session sA)
                  (Fact.Review(
                      ReviewApplied
                          {| Verdict = ReviewVerdict.NeedsChanges [ "fix a" ]
                             Round = 1
                             ResultingTodo = Some { Items = [ "task-a1"; "task-a2" ] } |}
                  ))
                  rt
              makeEnv
                  4L
                  (t0.AddSeconds 3.0)
                  (StreamId.Session sB)
                  (Fact.Review(
                      ReviewApplied
                          {| Verdict = ReviewVerdict.Passed
                             Round = 1
                             ResultingTodo = None |}
                  ))
                  rt ]

        let proj = Fold.apply Fold.empty envs
        let projA = Map.find sA proj.SessionProjections
        Assert.Equal({ Items = [ "task-a1"; "task-a2" ] }, projA.Todos.Value)
        Assert.Equal(ReviewVerdict.NeedsChanges [ "fix a" ], projA.LastReview.Value)

        let projB = Map.find sB proj.SessionProjections
        Assert.Equal({ Items = [ "task-b1" ] }, projB.Todos.Value)
        Assert.Equal(ReviewVerdict.Passed, projB.LastReview.Value)

    [<Fact>]
    let Fold_keeps_TodoChanged_isolated_between_sessions () =
        let rt = RuntimeId.create "rt-todo-isolation"
        let t0 = DateTimeOffset.UtcNow
        let sA, sB = SessionId.create "todo-session-a", SessionId.create "todo-session-b"

        let envs =
            [ makeEnv 1L t0 (StreamId.Session sA) (Fact.Todo(TodoChanged {| Snapshot = { Items = [ "a" ] } |})) rt
              makeEnv
                  2L
                  (t0.AddSeconds 1.0)
                  (StreamId.Session sB)
                  (Fact.Todo(TodoChanged {| Snapshot = { Items = [ "b" ] } |}))
                  rt ]

        let projections = (Fold.apply Fold.empty envs).SessionProjections

        Assert.Equal(Some { Items = [ "a" ] }, (Map.find sA projections).Todos)
        Assert.Equal(Some { Items = [ "b" ] }, (Map.find sB projections).Todos)

    [<Fact>]
    let Fold_preserves_Workspace_events_as_global_projections () =
        let rt = RuntimeId.create "rt-workspace-global"
        let t0 = DateTimeOffset.UtcNow

        let env1: Envelope =
            { RuntimeId = rt
              LocalSeq = LocalSeq.create 1L
              ObservedAt = t0
              EventId = EventId.create "e1"
              Stream = StreamId.Workspace
              TurnId = None
              Fact = Fact.Todo(TodoChanged {| Snapshot = { Items = [ "global-task" ] } |}) }

        let proj = Fold.apply Fold.empty [ env1 ]
        Assert.Equal({ Items = [ "global-task" ] }, proj.Todos.Value)

    [<Fact>]
    let Fold_applies_SessionFact_ChildFact_ProcessFact_and_SquadFact () =
        let rt = RuntimeId.create "rt-all-facts"
        let t0 = DateTimeOffset.UtcNow
        let sessionId = SessionId.create "session-1"
        let childId = ChildId.create "child-1"
        let processId = ProcessId.create "proc-1"

        let envs =
            [ makeEnv
                  1L
                  t0
                  (StreamId.Session sessionId)
                  (Fact.Session(HumanTurnStarted {| TurnId = TurnId.create "turn-1" |}))
                  rt
              makeEnv
                  2L
                  (t0.AddSeconds 1.0)
                  (StreamId.Session sessionId)
                  (Fact.Child(
                      ChildCompletedFact
                          {| ChildId = childId
                             Result = ChildCompleted "done" |}
                  ))
                  rt
              makeEnv
                  3L
                  (t0.AddSeconds 2.0)
                  (StreamId.Session sessionId)
                  (Fact.Process(
                      ProcessExited
                          {| ProcessId = processId
                             Result =
                              { ExitCode = 0
                                Stdout = "ok"
                                Stderr = ""
                                StdoutTruncated = false
                                StderrTruncated = false } |}
                  ))
                  rt
              makeEnv
                  4L
                  (t0.AddSeconds 3.0)
                  (StreamId.Session sessionId)
                  (Fact.Squad(
                      TaskVerifiedFact
                          {| TaskId = "task-1"
                             Result = TaskVerified "verified" |}
                  ))
                  rt
              makeEnv
                  5L
                  (t0.AddSeconds 4.0)
                  (StreamId.Session sessionId)
                  (Fact.Session(SessionSettled {| Result = SessionResult.Completed "all done" |}))
                  rt ]

        let proj = Fold.apply Fold.empty envs
        let sessionProj = Map.find sessionId proj.SessionProjections

        Assert.Equal(Some(TurnId.create "turn-1"), sessionProj.HumanTurnId)
        Assert.Equal(Some(SessionResult.Completed "all done"), sessionProj.SettledResult)
        Assert.Equal(ChildCompleted "done", Map.find childId sessionProj.Children)
        Assert.Equal(0, (Map.find processId sessionProj.Processes).ExitCode)
        Assert.Equal(TaskVerified "verified", Map.find "task-1" sessionProj.SquadTasks)
