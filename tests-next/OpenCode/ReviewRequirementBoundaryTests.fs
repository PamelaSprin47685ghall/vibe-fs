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

module ReviewRequirementBoundaryTests =

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
                    Task.FromResult(Ok(MessageId.create ("accepted-" + id)))
                else
                    Task.FromResult(Error "AG-LISTENER-BEFORE-SEND")

            member _.SendChildPromptFireAndForget(_, _, _, _) = Task.FromResult(Ok())
            member _.AbortSession(_) = Task.FromResult(Ok())
            member _.AbortChildren(_) = Task.FromResult(()) :> Task
            member _.CreateChildSession(_, _) = Task.FromResult(Ok(SessionId.create "child"))
            member _.GetSessionOutput(_) = [] }

    let private textPart text =
        createObj [ "id", box "p1"; "type", box "text"; "text", box text ]

    let private applyDecide sessionPort journal parents turn =
        let eventPort = Events.HostEventPort()

        TerminalPolicies.apply
            (sessionPort :> ISessionHostPort)
            (eventPort :> IEventObservationPort)
            journal
            None
            (HashSet())
            (HashSet())
            (HashSet())
            parents
            (fun _ -> ())
            (HashSet())
            turn

    let private completedTurn sessionId role parts =
        { SessionId = SessionId.create sessionId
          UserMessageId = MessageId.create "u1"
          RootUserMessageId = MessageId.create "u1"
          AssistantMessageId = MessageId.create "a1"
          AgentRole = Some role
          Directory = "/tmp/ws"
          Parts = parts
          Finish = Some "stop"
          ErrorName = None
          Model = None
          Outcome = TurnOutcome.TurnCompleted }

    [<Fact>]
    let ``Confirmed reviewer terminal resets only previously reviewed human requirements`` () =
        withTempDir (fun directory ->
            task {
                let managerId = SessionId.create "manager-review-boundary"
                let reviewerId = SessionId.create "reviewer-review-boundary"
                let prompts = ResizeArray<string * string>()
                let sessionPort = recordingPort prompts

                use journal =
                    AgentJournal.create directory (RuntimeId.create "review-boundary-runtime") 1 DateTimeOffset.UtcNow

                let dispatcher = PromptDispatcher.forJournal journal

                let acceptHuman messageId =
                    match dispatcher.AcceptHumanRoot managerId (MessageId.create messageId) (Some "fast-manager") with
                    | Ok _ -> ()
                    | Error err -> failwithf "expected HumanRoot acceptance: %s" err

                let pendingIds () =
                    AgentJournal.pendingReviewRequirements (Some journal) managerId
                    |> List.map (fun input -> MessageId.value input.MessageId)

                acceptHuman "user-initial"
                acceptHuman "user-middle"
                Assert.Equal<string list>([ "user-initial"; "user-middle" ], pendingIds ())

                let append fact =
                    match AgentJournal.appendAgent (StreamId.Session managerId) None fact journal with
                    | Ok _ -> ()
                    | Error failure -> failwithf "journal append failed: %A" failure.Failure

                append (
                    AgentFact.AgentLinked
                        {| ParentId = managerId
                           ChildId = ChildId.create (SessionId.value reviewerId)
                           TargetAgent = "reviewer"
                           Role = Some "fast-reviewer" |}
                )

                append (
                    AgentFact.ReviewBarrierStarted
                        {| ManagerSessionId = managerId
                           BarrierKey = "review-boundary" |}
                )

                let recordVerdict providerRunId toolCallId userMessageId userPrompt =
                    append (
                        AgentFact.ReviewVerdictRecorded
                            {| ManagerSessionId = managerId
                               ReviewerSessionId = reviewerId
                               ProviderRunId = providerRunId
                               UserPromptText = Some userPrompt
                               UserMessageId = Some userMessageId
                               ToolCallId = toolCallId
                               GitTreeHash = "tree-review-boundary"
                               Verdict = ReviewGuardVerdict.Perfect |}
                    )

                recordVerdict "provider-run-1" "verdict-1" "review-root" "Review the worktree."

                append (
                    AgentFact.GuardPromptAccepted
                        {| TargetSessionId = reviewerId
                           GuardKey = "confirm-perfect:tree-review-boundary"
                           HostMessageId = "review-confirmation" |}
                )

                recordVerdict "a1" "verdict-2" "review-confirmation" "PERFECT requires confirmation."

                Assert.Equal<string list>([ "user-initial"; "user-middle" ], pendingIds ())

                let parents = Dictionary<string, string>()
                parents.[SessionId.value reviewerId] <- SessionId.value managerId

                applyDecide
                    sessionPort
                    (Some journal)
                    parents
                    (completedTurn (SessionId.value reviewerId) AgentRole.Reviewer [| textPart "review complete" |])

                do! drainMicrotasks 4
                Assert.Empty(pendingIds ())

                acceptHuman "user-after-idle"
                Assert.Equal<string list>([ "user-after-idle" ], pendingIds ())

                let unrelatedReviewerTerminal =
                    { completedTurn (SessionId.value reviewerId) AgentRole.Reviewer [| textPart "later output" |] with
                        AssistantMessageId = MessageId.create "later-reviewer-run" }

                applyDecide sessionPort (Some journal) parents unrelatedReviewerTerminal

                do! drainMicrotasks 4
                Assert.Equal<string list>([ "user-after-idle" ], pendingIds ())
            })
