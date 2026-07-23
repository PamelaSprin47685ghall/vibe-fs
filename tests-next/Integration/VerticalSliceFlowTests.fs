namespace Wanxiangshu.Next.Tests.Integration

open System
open System.Threading
open System.Threading.Tasks
open Xunit
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Tests
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport

module VerticalSliceFlowTests =

    [<Fact>]
    let ``SessionFlows_run_commits_PromptRequested_and_awaits_terminal_via_TerminalSignal`` () =
        withTempDir (fun tempDir ->
            task {
                let! startRes = Gateway.start tempDir CancellationToken.None

                match startRes with
                | Error err -> Assert.True(false, sprintf "Gateway start failed: %A" err)
                | Ok gateway ->
                    let sessionId = SessionId.create "session-flow-terminal"
                    let inbox = FifoInbox(100) :> ISessionInbox
                    use driver = new SessionDriver(gateway, sessionId, inbox)

                    let seedTodo =
                        Fact.Todo(TodoChanged {| Snapshot = { Items = [ "implement feature X" ] } |})

                    match gateway.Append (StreamId.Session sessionId) None seedTodo with
                    | Committed _ -> ()
                    | _ -> Assert.True(false, "seed Todo commit failed")

                    let turnId = TurnId.create "msg-flow-terminal"
                    Assert.Equal(Ok(), inbox.TryPost(HumanMessageEvent(turnId, "Build the feature")))

                    let! promptSeen =
                        VerticalSliceWaiters._awaitEnvelope
                            gateway
                            (fun env ->
                                match env.Fact with
                                | Fact.Prompt(Fact.PromptFact.PromptRequested _) -> true
                                | _ -> false)
                            1

                    Assert.True(promptSeen, "Expected PromptRequested")

                    let sessionProj = Map.find sessionId gateway.ProjectionSet.SessionProjections
                    Assert.Equal(None, sessionProj.SettledResult)

                    let userMsgId = MessageId.create "assistant-msg-1"

                    Assert.Equal(
                        Ok(),
                        inbox.TryPost(
                            AssistantTerminalEvent(
                                MessageId.create "continue-flow",
                                userMsgId,
                                Fact.PromptOutcome.Delivered userMsgId
                            )
                        )
                    )

                    let! prompt2Seen =
                        VerticalSliceWaiters._awaitEnvelope
                            gateway
                            (fun env ->
                                match env.Fact with
                                | Fact.Prompt(Fact.PromptFact.PromptTerminal _) -> true
                                | _ -> false)
                            1

                    Assert.True(prompt2Seen, "Expected PromptTerminal after unblock")

                    let clearTodo =
                        Fact.Todo(TodoChanged {| Snapshot = { Items = [] } |})

                    match gateway.Append (StreamId.Session sessionId) None clearTodo with
                    | Committed _ -> ()
                    | _ -> Assert.True(false, "clear Todo commit failed")

                    Assert.Equal(
                        Ok(),
                        inbox.TryPost(
                            AssistantTerminalEvent(
                                MessageId.create "continue-flow-2",
                                MessageId.create "assistant-msg-2",
                                Fact.PromptOutcome.Delivered(MessageId.create "assistant-msg-2")
                            )
                        )
                    )

                    let! settledSeen = VerticalSliceWaiters._awaitSettled gateway sessionId
                    Assert.True(settledSeen, "Expected SessionSettled")

                    let finalEnvelopes = VerticalSliceJournalSupport._readEnvelopes gateway.JournalPath

                    let terminalCount =
                        finalEnvelopes
                        |> Array.filter (fun env ->
                            match env.Fact with
                            | Fact.Prompt(Fact.PromptFact.PromptTerminal _) -> true
                            | _ -> false)
                        |> Array.length

                    Assert.True(terminalCount >= 1, sprintf "Expected >= 1 PromptTerminal, got %d" terminalCount)

                    let! _ = gateway.DisposeAsync()
                    ()
            })

    [<Fact>]
    let ``SessionDriver_cancel_unblocks_stuck_flow`` () =
        withTempDir (fun tempDir ->
            task {
                let! startRes = Gateway.start tempDir CancellationToken.None

                match startRes with
                | Error err -> Assert.True(false, sprintf "Gateway start failed: %A" err)
                | Ok gateway ->
                    let sessionId = SessionId.create "session-flow-cancel"
                    let inbox = FifoInbox(100) :> ISessionInbox

                    let seedTodo =
                        Fact.Todo(TodoChanged {| Snapshot = { Items = [ "unblock me" ] } |})

                    match gateway.Append (StreamId.Session sessionId) None seedTodo with
                    | Committed _ -> ()
                    | _ -> Assert.True(false, "seed Todo commit failed")

                    use driver = new SessionDriver(gateway, sessionId, inbox)

                    let turnId = TurnId.create "msg-flow-cancel"
                    Assert.Equal(Ok(), inbox.TryPost(HumanMessageEvent(turnId, "work")))

                    let! promptSeen =
                        VerticalSliceWaiters._awaitEnvelope
                            gateway
                            (fun env ->
                                match env.Fact with
                                | Fact.Prompt(Fact.PromptFact.PromptRequested _) -> true
                                | _ -> false)
                            1

                    Assert.True(promptSeen, "Expected PromptRequested before cancel")

                    Assert.Equal(Ok(), inbox.TryPost(CancelEvent "test-cancel"))

                    let! cancelTerminalSeen =
                        VerticalSliceWaiters._awaitEnvelope
                            gateway
                            (fun env ->
                                match env.Fact with
                                | Fact.Prompt(Fact.PromptFact.PromptTerminal _) -> true
                                | _ -> false)
                            1

                    Assert.True(cancelTerminalSeen, "Expected PromptTerminal after cancel")

                    let proj = Map.find sessionId gateway.ProjectionSet.SessionProjections
                    Assert.Equal(None, proj.SettledResult)

                    let envelopes = VerticalSliceJournalSupport._readEnvelopes gateway.JournalPath

                    let terminalCount =
                        envelopes
                        |> Array.filter (fun env ->
                            match env.Fact with
                            | Fact.Prompt(Fact.PromptFact.PromptTerminal _) -> true
                            | _ -> false)
                        |> Array.length

                    Assert.True(terminalCount >= 1, "Expected at least one PromptTerminal from cancellation")

                    let! _ = gateway.DisposeAsync()
                    ()
            })
