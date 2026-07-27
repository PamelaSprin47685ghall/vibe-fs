namespace Wanxiangshu.Next.Tests.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tests.EventDrivenHarness
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport

/// ReviewGuard coverage migrated from the deleted HostEventRouter to the
/// Decide layer (TerminalPolicies.apply). The router previously funneled
/// completed turns through HostReviewGuard; we now drive the same path with a
/// constructed ReconciledTurn.
module HostReviewGuardTests =

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

    let private gatedRecordingPort
        (prompts: ResizeArray<string * string>)
        (sendObserved: TaskCompletionSource<unit>)
        (acceptance: TaskCompletionSource<Result<MessageId, string>>)
        =
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
                    sendObserved.SetResult(())
                    acceptance.Task
                else
                    Task.FromResult(Error "AG-LISTENER-BEFORE-SEND")

            member _.SendChildPromptFireAndForget(_, _, _, _) = Task.FromResult(Ok())
            member _.AbortSession(_) = Task.FromResult(Ok())
            member _.AbortChildren(_) = Task.FromResult(()) :> Task

            member _.CreateChildSession(_, _) =
                Task.FromResult(Ok(SessionId.create "child"))

            member _.GetSessionOutput(_) = [] }

    let private hasAcceptedGuard (journal: AgentJournal) sessionId guardKey =
        let sid = SessionId.create sessionId

        match Map.tryFind sid (AgentJournal.snapshot journal).AgentProjections.Sessions with
        | Some session ->
            match session.ReviewGuard with
            | Some guard -> guard.AcceptedGuardKey = Some guardKey
            | None -> false
        | None -> false

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

    let private reviewerTurn sessionId messageId =
        { SessionId = SessionId.create sessionId
          UserMessageId = MessageId.create "u1"
          AssistantMessageId = MessageId.create messageId
          AgentRole = Some AgentRole.Reviewer
          Directory = "/tmp/ws"
          Parts = [| textPart "reviewed tree" |]
          Finish = Some "stop"
          ErrorName = None
          Model = None
          Outcome = TurnOutcome.TurnCompleted }

    let private managerTurn sessionId messageId =
        { SessionId = SessionId.create sessionId
          UserMessageId = MessageId.create "u1"
          AssistantMessageId = MessageId.create messageId
          AgentRole = Some AgentRole.Manager
          Directory = "/tmp/ws"
          Parts = [| textPart "manager done" |]
          Finish = Some "stop"
          ErrorName = None
          Model = None
          Outcome = TurnOutcome.TurnCompleted }

    [<Fact>]
    let ``Reviewer_terminal_without_verdict_appends_guard_fact_after_send_and_survives_restart`` () =
        withTempDir (fun directory ->
            task {
                let sessionId = "reviewer-guard-session"
                let messageId = "assistant-reviewer-guard"
                let prompts = ResizeArray<string * string>()

                use journal =
                    AgentJournal.create directory (RuntimeId.create "reviewer-guard-runtime") 1 DateTimeOffset.UtcNow

                applyDecide (recordingPort prompts) (Some journal) None (HashSet()) (HashSet()) (HashSet()) (Dictionary())
                    (reviewerTurn sessionId messageId)
                do! drainMicrotasks 16

                let guardKey = sprintf "review-guard:%s:%s:%s" sessionId messageId "missing-verdict"
                Assert.Single(prompts) |> ignore
                Assert.True(hasAcceptedGuard journal sessionId guardKey)

                (journal :> IDisposable).Dispose()
                let boot = Boot.boot directory

                use restartedJournal =
                    AgentJournal.createFromBoot
                        directory
                        (RuntimeId.create "reviewer-guard-restart")
                        2
                        DateTimeOffset.UtcNow
                        boot

                // Restart must not append a second acceptance: the guard fact is
                // durable and the in-memory dedupe is re-seeded from the projection.
                applyDecide (recordingPort prompts) (Some restartedJournal) None (HashSet()) (HashSet()) (HashSet())
                    (Dictionary()) (reviewerTurn sessionId messageId)
                Assert.Single(prompts) |> ignore
            })

    [<Fact>]
    let ``Reviewer_guard_send_failure_does_not_append_acceptance_fact`` () =
        withTempDir (fun directory ->
            task {
                let sessionId = "reviewer-guard-failure"
                let messageId = "assistant-reviewer-failure"
                let prompts = ResizeArray<string * string>()
                let sendObserved = TaskCompletionSource<unit>()
                let acceptance = TaskCompletionSource<Result<MessageId, string>>()
                acceptance.SetResult(Error "send failed")

                use journal =
                    AgentJournal.create
                        directory
                        (RuntimeId.create "reviewer-guard-failure-runtime")
                        1
                        DateTimeOffset.UtcNow

                applyDecide (gatedRecordingPort prompts sendObserved acceptance) (Some journal) None (HashSet()) (HashSet())
                    (HashSet()) (Dictionary()) (reviewerTurn sessionId messageId)
                let! _ = sendObserved.Task
                let! _ = acceptance.Task
                do! drainMicrotasks 8

                let guardKey = sprintf "review-guard:%s:%s:%s" sessionId messageId "missing-verdict"
                Assert.False(hasAcceptedGuard journal sessionId guardKey)
                Assert.Single(prompts) |> ignore
            })

    [<Fact>]
    let ``Manager_review_guard_unavailable_fails_closed`` () =
        withTempDir (fun directory ->
            task {
                let sessionId = "manager-guard-unavailable"
                let prompts = ResizeArray<string * string>()

                use journal =
                    AgentJournal.create
                        directory
                        (RuntimeId.create "manager-guard-unavailable-runtime")
                        1
                        DateTimeOffset.UtcNow

                match HostReviewGuard.missingTree None (Some { GetTreeHash = fun () -> "tree" }) sessionId with
                | HostReviewGuard.ReviewGuardUnavailable _ -> ()
                | result -> Assert.True(false, sprintf "Expected unavailable journal result, got %A" result)

                let throwingPort =
                    { GetTreeHash = fun () -> raise (InvalidOperationException("tree read failed")) }

                match HostReviewGuard.missingTree (Some journal) (Some throwingPort) sessionId with
                | HostReviewGuard.ReviewGuardUnavailable _ -> ()
                | result -> Assert.True(false, sprintf "Expected unavailable tree result, got %A" result)

                let caught =
                    try
                        applyDecide (recordingPort prompts) (Some journal) (Some throwingPort) (HashSet()) (HashSet())
                            (HashSet()) (Dictionary()) (managerTurn sessionId "assistant-unavailable")
                        None
                    with :? InvalidOperationException as ex -> Some ex

                match caught with
                | Some ex -> Assert.Contains("Review guard unavailable", ex.Message)
                | None -> Assert.True(false, "Review guard unavailable was not raised")
            })
