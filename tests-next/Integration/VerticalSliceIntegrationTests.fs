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

module VerticalSliceIntegrationTests =

    [<Fact>]
    let Vertical_slice_integration_closed_loop () =
        withTempDir (fun tempDir ->
            task {
                let! startRes = Gateway.start tempDir CancellationToken.None

                match startRes with
                | Error err -> Assert.True(false, sprintf "Gateway start failed: %A" err)
                | Ok gateway ->
                    let sessionId = SessionId.create "session-integration-vertical"
                    let inbox = FifoInbox(1000) :> ISessionInbox
                    use driver = new SessionDriver(gateway, sessionId, inbox)
                    let inboxes = System.Collections.Generic.Dictionary<SessionId, ISessionInbox>()
                    inboxes.[sessionId] <- inbox

                    do! VerticalSliceJournalSupport._runStep1 gateway sessionId inboxes
                    do! VerticalSliceJournalSupport._runStep2 gateway sessionId tempDir inbox

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

    [<Fact>]
    let ``SessionDriver_commits_human_turns_without_cross_session_projection_leakage`` () =
        withTempDir (fun tempDir ->
            task {
                let! startRes = Gateway.start tempDir CancellationToken.None

                match startRes with
                | Error err -> Assert.True(false, sprintf "Gateway start failed: %A" err)
                | Ok gateway ->
                    let sessionA = SessionId.create "session-human-a"
                    let sessionB = SessionId.create "session-human-b"
                    let inboxA = FifoInbox(10) :> ISessionInbox
                    let inboxB = FifoInbox(10) :> ISessionInbox
                    use driverA = new SessionDriver(gateway, sessionA, inboxA)
                    use driverB = new SessionDriver(gateway, sessionB, inboxB)

                    let turnA = TurnId.create "msg-human-a"
                    let turnB = TurnId.create "msg-human-b"

                    Assert.Equal(Ok(), inboxA.TryPost(HumanMessageEvent(turnA, "work for session A")))
                    Assert.Equal(Ok(), inboxB.TryPost(HumanMessageEvent(turnB, "work for session B")))

                    do! VerticalSliceJournalSupport._awaitDriverProcessed inboxA
                    do! VerticalSliceJournalSupport._awaitDriverProcessed inboxB

                    let projections = gateway.ProjectionSet.SessionProjections
                    let projectionA = Map.tryFind sessionA projections
                    let projectionB = Map.tryFind sessionB projections

                    Assert.True(projectionA.IsSome)
                    Assert.True(projectionB.IsSome)
                    Assert.Equal(Some turnA, projectionA.Value.HumanTurnId)
                    Assert.Equal(Some turnB, projectionB.Value.HumanTurnId)
                    Assert.False(projectionA.Value.HumanTurnId = Some turnB)
                    Assert.False(projectionB.Value.HumanTurnId = Some turnA)
                    Assert.Equal(2, Map.count projections)

                    let! _ = gateway.DisposeAsync()
                    ()
             })

    [<Fact>]
    let ``OpenCode_human_hook_commits_one_turn_fact`` () =
        withTempDir (fun tempDir ->
            task {
                let! startRes = Gateway.start tempDir CancellationToken.None

                match startRes with
                | Error err -> Assert.True(false, sprintf "Gateway start failed: %A" err)
                | Ok gateway ->
                    let sessionId = SessionId.create "session-human-hook"
                    let inbox = FifoInbox(10) :> ISessionInbox
                    use driver = new SessionDriver(gateway, sessionId, inbox)
                    let inboxes = System.Collections.Generic.Dictionary<SessionId, ISessionInbox>()
                    inboxes.[sessionId] <- inbox

                    let userMessage =
                        {| id = "msg-human-hook"
                           role = "user"
                           sessionID = "session-human-hook"
                           parts = [ {| ``type`` = "text"; text = "continue" |} ] |}

                    let hookInput: OpencodeHookInput =
                        { sessionID = "session-human-hook"
                          messageID = Some "msg-human-hook"
                          agent = Some "coder"
                          model = None }

                    OpencodeHooks.handleChatMessage gateway (SessionDrivers()) inboxes hookInput {| message = userMessage |}
                    do! VerticalSliceJournalSupport._awaitDriverProcessed inbox

                    let envelopes = VerticalSliceJournalSupport._readEnvelopes gateway.JournalPath
                    VerticalSliceJournalSupport._assertSingleHumanTurn envelopes
                    let! _ = gateway.DisposeAsync()
                    ()
            })

    [<Fact>]
    let ``OpenCode_human_message_is_committed_exactly_once`` () =
        withTempDir (fun tempDir ->
            task {
                let! startRes = Gateway.start tempDir CancellationToken.None

                match startRes with
                | Error err -> Assert.True(false, sprintf "Gateway start failed: %A" err)
                | Ok gateway ->
                    let sessionId = SessionId.create "session-human-once"
                    let inbox = FifoInbox(10) :> ISessionInbox
                    let drivers = SessionDrivers()
                    let inboxes = System.Collections.Generic.Dictionary<SessionId, ISessionInbox>()
                    inboxes.[sessionId] <- inbox

                    use driver = new SessionDriver(gateway, sessionId, inbox)

                    let userMsgObj =
                        {| id = "msg-human-once"
                           role = "user"
                           sessionID = "session-human-once"
                           parts =
                            [ {| ``type`` = "text"
                                 text = "Work once" |} ] |}

                    let hookInput: OpencodeHookInput =
                        { sessionID = "session-human-once"
                          messageID = Some "msg-human-once"
                          agent = Some "coder"
                          model = None }

                    OpencodeHooks.handleChatMessage gateway drivers inboxes hookInput {| message = userMsgObj |}
                    do! VerticalSliceJournalSupport._awaitDriverProcessed inbox

                    let envelopes = VerticalSliceJournalSupport._readEnvelopes gateway.JournalPath
                    VerticalSliceJournalSupport._assertSingleHumanTurn envelopes

                    let! _ = gateway.DisposeAsync()
                    ()
            })

    [<Fact>]
    let Vertical_slice_integration_step2_closed_loop () =
        withTempDir (fun tempDir ->
            task {
                let! startRes = Gateway.start tempDir CancellationToken.None

                match startRes with
                | Error err -> Assert.True(false, sprintf "Gateway start failed: %A" err)
                | Ok gateway ->
                    let sessionId = SessionId.create "session-demoted-vertical"
                    let inboxMap = System.Collections.Generic.Dictionary<SessionId, ISessionInbox>()
                    let inbox = FifoInbox(1000) :> ISessionInbox
                    inboxMap.[sessionId] <- inbox
                    use driver = new SessionDriver(gateway, sessionId, inbox)

                    let! _ = VerticalSliceJournalSupport._runStep1 gateway sessionId inboxMap
                    do! VerticalSliceJournalSupport._runStep2 gateway sessionId tempDir inbox

                    let userMsgId = MessageId.create "msg_user_1"
                    let assistantMsgId = MessageId.create "assistant_e2e_1"
                    Assert.Equal(Ok(), inbox.TryPost(AssistantTerminalEvent(userMsgId, assistantMsgId, PromptOutcome.Delivered assistantMsgId)))

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

    [<Fact>]
    let Opencode_plugin_gateway_integration_flow () =
        withTempDir (fun tempDir ->
            task {
                let initArg = createObj [ "directory", box tempDir ]
                let! hooksObj = Plugin.initPlugin initArg
                Assert.False(isNull hooksObj)

                let sessionId = SessionId.create "sess-e2e-flow"
                let getInbox = unbox<SessionId -> ISessionInbox> hooksObj?getOrCreateInbox
                let inbox = getInbox sessionId

                let eventFn = unbox<obj -> unit> hooksObj?event
                let cmdFn = unbox<obj -> unit> hooksObj?command

                let cmdArg = createObj [ "name", box "loop"; "sessionID", box "sess-e2e-flow"; "arguments", box "task loop" ]
                cmdFn cmdArg

                let! ev1 = inbox.Receive CancellationToken.None

                match ev1 with
                | LoopCommandEvent(sId, text) ->
                    Assert.Equal("sess-e2e-flow", SessionId.value sId)
                    Assert.Equal("task loop", text)
                | other -> Assert.True(false, sprintf "Expected LoopCommandEvent, got %A" other)

                let evIdle = createObj [ "type", box "session.idle"; "properties", box (createObj [ "sessionID", box "sess-e2e-flow" ]) ]
                eventFn evIdle

                let! ev2 = inbox.Receive CancellationToken.None

                match ev2 with
                | LifecycleEvent kind -> Assert.Equal("session.idle", kind)
                | other -> Assert.True(false, sprintf "Expected session.idle, got %A" other)

                let disposeFn = unbox<unit -> unit> hooksObj?dispose
                disposeFn ()
            })
