namespace Wanxiangshu.Next.Tests.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Xunit
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Session

module EventsTests =

    [<Fact>]
    let ``HostEventPort_records_assistant_output`` () =
        let eventPort = Events.HostEventPort()
        let sessionId = SessionId.create "event-output-session"

        eventPort.RecordSessionOutput sessionId "assistant answer"

        let observation = eventPort :> IEventObservationPort
        Assert.equal([ "assistant answer" ], observation.GetSessionOutput sessionId)

    [<Fact>]
    let ``TerminalPolicies_accumulates_session_wide_A_across_completed_turns`` () =
        let eventPort = Events.HostEventPort()
        let observation = eventPort :> IEventObservationPort
        let sessionId = SessionId.create "session-wide-a"

        let completed parts messageId =
            { SessionId = sessionId
              UserMessageId = MessageId.create "u1"
              RootUserMessageId = MessageId.create "u1"
              AssistantMessageId = MessageId.create messageId
              AgentRole = Some AgentRole.Coder
              Directory = "/tmp/ws"
              Parts = parts
              Finish = Some "stop"
              ErrorName = None
              Model = None
              Outcome = TurnOutcome.TurnCompleted }

        let sessionPort =
            { new ISessionHostPort with
                member _.SubscribeTerminal(_, _) =
                    { new System.IDisposable with
                        member _.Dispose() = () }

                member _.SendPrompt(_, _, _) =
                    System.Threading.Tasks.Task.FromResult(Ok(MessageId.create "m"))

                member _.SendChildPromptFireAndForget(_, _, _, _) =
                    System.Threading.Tasks.Task.FromResult(Ok())

                member _.AbortSession(_) = System.Threading.Tasks.Task.FromResult(Ok())
                member _.AbortChildren(_) =
                    System.Threading.Tasks.Task.FromResult(()) :> System.Threading.Tasks.Task

                member _.CreateChildSession(_, _) =
                    System.Threading.Tasks.Task.FromResult(Ok(SessionId.create "child"))

                member _.GetSessionOutput(id) = observation.GetSessionOutput id }

        let outcomes = ResizeArray<TerminalOutcome>()

        use _ =
            observation.SubscribeTerminalListener(fun sid outcome ->
                if sid = sessionId then
                    outcomes.Add outcome)

        TerminalPolicies.apply
            sessionPort
            observation
            None
            None
            (System.Collections.Generic.HashSet<string>())
            (System.Collections.Generic.HashSet<string>())
            (System.Collections.Generic.HashSet<string>())
            (System.Collections.Generic.Dictionary<string, string>())
            (fun _ -> ())
            (System.Collections.Generic.HashSet<string>())
            (completed [| createObj [ "type", box "text"; "text", box "first formal paragraph" ] |] "a1")

        TerminalPolicies.apply
            sessionPort
            observation
            None
            None
            (System.Collections.Generic.HashSet<string>())
            (System.Collections.Generic.HashSet<string>())
            (System.Collections.Generic.HashSet<string>())
            (System.Collections.Generic.Dictionary<string, string>())
            (fun _ -> ())
            (System.Collections.Generic.HashSet<string>())
            (completed
                [| createObj [ "type", box "reasoning"; "text", box "second turn reasoning" ]
                   createObj [ "type", box "text"; "text", box "second formal paragraph" ] |]
                "a2")

        Assert.Equal(
            [ "first formal paragraph"; "second turn reasoning\n\nsecond formal paragraph" ],
            observation.GetSessionOutput sessionId
        )

        match outcomes.[outcomes.Count - 1] with
        | TerminalOutcome.Completed result ->
            Assert.Equal(
                "first formal paragraph\n\nsecond turn reasoning\n\nsecond formal paragraph",
                result.FinalText
            )
        | other -> Assert.True(false, sprintf "expected Completed, got %A" other)
