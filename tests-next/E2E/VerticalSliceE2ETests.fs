namespace Wanxiangshu.Next.Tests.E2E

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
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
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport
open Wanxiangshu.Next.Tests.Integration

module VerticalSliceE2ETests =

    let private runStep2 (gateway: Gateway) (sessionId: SessionId) (tempDir: string) (inbox: ISessionInbox) =
        task {
            let port = SessionInboxCommandPort(inbox)
            let todoTool = StaticTools.todowriteTool port

            let toolCtx: ToolContext =
                { SessionId = sessionId
                  Workspace = tempDir
                  Cancellation = CancellationToken.None
                  Deadline = Deadline.ofBudget DateTimeOffset.UtcNow (TimeSpan.FromSeconds 10.0)
                  Session = port }

            let payload = "[\"implement vertical slice\", \"run tests\"]"
            let toolTask = todoTool.Execute toolCtx { Payload = payload }

            let! inboxEv = inbox.Receive CancellationToken.None

            match inboxEv with
            | SessionCommandEvent(UpsertTodo(snap, reply)) ->
                let commitRes =
                    gateway.Append (StreamId.Session sessionId) None (Fact.Todo(TodoChanged {| Snapshot = snap |}))

                match commitRes with
                | Committed _ -> reply (Ok SessionCommandResult.Upserted)
                | _ -> Assert.True(false, "Expected Committed for TodoChanged")
            | _ -> Assert.True(false, sprintf "Expected SessionCommandEvent, got %A" inboxEv)

            let! toolOutput = toolTask
            Assert.False(toolOutput.Truncated)

            let sessionProj2 = Map.tryFind sessionId gateway.ProjectionSet.SessionProjections
            Assert.True(sessionProj2.IsSome)
            Assert.True(sessionProj2.Value.Todos.IsSome)
            Assert.Equal(2, sessionProj2.Value.Todos.Value.Items.Length)
        }

    [<Fact>]
    let Vertical_slice_end_to_end_closed_loop () =
        withTempDir (fun tempDir ->
            task {
                let! startRes = Gateway.start tempDir CancellationToken.None

                match startRes with
                | Error err -> Assert.True(false, sprintf "Gateway start failed: %A" err)
                | Ok gateway ->
                    let sessionId = SessionId.create "session-e2e-vertical"
                    let inboxMap = Dictionary<SessionId, ISessionInbox>()
                    let inbox = FifoInbox(1000) :> ISessionInbox
                    inboxMap.[sessionId] <- inbox
                    use driver = new SessionDriver(gateway, sessionId, inbox)

                    let! _ = VerticalSliceJournalSupport._runStep1 gateway sessionId inboxMap

                    // Step 1 starts SessionFlows.run which requested ContinueWork prompt.
                    // Deliver terminal outcome to unblock flow:
                    let userMsgId = MessageId.create "msg_user_1"
                    let assistantMsgId = MessageId.create "assistant_e2e_1"
                    Assert.Equal(Ok(), inbox.TryPost(AssistantTerminalEvent(userMsgId, assistantMsgId, PromptOutcome.Delivered assistantMsgId)))

                    do! runStep2 gateway sessionId tempDir inbox

                    let finishFact =
                        Fact.Session(SessionSettled {| Result = SessionResult.Completed "Vertical slice completed" |})

                    let commitFinish = gateway.Append (StreamId.Session sessionId) None finishFact

                    match commitFinish with
                    | Committed _ -> ()
                    | _ -> Assert.True(false, "Expected Committed for SessionSettled")

                    let sessionProj3 = Map.find sessionId gateway.ProjectionSet.SessionProjections
                    Assert.Equal(Some(SessionResult.Completed "Vertical slice completed"), sessionProj3.SettledResult)

                    let! _ = gateway.DisposeAsync()
                    ()
            })
