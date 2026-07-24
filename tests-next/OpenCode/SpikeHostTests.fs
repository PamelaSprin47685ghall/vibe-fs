namespace Wanxiangshu.Next.Tests.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.OpenCode

module SpikeHostTests =

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

            let opts: OpenCodePromptOptions = { Model = None; Agent = None }
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
    let ``Terminal_completion_notifies_listeners_strictly_once`` () =
        let eventPort = Events.DeterministicEventPort() :> IEventObservationPort
        let sId = SessionId.create "sess-once-test"
        let mutable callCount = 0

        use _sub =
            eventPort.SubscribeTerminalListener(fun _ _ -> callCount <- callCount + 1)

        let msgId = MessageId.create "msg1"
        let firstNotify = eventPort.NotifyTerminal sId (Completed msgId)
        Assert.True(firstNotify)
        Assert.Equal(1, callCount)

        let secondNotify = eventPort.NotifyTerminal sId (Completed msgId)
        Assert.False(secondNotify)
        Assert.Equal(1, callCount)
        Assert.True(eventPort.IsCompleted sId)

    [<Fact>]
    let ``A_version_output_separation_isolates_session_channels`` () =
        task {
            let eventPort = Events.DeterministicEventPort() :> IEventObservationPort
            let sessionPort = InjectedSessionPort(None, eventPort) :> ISessionHostPort
            let sA = SessionId.create "sess-A"
            let sB = SessionId.create "sess-B"

            use _subA = sessionPort.SubscribeTerminal(sA, (fun _ _ -> ()))
            use _subB = sessionPort.SubscribeTerminal(sB, (fun _ _ -> ()))

            let opts: OpenCodePromptOptions = { Model = None; Agent = None }
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
            let childOptions: OpenCodeChildOptions = { Title = Some "Child 1"; Agent = None }

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

    [<Fact>]
    let ``SpikePlugin_initSpikePlugin_exposes_hooks_and_ports`` () =
        task {
            let input = createObj []
            let! hooksObj = SpikePlugin.initSpikePlugin input
            Assert.False(isNull hooksObj)
            Assert.False(isNull hooksObj?projection)
            Assert.False(isNull hooksObj?events)
            Assert.False(isNull hooksObj?sessions)
            Assert.False(isNull hooksObj?``chat.transform``)
        }
