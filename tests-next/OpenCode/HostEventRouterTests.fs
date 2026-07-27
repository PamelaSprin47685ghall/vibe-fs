namespace Wanxiangshu.Next.Tests.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tests.EventDrivenHarness
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport

/// Behavioral coverage migrated from the deleted HostEventRouter to the
/// Decide layer (TerminalPolicies) and the sole fallback writer
/// (RetrySignalHandler). No raw-event parsing lives in tests anymore.
module HostEventRouterTests =

    let private recordingPort (prompts: ResizeArray<string * string>) =
        let activeSessions = HashSet<string>()

        { new ISessionHostPort with
            member _.SubscribeTerminal(sessionId, _) =
                let id = SessionId.value sessionId
                activeSessions.Add id |> ignore

                { new IDisposable with
                    member _.Dispose() = activeSessions.Remove id |> ignore }

            member _.SendPrompt(sessionId, text, _) =
                let id = SessionId.value sessionId

                if activeSessions.Contains id then
                    prompts.Add(id, text)
                    Task.FromResult(Ok(MessageId.create "accepted"))
                else
                    Task.FromResult(Error "AG-LISTENER-BEFORE-SEND")

            member _.SendChildPromptFireAndForget(_, _, _, _) = Task.FromResult(Ok())
            member _.AbortSession(_) = Task.FromResult(Ok())
            member _.AbortChildren(_) = Task.FromResult(()) :> Task

            member _.CreateChildSession(_, _) =
                Task.FromResult(Ok(SessionId.create "child"))

            member _.GetSessionOutput(_) = [] }

    let private textPart text =
        createObj [ "id", box "p1"; "type", box "text"; "text", box text ]

    let private applyDecide sessionPort journal git verdict nudgeSent managerGuard parents turn =
        let eventPort = Events.HostEventPort()

        TerminalPolicies.apply
            (sessionPort :> ISessionHostPort)
            (eventPort :> IEventObservationPort)
            journal
            git
            verdict
            nudgeSent
            managerGuard
            parents
            (fun _ -> ())
            turn

    let private completedTurn sessionId role parts =
        { SessionId = SessionId.create sessionId
          UserMessageId = MessageId.create "u1"
          AssistantMessageId = MessageId.create "a1"
          AgentRole = Some role
          Directory = "/tmp/ws"
          Parts = parts
          Finish = Some "stop"
          ErrorName = None
          Model = None
          Outcome = TurnOutcome.TurnCompleted }

    let private fallbackFailures (journal: AgentJournal) sessionId =
        match
            (AgentJournal.snapshot journal)
                .AgentProjections.Sessions.TryFind(SessionId.create sessionId)
        with
        | Some session ->
            session.Fallback
            |> Option.map (fun fb -> fb.TotalFailures)
            |> Option.defaultValue 0
        | None -> 0

    [<Fact>]
    let ``Completed_coder_turn_with_text_part_prevents_zero_width_continuation`` () =
        task {
            let sessionId = "coder-text-session"
            let prompts = ResizeArray<string * string>()
            let sessionPort = recordingPort prompts

            applyDecide
                sessionPort
                None
                None
                (HashSet())
                (HashSet())
                (HashSet())
                (Dictionary())
                (completedTurn sessionId AgentRole.Coder [| textPart "Blogger record" |])

            do! drainMicrotasks 16
            Assert.Empty(prompts)
        }

    [<Fact>]
    let ``Unknown_turn_without_parts_is_not_empty_and_triggers_no_decision`` () =
        // An in-flight assistant message (no finish, no parts) reconciles to
        // TurnUnknown. The Decide layer must treat it as UNKNOWN, not empty:
        // no zero-width continuation and no durable fallback fact.
        task {
            let sessionId = "unknown-session"
            let prompts = ResizeArray<string * string>()
            let sessionPort = recordingPort prompts

            let unknown =
                { SessionId = SessionId.create sessionId
                  UserMessageId = MessageId.create "u1"
                  AssistantMessageId = MessageId.create "a1"
                  AgentRole = Some AgentRole.Coder
                  Directory = "/tmp/ws"
                  Parts = [||]
                  Finish = None
                  ErrorName = None
                  Model = None
                  Outcome = TurnOutcome.TurnUnknown }

            applyDecide sessionPort None None (HashSet()) (HashSet()) (HashSet()) (Dictionary()) unknown
            do! drainMicrotasks 8
            Assert.Empty(prompts)
        }

    [<Fact>]
    let ``Same_user_replay_keeps_assistant_id_for_provider_retry`` () =
        withTempDir (fun directory ->
            task {
                let sessionId = "retry-identity-session"
                use journal =
                    AgentJournal.create directory (RuntimeId.create "retry-identity-runtime") 1 DateTimeOffset.UtcNow

                let recorded = HashSet<string>()
                let userBindings = Dictionary<string, MessageId>()
                userBindings.[sessionId] <- MessageId.create "user-1"

                // Replaying the same user message must not reset the binding
                // before the provider retry lands, so the fallback is attributed
                // to user-1.
                RetrySignalHandler.handle
                    (Some journal)
                    recorded
                    userBindings
                    { SessionId = SessionId.create sessionId
                      Attempt = "1"
                      Reason = "mock provider failure round1"
                      MessageId = None }

                // A retry with the same identity is deduplicated by
                // (session, user, attempt) - no second failure recorded.
                RetrySignalHandler.handle
                    (Some journal)
                    recorded
                    userBindings
                    { SessionId = SessionId.create sessionId
                      Attempt = "1"
                      Reason = "replay"
                      MessageId = None }

                Assert.Equal(1, fallbackFailures journal sessionId)

                // A retry with no current user/assistant identity writes nothing.
                let orphan = Dictionary<string, MessageId>()
                RetrySignalHandler.handle
                    (Some journal)
                    recorded
                    orphan
                    { SessionId = SessionId.create sessionId
                      Attempt = "3"
                      Reason = "no identity"
                      MessageId = None }

                Assert.Equal(1, fallbackFailures journal sessionId)

                // A genuinely new user turn records a distinct fallback.
                userBindings.[sessionId] <- MessageId.create "user-2"
                RetrySignalHandler.handle
                    (Some journal)
                    recorded
                    userBindings
                    { SessionId = SessionId.create sessionId
                      Attempt = "2"
                      Reason = "new user turn"
                      MessageId = None }

                Assert.Equal(2, fallbackFailures journal sessionId)
            })
