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

type private RecordingPromptPort(continuationMsgId: MessageId, sendCount: ref<int>) =
    interface IPromptPort with
        member _.SendPrompt (_sessionId: SessionId) (_text: string) (_opts: PromptOptions) =
            Interlocked.Increment(sendCount) |> ignore
            Task.FromResult(SendOutcome.Delivered continuationMsgId)

module TodoVerticalSliceE2ETests =

    [<Fact>]
    let Todo_vertical_slice_real_e2e_flow () =
        withTempDir (fun tempDir ->
            task {
                let! startRes = Gateway.start tempDir CancellationToken.None

                match startRes with
                | Error err -> Assert.True(false, sprintf "Gateway start failed: %A" err)
                | Ok gateway ->
                    let sessionId = SessionId.create "sess-todo-vertical-real"
                    let inbox = FifoInbox(1000) :> ISessionInbox
                    let continuationUserMsgId = MessageId.create "cont-user-msg-todo"
                    let sendCountRef = ref 0
                    let port = RecordingPromptPort(continuationUserMsgId, sendCountRef) :> IPromptPort
                    use driver = new SessionDriver(gateway, sessionId, inbox, port = port)

                    let humanTurnId = TurnId.create "human-msg-todo-1"
                    Assert.Equal(Ok(), inbox.TryPost(HumanMessageEvent(humanTurnId, "Implement vertical slice task")))

                    let commandPort = SessionInboxCommandPort(inbox) :> SessionCommandPort
                    let todoTool = StaticTools.todowriteTool ()
                    let toolCtx: ToolContext =
                        { SessionId = sessionId
                          Workspace = tempDir
                          Cancellation = CancellationToken.None
                          Deadline = Wanxiangshu.Next.Process.Deadline.ofBudget DateTimeOffset.UtcNow (TimeSpan.FromSeconds 10.0)
                          Session = commandPort }

                    let! _ = todoTool.Execute toolCtx { Payload = "{\"todos\":[\"task 1 pending\"]}" }

                    let nativeAstId = MessageId.create "native-ast-todo-1"
                    Assert.Equal(
                        Ok(),
                        inbox.TryPost(
                            AssistantTerminalEvent(
                                MessageId.create "human-msg-todo-1",
                                nativeAstId,
                                Fact.PromptOutcome.Delivered nativeAstId
                            )
                        )
                    )

                    let! promptReqSeen =
                        VerticalSliceWaitTestSupport._awaitEnvelope
                            gateway
                            (fun env ->
                                match env.Fact with
                                | Fact.Prompt(Fact.PromptFact.PromptRequested _) -> true
                                | _ -> false)
                            1

                    Assert.True(promptReqSeen, "Expected PromptRequested during continuation flow")
                    Assert.Equal(1, sendCountRef.Value)

                    let! _ = todoTool.Execute toolCtx { Payload = "{\"todos\":[]}" }

                    let contAstId = MessageId.create "cont-ast-todo-1"
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
                        VerticalSliceWaitTestSupport._awaitEnvelope
                            gateway
                            (fun env ->
                                match env.Fact with
                                | Fact.Session(Fact.SessionFact.SessionSettled _) -> true
                                | _ -> false)
                            1

                    Assert.True(settledSeen, "Expected SessionSettled after todo cleared")
                    Assert.Equal(1, sendCountRef.Value)

                    let sessionProj = Map.find sessionId gateway.ProjectionSet.SessionProjections
                    Assert.True(sessionProj.SettledResult.IsSome, "Expected SettledResult to be present")

                    let envelopes = VerticalSliceJournalTestSupport._readEnvelopes gateway.JournalPath
                    Assert.True(envelopes.Length >= 6, sprintf "Expected >= 6 envelopes, got %d" envelopes.Length)

                    let indexOfFact predicate =
                        envelopes |> Array.findIndex (fun env -> predicate env.Fact)

                    let idxRuntimeStarted = indexOfFact (function Fact.Runtime(Fact.RuntimeStarted _) -> true | _ -> false)
                    let idxHumanTurnStarted = indexOfFact (function Fact.Session(Fact.HumanTurnStarted _) -> true | _ -> false)
                    let idxPromptRequested = indexOfFact (function Fact.Prompt(Fact.PromptFact.PromptRequested _) -> true | _ -> false)
                    let idxPromptTerminal = indexOfFact (function Fact.Prompt(Fact.PromptFact.PromptTerminal _) -> true | _ -> false)
                    let idxSessionSettled = indexOfFact (function Fact.Session(Fact.SessionFact.SessionSettled _) -> true | _ -> false)

                    Assert.True(idxRuntimeStarted < idxHumanTurnStarted, "Fact ordering: RuntimeStarted < HumanTurnStarted")
                    Assert.True(idxHumanTurnStarted < idxPromptRequested, "Fact ordering: HumanTurnStarted < PromptRequested")
                    Assert.True(idxPromptRequested < idxPromptTerminal, "Fact ordering: PromptRequested < PromptTerminal")
                    Assert.True(idxPromptTerminal < idxSessionSettled, "Fact ordering: PromptTerminal < SessionSettled")

                    let! _ = gateway.DisposeAsync()
                    ()
            })
