namespace Wanxiangshu.Next.Tests.Integration

open System
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

module VerticalSliceIntegrationTests =

    let private runStep1 (gateway: Gateway) (sessionId: SessionId) =
        let userMsgObj =
            {| id = "msg_user_1"
               role = "user"
               sessionID = "session-integration-vertical"
               parts =
                [ {| ``type`` = "text"
                     text = "Build feature X" |} ] |}

        let hookInput: OpencodeHookInput =
            { sessionID = "session-integration-vertical"
              messageID = Some "msg_user_1"
              agent = Some "coder"
              model = None }

        let drivers = SessionDrivers()
        OpencodeHooks.handleChatMessage gateway drivers hookInput {| message = userMsgObj |}
        let sessionProj1 = Map.find sessionId gateway.ProjectionSet.SessionProjections
        Assert.Equal(Some(TurnId.create "msg_user_1"), sessionProj1.HumanTurnId)

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
            let! toolOutput = todoTool.Execute toolCtx { Payload = payload }
            Assert.False(toolOutput.Truncated)

            let! inboxEv = inbox.Receive CancellationToken.None

            match inboxEv with
            | SessionCommandEvent(UpsertTodo snap) ->
                let commitRes =
                    gateway.Append (StreamId.Session sessionId) None (Fact.Todo(TodoChanged {| Snapshot = snap |}))

                match commitRes with
                | Committed _ -> ()
                | _ -> Assert.True(false, "Expected Committed for TodoChanged")
            | _ -> Assert.True(false, sprintf "Expected SessionCommandEvent, got %A" inboxEv)

            let sessionProj2 = Map.find sessionId gateway.ProjectionSet.SessionProjections
            Assert.True(sessionProj2.Todos.IsSome)
            Assert.Equal(2, sessionProj2.Todos.Value.Items.Length)
        }

    [<Fact>]
    let Vertical_slice_integration_closed_loop () =
        withTempDir (fun tempDir ->
            task {
                let! startRes = Gateway.start tempDir CancellationToken.None

                match startRes with
                | Error err -> Assert.True(false, sprintf "Gateway start failed: %A" err)
                | Ok gateway ->
                    let sessionId = SessionId.create "session-integration-vertical"
                    let inbox = Plugin.getOrCreateInbox sessionId
                    use driver = new SessionDriver(gateway, sessionId, inbox)

                    runStep1 gateway sessionId
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
