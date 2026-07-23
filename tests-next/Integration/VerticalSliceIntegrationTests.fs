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
                    let inbox = Plugin.getOrCreateInbox sessionId
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
