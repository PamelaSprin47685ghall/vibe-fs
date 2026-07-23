namespace Wanxiangshu.Next.Tests

open System
open System.Threading
open System.Threading.Tasks
open Xunit
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Session.SessionFlows
open Wanxiangshu.Next.Tests.SessionTestSupport

module SessionProtocolTests =

    [<Fact>]
    let ``Driver_second_Activate_fails`` () =
        let registry = SessionDrivers()

        let key =
            { RuntimeId = RuntimeId.create "rt1"
              SessionId = SessionId.create "s1" }

        use cts1 = new CancellationTokenSource()
        use cts2 = new CancellationTokenSource()

        let activated1 = registry.Activate(key, cts1)
        let activated2 = registry.Activate(key, cts2)

        Assert.True(activated1)
        Assert.False(activated2)

    [<Fact>]
    let ``evaluateSendOnce_HistoricalHit`` () =
        let sessionId = SessionId.create "session1"
        let turnId = TurnId.create "turn1"

        let testModel =
            Some
                { ProviderId = "test"
                  ModelId = "model1"
                  Variant = None }

        let key =
            PromptKey.create sessionId turnId PromptPurpose.ContinueTodo testModel 1 None "hash123"

        let keyStr = PromptKey.asString key

        let history =
            { Key = keyStr
              UserMessageId = Some(MessageId.create "u1")
              AssistantMessageId = Some(MessageId.create "a1")
              Outcome = Some(Fact.PromptOutcome.Delivered(MessageId.create "a1"))
              CompletedAt = Some DateTimeOffset.UtcNow }

        let historical: HistoricalPromptIndex = Map.ofList [ (keyStr, history) ]
        let local: LocalPromptProtocol = PromptProtocol.emptyLocalProtocol

        let decision = PromptProtocol.evaluateSendOnce historical local key

        match decision with
        | HistoricalHit h -> Assert.Equal(history, h)
        | _ -> Assert.Fail(sprintf "Expected HistoricalHit but got %A" decision)

    [<Fact>]
    let ``evaluateSendOnce_LocalPending_blocks_different_key`` () =
        let sessionId = SessionId.create "session1"
        let turnId = TurnId.create "turn1"

        let testModel =
            Some
                { ProviderId = "test"
                  ModelId = "model1"
                  Variant = None }

        let keyA =
            PromptKey.create sessionId turnId PromptPurpose.ContinueTodo testModel 1 None "hashA"

        let keyB =
            PromptKey.create sessionId turnId PromptPurpose.ContinueTodo testModel 2 None "hashB"

        let pending =
            { RequestKey = keyA
              DispatchId = DispatchId.create "dispatch1"
              UserMessageId = Some(MessageId.create "u1")
              SubmittedAt = DateTimeOffset.UtcNow }

        let historical: HistoricalPromptIndex = PromptProtocol.emptyHistoricalIndex
        let local: LocalPromptProtocol = Map.ofList [ (sessionId, Some pending) ]

        let decision = PromptProtocol.evaluateSendOnce historical local keyB

        match decision with
        | LocalPending p -> Assert.Equal(pending, p)
        | _ -> Assert.Fail(sprintf "Expected LocalPending but got %A" decision)

    [<Fact>]
    let ``evaluateSendOnce_Uncertain_submitted_without_terminal`` () =
        let sessionId = SessionId.create "session1"
        let turnId = TurnId.create "turn1"

        let testModel =
            Some
                { ProviderId = "test"
                  ModelId = "model1"
                  Variant = None }

        let key =
            PromptKey.create sessionId turnId PromptPurpose.ContinueTodo testModel 1 None "hash123"

        let keyStr = PromptKey.asString key

        let history =
            { Key = keyStr
              UserMessageId = Some(MessageId.create "u1")
              AssistantMessageId = None
              Outcome = None
              CompletedAt = None }

        let historical: HistoricalPromptIndex = Map.ofList [ (keyStr, history) ]
        let local: LocalPromptProtocol = PromptProtocol.emptyLocalProtocol

        let decision = PromptProtocol.evaluateSendOnce historical local key

        match decision with
        | Uncertain reason -> Assert.Equal("submitted-without-terminal", reason)
        | _ -> Assert.Fail(sprintf "Expected Uncertain submitted-without-terminal but got %A" decision)

    [<Fact>]
    let ``evaluateSendOnce_Uncertain_requested_without_terminal`` () =
        let sessionId = SessionId.create "session1"
        let turnId = TurnId.create "turn1"

        let testModel =
            Some
                { ProviderId = "test"
                  ModelId = "model1"
                  Variant = None }

        let key =
            PromptKey.create sessionId turnId PromptPurpose.ContinueTodo testModel 1 None "hash123"

        let keyStr = PromptKey.asString key

        let history =
            { Key = keyStr
              UserMessageId = None
              AssistantMessageId = None
              Outcome = None
              CompletedAt = None }

        let historical: HistoricalPromptIndex = Map.ofList [ (keyStr, history) ]
        let local: LocalPromptProtocol = PromptProtocol.emptyLocalProtocol

        let decision = PromptProtocol.evaluateSendOnce historical local key

        match decision with
        | Uncertain reason -> Assert.Equal("requested-without-terminal", reason)
        | _ -> Assert.Fail(sprintf "Expected Uncertain requested-without-terminal but got %A" decision)

    [<Fact>]
    let ``AcceptVerdict_commits_ReviewApplied_fact`` () =
        task {
            let mutable committedFacts = []

            let commit (fact: Fact.Fact) : SessionFlow<unit> =
                session {
                    committedFacts <- fact :: committedFacts
                    return ()
                }

            let report: Review.ReviewReport =
                { Text = "LGT2"
                  Verdict = Fact.ReviewVerdict.Passed }

            let program = Review.acceptVerdict commit 1 report

            let script =
                createTestScript
                    { Unfinished = false
                      ProgressStamp = 0L }
                    (fun () -> session { return () })

            let! res = Flow.run script CancellationToken.None program

            Assert.Equal(Ok Fact.ReviewVerdict.Passed, res)
            Assert.Single(committedFacts) |> ignore

            match List.head committedFacts with
            | Fact.Review(Fact.ReviewFact.ReviewApplied applied) ->
                Assert.Equal(Fact.ReviewVerdict.Passed, applied.Verdict)
                Assert.Equal(2, applied.Round)
                Assert.True(applied.ResultingTodo.IsNone)
            | other -> Assert.Fail(sprintf "Expected ReviewApplied fact, got %A" other)
        }

    [<Fact>]
    let ``FifoInbox_TryPost_InboxFull_when_capacity_exceeded`` () =
        let inbox = FifoInbox(1) :> ISessionInbox
        let ev1 = HumanMessageEvent(TurnId.create "t1", "hello")
        let ev2 = HumanMessageEvent(TurnId.create "t2", "world")

        let res1 = inbox.TryPost(ev1)
        let res2 = inbox.TryPost(ev2)

        Assert.Equal(Ok(), res1)
        Assert.Equal(Error SessionError.InboxFull, res2)

    [<Fact>]
    let ``FifoInbox_Receive_throws_when_cancelled`` () =
        task {
            use cancellation = new CancellationTokenSource()
            let inbox = FifoInbox(1) :> ISessionInbox
            let pending = inbox.Receive(cancellation.Token)

            cancellation.Cancel()

            let! error = Assert.ThrowsAsync<OperationCanceledException>(fun () -> pending :> Task)
            Assert.NotNull(error)
        }

    [<Fact>]
    let ``SendOutcomeMap_toPromptOutcome_all_cases`` () =
        let msgId = MessageId.create "msg_test_delivered"
        Assert.Equal(Fact.PromptOutcome.Delivered msgId, SendOutcomeMap.toPromptOutcome (SendOutcome.Delivered msgId))

        Assert.Equal(
            Fact.PromptOutcome.RetryableFailure "network error",
            SendOutcomeMap.toPromptOutcome (SendOutcome.Retryable "network error")
        )

        Assert.Equal(
            Fact.PromptOutcome.FatalFailure "unrecoverable error",
            SendOutcomeMap.toPromptOutcome (SendOutcome.Fatal "unrecoverable error")
        )

        Assert.Equal(
            Fact.PromptOutcome.AcceptanceUnknown("timeout", Some msgId),
            SendOutcomeMap.toPromptOutcome (SendOutcome.AcceptanceUnknown("timeout", Some msgId))
        )

        Assert.Equal(
            Fact.PromptOutcome.AcceptanceUnknown("timeout", None),
            SendOutcomeMap.toPromptOutcome (SendOutcome.AcceptanceUnknown("timeout", None))
        )

    [<Fact>]
    let ``Model_ofString_valid_and_invalid`` () =
        Assert.Equal(
            Ok
                { ProviderId = "openai"
                  ModelId = "gpt-4o"
                  Variant = None },
            Model.ofString "openai/gpt-4o"
        )

        Assert.Equal(
            Ok
                { ProviderId = "anthropic"
                  ModelId = "claude-3-5"
                  Variant = Some "sonnet" },
            Model.ofString "anthropic/claude-3-5/sonnet"
        )

        match Model.ofString "invalid_no_slash" with
        | Error _ -> ()
        | Ok m -> Assert.Fail(sprintf "Expected Error for invalid_no_slash but got %A" m)

        match Model.ofString "provider/" with
        | Error _ -> ()
        | Ok m -> Assert.Fail(sprintf "Expected Error for provider/ but got %A" m)

        match Model.ofString "" with
        | Error _ -> ()
        | Ok m -> Assert.Fail(sprintf "Expected Error for empty string but got %A" m)
