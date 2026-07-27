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

/// SessionDead gate at the Decide (TerminalPolicies) path: a dead session must
/// not receive a ReviewGuard guard prompt or a zero-width continuation nudge.
/// A non-dead session (fewer than 4 failures) must still receive its guard
/// prompt (no over-blocking).
module HostEventRouterSessionDeadTests =

    let private recordingPort (prompts: ResizeArray<string * string>) =
        let activeSessions = (HashSet<string>())

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
            (HashSet<string>())
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

    let private recordFailures (journal: AgentJournal) (sessionId: string) (n: int) =
        for i in 1..n do
            AgentJournal.appendAgent
                (StreamId.Session(SessionId.create sessionId))
                None
                (AgentFact.FallbackFailureRecorded
                    {| SessionId = SessionId.create sessionId
                       Reason = sprintf "f%d" i
                       AssistantMessageId = sprintf "m%d" i
                       ProviderAttempt = sprintf "pa%d" i |})
                journal
            |> ignore

    [<Fact>]
    let ``Dead_manager_session_receives_no_guard_prompt_at_terminal`` () =
        withTempDir (fun d ->
            task {
                let sid = "dead-mgr"
                let prompts = ResizeArray<string * string>()
                let sessionPort = recordingPort prompts
                use j = AgentJournal.create d (RuntimeId.create "r-dm") 1 DateTimeOffset.UtcNow
                recordFailures j sid 4

                applyDecide
                    sessionPort
                    (Some j)
                    (Some { GetTreeHash = fun () -> "tree-nr" })
                    (HashSet())
                    (HashSet())
                    (HashSet())
                    (Dictionary())
                    (completedTurn sid AgentRole.Manager [| textPart "manager done" |])

                do! drainMicrotasks 16
                Assert.Empty(prompts)
            })

    [<Fact>]
    let ``Dead_session_receives_no_zero_width_continuation_at_terminal`` () =
        withTempDir (fun d ->
            task {
                let sid = "dead-cont"
                let prompts = ResizeArray<string * string>()
                let sessionPort = recordingPort prompts
                use j = AgentJournal.create d (RuntimeId.create "r-dc") 1 DateTimeOffset.UtcNow
                recordFailures j sid 4

                applyDecide
                    sessionPort
                    (Some j)
                    None
                    (HashSet())
                    (HashSet())
                    (HashSet())
                    (Dictionary())
                    (completedTurn sid AgentRole.Coder [| textPart "" |])

                do! drainMicrotasks 16
                Assert.Empty(prompts)
            })

    [<Fact>]
    let ``Non_dead_manager_with_prior_failures_still_receives_guard`` () =
        withTempDir (fun d ->
            task {
                let sid = "non-dead-mgr"
                let prompts = ResizeArray<string * string>()
                let sessionPort = recordingPort prompts
                use j = AgentJournal.create d (RuntimeId.create "r-ndm") 1 DateTimeOffset.UtcNow
                recordFailures j sid 3

                applyDecide
                    sessionPort
                    (Some j)
                    (Some { GetTreeHash = fun () -> "tree-nr" })
                    (HashSet())
                    (HashSet())
                    (HashSet())
                    (Dictionary())
                    (completedTurn sid AgentRole.Manager [| textPart "manager done" |])

                do! drainMicrotasks 16
                Assert.Single(prompts) |> ignore
                Assert.Contains("Review is required before completion.", snd prompts.[0])
            })

    [<Fact>]
    let ``Aborted_manager_terminal_never_receives_review_or_continuation_nudge`` () =
        task {
            let sessionId = "aborted-manager-session"
            let prompts = ResizeArray<string * string>()
            let sessionPort = recordingPort prompts

            applyDecide
                sessionPort
                None
                (Some { GetTreeHash = fun () -> "tree-after-abort" })
                (HashSet())
                (HashSet())
                (HashSet())
                (Dictionary())
                (abortedTurn sessionId AgentRole.Manager)

            Assert.Empty(prompts)
        }
