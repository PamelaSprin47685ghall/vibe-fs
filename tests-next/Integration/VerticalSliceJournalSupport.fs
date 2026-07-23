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
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tools

module VerticalSliceJournalSupport =
    module private NodeFs =
        [<Import("readFileSync", "node:fs")>]
        let readFileSync (path: string, encoding: string) : string = jsNative

    let _readEnvelopes (path: string) =
        (NodeFs.readFileSync (path, "utf-8"))
            .Split([| '\n' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.map (fun line ->
            match Envelope.deserialize line with
            | Ok envelope -> envelope
            | Error error -> Assert.Fail(sprintf "Expected a complete journal line, got: %s" error))

    let _assertSingleHumanTurn (envelopes: Envelope array) =
        Assert.Equal(
            1,
            envelopes
            |> Array.filter (fun envelope ->
                match envelope.Fact with
                | Fact.Runtime(RuntimeStarted _) -> true
                | _ -> false)
            |> Array.length
        )

        Assert.Equal(
            1,
            envelopes
            |> Array.filter (fun envelope ->
                match envelope.Fact with
                | Fact.Session(HumanTurnStarted _) -> true
                | _ -> false)
            |> Array.length
        )

    let _awaitDriverProcessed (inbox: ISessionInbox) =
        task {
            let commandPort = SessionInboxCommandPort(inbox) :> SessionCommandPort

            let deadline =
                Wanxiangshu.Next.Process.Deadline.ofBudget DateTimeOffset.UtcNow (TimeSpan.FromSeconds 10.0)

            let! result = commandPort.Request (QuerySnapshot ignore) CancellationToken.None deadline

            match result with
            | Ok(SessionCommandResult.SnapshotQueried _) -> ()
            | other -> Assert.Fail(sprintf "Expected the driver query barrier to complete, got %A" other)
        }

    let _runStep1
        (gateway: Gateway)
        (sessionId: SessionId)
        (inboxes: System.Collections.Generic.Dictionary<SessionId, ISessionInbox>)
        =
        task {
            let userMsgObj =
                {| id = "msg_user_1"
                   role = "user"
                   sessionID = SessionId.value sessionId
                   parts =
                    [ {| ``type`` = "text"
                         text = "Build feature X" |} ] |}

            let hookInput: OpencodeHookInput =
                { sessionID = SessionId.value sessionId
                  messageID = Some "msg_user_1"
                  agent = Some "coder"
                  model = None }

            OpencodeHooks.handleChatMessage gateway (SessionDrivers()) inboxes hookInput {| message = userMsgObj |}
            do! _awaitDriverProcessed inboxes.[sessionId]

            let sessionProj1 = Map.find sessionId gateway.ProjectionSet.SessionProjections
            Assert.Equal(Some(TurnId.create "msg_user_1"), sessionProj1.HumanTurnId)
        }

    let _runStep2 (gateway: Gateway) (sessionId: SessionId) (tempDir: string) (inbox: ISessionInbox) =
        task {
            let port = SessionInboxCommandPort(inbox)
            let execTool = StaticTools.executorTool ()

            let toolCtx: ToolContext =
                { SessionId = sessionId
                  Workspace = tempDir
                  Cancellation = CancellationToken.None
                  Deadline =
                    Wanxiangshu.Next.Process.Deadline.ofBudget DateTimeOffset.UtcNow (TimeSpan.FromSeconds 10.0)
                  Session = port }

            let payload = "{\"command\":\"echo vertical slice tool test\"}"
            let! toolOutput = execTool.Execute toolCtx { Payload = payload }
            Assert.False(toolOutput.Truncated)
            Assert.Contains("vertical slice tool test", toolOutput.Result)
        }
