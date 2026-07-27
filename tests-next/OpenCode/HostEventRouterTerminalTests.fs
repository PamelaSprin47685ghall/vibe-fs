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

/// Terminal (Decide) layer coverage migrated from the deleted HostEventRouter.
/// These assertions now run directly against TerminalPolicies.apply with a
/// constructed ReconciledTurn, the same path production wires via the
/// SessionReconciler.
module HostEventRouterTerminalTests =

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

    let private abortedTurn sessionId role =
        { SessionId = SessionId.create sessionId
          UserMessageId = MessageId.create "u1"
          AssistantMessageId = MessageId.create "a1"
          AgentRole = Some role
          Directory = "/tmp/ws"
          Parts = [||]
          Finish = Some "error"
          ErrorName = Some "MessageAbortedError"
          Model = None
          Outcome = TurnOutcome.TurnAborted "aborted" }

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
    let ``Successful_terminal_then_shutdown_idle_records_no_fallback`` () =
        withTempDir (fun directory ->
            task {
                let sessionId = "shutdown-idle-session"
                let prompts = ResizeArray<string * string>()
                let sessionPort = recordingPort prompts
                use journal =
                    AgentJournal.create directory (RuntimeId.create "shutdown-idle-runtime") 1 DateTimeOffset.UtcNow

                // Completed reviewer terminal -> exactly one reviewer missing-verdict nudge.
                applyDecide
                    sessionPort
                    (Some journal)
                    None
                    (HashSet())
                    (HashSet())
                    (HashSet())
                    (Dictionary())
                    (completedTurn sessionId AgentRole.Reviewer [| textPart "reviewed tree" |])

                do! drainMicrotasks 16
                Assert.Single(prompts) |> ignore

                // A later aborted assistant (shutdown mid-turn) must not poison
                // fallback and must not add a second prompt.
                applyDecide
                    sessionPort
                    (Some journal)
                    None
                    (HashSet())
                    (HashSet())
                    (HashSet())
                    (Dictionary())
                    (abortedTurn sessionId AgentRole.Reviewer)

                do! drainMicrotasks 16
                Assert.Single(prompts) |> ignore
                Assert.Equal(0, fallbackFailures journal sessionId)
            })

    [<Fact>]
    let ``Abort_and_completed_turns_do_not_poison_fallback`` () =
        withTempDir (fun directory ->
            task {
                let sessionId = "abort-order-session"
                let prompts = ResizeArray<string * string>()
                let sessionPort = recordingPort prompts
                use journal =
                    AgentJournal.create directory (RuntimeId.create "abort-order-runtime") 1 DateTimeOffset.UtcNow

                // Aborted turn alone: no prompt, no fallback.
                applyDecide
                    sessionPort
                    (Some journal)
                    None
                    (HashSet())
                    (HashSet())
                    (HashSet())
                    (Dictionary())
                    (abortedTurn sessionId AgentRole.Coder)

                do! drainMicrotasks 16
                Assert.Empty(prompts)
                Assert.Equal(0, fallbackFailures journal sessionId)

                // A completed coder terminal with text: no zero-width, no fallback.
                applyDecide
                    sessionPort
                    (Some journal)
                    None
                    (HashSet())
                    (HashSet())
                    (HashSet())
                    (Dictionary())
                    (completedTurn sessionId AgentRole.Coder [| textPart "done" |])

                do! drainMicrotasks 16
                Assert.Empty(prompts)
                Assert.Equal(0, fallbackFailures journal sessionId)
            })

    [<Fact>]
    let ``Manager_terminal_without_current_review_receives_durable_guard_prompt`` () =
        withTempDir (fun directory ->
            task {
                let sessionId = "manager-guard-session"
                let prompts = ResizeArray<string * string>()
                let sessionPort = recordingPort prompts
                use journal =
                    AgentJournal.create directory (RuntimeId.create "manager-guard-runtime") 1 DateTimeOffset.UtcNow

                let gitTreePort = { GetTreeHash = fun () -> "tree-without-review" }

                applyDecide
                    sessionPort
                    (Some journal)
                    (Some gitTreePort)
                    (HashSet())
                    (HashSet())
                    (HashSet())
                    (Dictionary())
                    (completedTurn sessionId AgentRole.Manager [| textPart "manager done" |])

                do! drainMicrotasks 16
                Assert.Single(prompts) |> ignore
                Assert.Contains("Review is required before completion.", snd prompts.[0])
            })

    [<Fact>]
    let ``Terminal_empty_text_part_receives_one_zero_width_continuation`` () =
        task {
            let sessionId = "coder-session"
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
                (completedTurn sessionId AgentRole.Coder [| textPart "" |])

            do! drainMicrotasks 16
            Assert.Single(prompts) |> ignore
            Assert.Equal("\u200B", snd prompts.[0])
        }
