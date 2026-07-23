namespace Wanxiangshu.Next.Tests.Integration

open System
open System.Threading
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Xunit
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tools
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Tests
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport

module VerticalSliceE2ETests =

    [<Fact>]
    let Vertical_slice_full_production_vertical_slice_e2e () =
        withTempDir (fun tempDir ->
            task {
                let! startRes = Gateway.start tempDir CancellationToken.None

                match startRes with
                | Error err -> Assert.True(false, sprintf "Gateway start failed: %A" err)
                | Ok gateway ->
                    let sessionId = SessionId.create "sess-full-vertical"
                    let inbox = FifoInbox(1000) :> ISessionInbox
                    let continuationUserMsgId = MessageId.create "cont-user-msg-1"
                    let fakePort = FakePromptPort(continuationUserMsgId) :> IPromptPort
                    use driver = new SessionDriver(gateway, sessionId, inbox, port = fakePort)

                    let turnId = TurnId.create "human-msg-1"
                    Assert.Equal(Ok(), inbox.TryPost(HumanMessageEvent(turnId, "Build feature Z")))

                    let commandPort = SessionInboxCommandPort(inbox) :> SessionCommandPort
                    let todoTool = StaticTools.todowriteTool ()
                    let toolCtx: ToolContext =
                        { SessionId = sessionId
                          Workspace = tempDir
                          Cancellation = CancellationToken.None
                          Deadline = Wanxiangshu.Next.Process.Deadline.ofBudget DateTimeOffset.UtcNow (TimeSpan.FromSeconds 10.0)
                          Session = commandPort }

                    let! _ = todoTool.Execute toolCtx { Payload = "{\"todos\":[\"task 1\"]}" }

                    let nativeAstId = MessageId.create "native-ast-1"
                    Assert.Equal(
                        Ok(),
                        inbox.TryPost(
                            AssistantTerminalEvent(
                                MessageId.create "human-msg-1",
                                nativeAstId,
                                Fact.PromptOutcome.Delivered nativeAstId
                            )
                        )
                    )

                    let! promptReqSeen =
                        VerticalSliceWaiters._awaitEnvelope
                            gateway
                            (fun env ->
                                match env.Fact with
                                | Fact.Prompt(Fact.PromptFact.PromptRequested _) -> true
                                | _ -> false)
                            1

                    Assert.True(promptReqSeen, "Expected PromptRequested during continuation flow")

                    let! _ = todoTool.Execute toolCtx { Payload = "{\"todos\":[]}" }

                    let contAstId = MessageId.create "cont-ast-1"
                    Assert.Equal(
                        Ok(),
                        inbox.TryPost(
                            AssistantTerminalEvent(
                                continuationUserMsgId,
                                contAstId,
                                Fact.PromptOutcome.Delivered contAstId
                            )
                        )
                    )

                    let! settledSeen =
                        VerticalSliceWaiters._awaitEnvelope
                            gateway
                            (fun env ->
                                match env.Fact with
                                | Fact.Session(Fact.SessionFact.SessionSettled _) -> true
                                | _ -> false)
                            1

                    Assert.True(settledSeen, "Expected SessionSettled after todo cleared")

                    let sessionProj = Map.find sessionId gateway.ProjectionSet.SessionProjections
                    Assert.True(sessionProj.SettledResult.IsSome, "Expected SettledResult to be present")

                    let envelopes = VerticalSliceJournalSupport._readEnvelopes gateway.JournalPath
                    Assert.True(envelopes.Length >= 5, sprintf "Expected >= 5 envelopes, got %d" envelopes.Length)

                    let! _ = gateway.DisposeAsync()
                    ()
            })