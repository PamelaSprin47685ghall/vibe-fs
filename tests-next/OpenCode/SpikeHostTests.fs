namespace Wanxiangshu.Next.Tests.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Xunit
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Tools

module SpikeHostTests =

    [<Fact>]
    let ``Reviewer_verdict_surface_is_enum_and_manager_coder_are_denied`` () =
        let reviewer = StaticTools.reviewerAgentConfig ()
        let manager = StaticTools.managerAgentConfig ()
        let coder = StaticTools.coderAgentConfig ()

        Assert.Equal("allow", unbox<string> reviewer?permission?verdict)
        Assert.Equal("deny", unbox<string> manager?permission?verdict)
        Assert.Equal("deny", unbox<string> coder?permission?verdict)
        Assert.Contains("\"enum\":[\"PERFECT\",\"REVISE\"]", StaticTools.reviewerVerdictSchemaJson)

        match StaticTools.reviewerVerdictOfString "PERFECT" with
        | Ok ReviewGuardVerdict.Perfect -> ()
        | other -> Assert.True(false, sprintf "Expected PERFECT, got %A" other)

        match StaticTools.reviewerVerdictOfString "REVISE" with
        | Ok ReviewGuardVerdict.Revise -> ()
        | other -> Assert.True(false, sprintf "Expected REVISE, got %A" other)

        Assert.True(Result.isError (StaticTools.reviewerVerdictOfString "PERFECT: looks good"))
        Assert.True(Result.isError (StaticTools.reviewerVerdictOfString "perfect"))

    [<Fact>]
    let ``AG_CURRENT_TAIL_PRESERVED_preserves_raw_tail_messages`` () =
        let rawTail1 =
            createObj [ "role", box "user"; "text", box "tail message 1"; "id", box "m1" ]

        let rawTail2 =
            createObj [ "role", box "assistant"; "text", box "tail message 2"; "id", box "m2" ]

        let rawMsgs = [ rawTail1; rawTail2 ]

        let newPrefix = [ createObj [ "role", box "system"; "text", box "system context" ] ]

        let result = Projection.replaceRawPrefix newPrefix 0 rawMsgs

        Assert.Equal(3, List.length result)
        Assert.Equal("system context", unbox<string> (List.head result)?text)
        Assert.Equal("tail message 1", unbox<string> (List.item 1 result)?text)
        Assert.Equal("tail message 2", unbox<string> (List.item 2 result)?text)

    [<Fact>]
    let ``AG_LISTENER_BEFORE_SEND_enforces_listener_registration_order`` () =
        task {
            let eventPort = Events.DeterministicEventPort() :> IEventObservationPort
            let sessionPort = InjectedSessionPort(None, eventPort) :> ISessionHostPort
            let sId = SessionId.create "sess-no-listener"

            let opts: OpenCodePromptOptions =
                { Model = None
                  Agent = None
                  Directory = None }

            let! errRes = sessionPort.SendPrompt(sId, "hello without listener", opts)

            match errRes with
            | Error err -> Assert.Contains("AG-LISTENER-BEFORE-SEND", err)
            | Ok _ -> Assert.True(false, "Expected error when sending prompt without listener")

            use _sub = sessionPort.SubscribeTerminal(sId, (fun _ _ -> ()))
            let! okRes = sessionPort.SendPrompt(sId, "hello with listener", opts)

            match okRes with
            | Ok _ -> ()
            | Error err -> Assert.True(false, sprintf "Expected success when listener is subscribed, got %s" err)
        }

    [<Fact>]
    let ``Terminal_completion_notifies_each_run_listener`` () =
        let eventPort = Events.DeterministicEventPort() :> IEventObservationPort
        let sId = SessionId.create "sess-once-test"
        let mutable callCount = 0

        use _sub =
            eventPort.SubscribeTerminalListener(fun _ _ -> callCount <- callCount + 1)

        let msgId = MessageId.create "msg1"

        let fakeResult =
            { SessionId = sId
              RootUserMessageId = msgId
              AssistantMessageId = msgId
              Role = "test"
              Directory = ""
              FinalText = "done"
              Parts = [||] }

        let firstNotify =
            eventPort.NotifyTerminal sId (TerminalOutcome.Completed fakeResult)

        Assert.True(firstNotify)
        Assert.Equal(1, callCount)

        let fakeResult2 =
            { SessionId = sId
              RootUserMessageId = msgId
              AssistantMessageId = msgId
              Role = "test"
              Directory = ""
              FinalText = "done"
              Parts = [||] }

        let secondNotify =
            eventPort.NotifyTerminal sId (TerminalOutcome.Completed fakeResult2)

        Assert.True(secondNotify)
        Assert.Equal(2, callCount)
        Assert.False(eventPort.IsCompleted sId)

    [<Fact>]
    let ``HostEventPort_notify_terminal_raises_idle_and_abort_listeners`` () =
        let eventPort = Events.HostEventPort()
        let mutable observed = []

        use _sub =
            (eventPort :> IEventObservationPort)
                .SubscribeTerminalListener(fun sessionId outcome ->
                    observed <- (SessionId.value sessionId, outcome) :: observed)

        let idleSid = SessionId.create "idle-session"
        let idleMid = MessageId.create "m-idle"

        let idleResult =
            { SessionId = idleSid
              RootUserMessageId = idleMid
              AssistantMessageId = idleMid
              Role = "test"
              Directory = ""
              FinalText = "done"
              Parts = [||] }

        (eventPort :> IEventObservationPort).NotifyTerminal idleSid (TerminalOutcome.Completed idleResult)
        |> ignore

        (eventPort :> IEventObservationPort).NotifyTerminal (SessionId.create "abort-session") (Aborted "stopped")
        |> ignore

        Assert.Equal(2, List.length observed)

        Assert.True(
            observed
            |> List.exists (fun (sessionId, outcome) ->
                sessionId = "idle-session"
                && match outcome with
                   | Completed _ -> true
                   | _ -> false)
        )

        Assert.True(
            observed
            |> List.exists (fun (sessionId, outcome) ->
                sessionId = "abort-session"
                && match outcome with
                   | Aborted _ -> true
                   | _ -> false)
        )

    [<Fact>]
    let ``HostEventPort_records_assistant_text_output`` () =
        let eventPort = Events.HostEventPort()

        let output sessionId =
            (eventPort :> IEventObservationPort)
                .GetSessionOutput(SessionId.create sessionId)

        eventPort.RecordSessionOutput (SessionId.create "message-parts") "from message"
        eventPort.RecordSessionOutput (SessionId.create "direct-parts") "from parts"
        eventPort.RecordSessionOutput (SessionId.create "direct-text") "from text"

        Assert.Equal([ "from message" ], output "message-parts")
        Assert.Equal([ "from parts" ], output "direct-parts")
        Assert.Equal([ "from text" ], output "direct-text")

    [<Fact>]
    let ``Underlying_prompt_acceptance_does_not_complete_synchronously`` () =
        task {
            let acceptance = TaskCompletionSource<SendOutcome>()

            let underlying =
                { new IOpenCodePort with
                    member _.SendPrompt _ _ _ = acceptance.Task
                    member _.AbortSession _ = Task.FromResult(Ok())

                    member _.CreateChildSession _ _ =
                        Task.FromResult(Ok(SessionId.create "child"))

                    member _.CloseChildSession _ = Task.FromResult(Ok()) }

            let eventPort = Events.DeterministicEventPort() :> IEventObservationPort

            let sessionPort =
                InjectedSessionPort(Some(underlying), eventPort) :> ISessionHostPort

            let sessionId = SessionId.create "underlying-session"
            let mutable terminalObserved = false

            use _sub =
                sessionPort.SubscribeTerminal(sessionId, (fun _ _ -> terminalObserved <- true))

            let opts: OpenCodePromptOptions =
                { Model = None
                  Agent = None
                  Directory = None }

            let sendTask = sessionPort.SendPrompt(sessionId, "pending", opts)

            Assert.False(terminalObserved)
            acceptance.SetResult(Delivered(MessageId.create "accepted"))

            let! result = sendTask

            match result with
            | Ok messageId -> Assert.Equal("accepted", MessageId.value messageId)
            | Error err -> Assert.True(false, sprintf "Expected accepted prompt, got %s" err)
        }

    [<Fact>]
    let ``A_version_output_separation_isolates_session_channels`` () =
        task {
            let eventPort = Events.DeterministicEventPort() :> IEventObservationPort
            let sessionPort = InjectedSessionPort(None, eventPort) :> ISessionHostPort
            let sA = SessionId.create "sess-A"
            let sB = SessionId.create "sess-B"

            use _subA = sessionPort.SubscribeTerminal(sA, (fun _ _ -> ()))
            use _subB = sessionPort.SubscribeTerminal(sB, (fun _ _ -> ()))

            let opts: OpenCodePromptOptions =
                { Model = None
                  Agent = None
                  Directory = None }

            let! _ = sessionPort.SendPrompt(sA, "Prompt for A", opts)
            let! _ = sessionPort.SendPrompt(sB, "Prompt for B", opts)

            let outA = sessionPort.GetSessionOutput(sA)
            let outB = sessionPort.GetSessionOutput(sB)

            Assert.Single(outA)
            Assert.Equal("Prompt: Prompt for A", List.head outA)

            Assert.Single(outB)
            Assert.Equal("Prompt: Prompt for B", List.head outB)
        }

    [<Fact>]
    let ``Parent_cancellation_aborts_all_child_sessions_and_cleans_up`` () =
        task {
            let eventPort = Events.DeterministicEventPort() :> IEventObservationPort
            let sessionPort = InjectedSessionPort(None, eventPort) :> ISessionHostPort
            let parentId = SessionId.create "parent-sess"

            let mutable childTerminalCount = 0

            let childOptions: OpenCodeChildOptions =
                { Title = Some "Child 1"
                  Agent = None
                  Directory = None }

            let! childIdRes = sessionPort.CreateChildSession(parentId, childOptions)

            let childId =
                match childIdRes with
                | Ok cId -> cId
                | Error err -> failwith err

            use _subChild =
                sessionPort.SubscribeTerminal(
                    childId,
                    fun _ outcome ->
                        match outcome with
                        | Aborted _ -> childTerminalCount <- childTerminalCount + 1
                        | _ -> ()
                )

            let! _ = sessionPort.AbortSession(parentId)

            Assert.Equal(1, childTerminalCount)
            let childOut = sessionPort.GetSessionOutput(childId)
            Assert.True(childOut |> List.exists (fun line -> line.Contains("Aborted")))
        }
