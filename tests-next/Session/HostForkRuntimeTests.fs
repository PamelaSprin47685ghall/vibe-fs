namespace Wanxiangshu.Next.Tests.SessionTests

open System
open System.Threading.Tasks
open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tests.JournalTests
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport

module HostForkRuntimeTests =

    let private makeFake () =
        let mutable terminal: (SessionId -> TerminalOutcome -> unit) option = None
        let mutable childCount = 0
        let mutable childPromptCount = 0
        let mutable output: string list = []
        let childId = SessionId.create "child-1"

        let host =
            { new ISessionHostPort with
                member _.SubscribeTerminal(_, listener) =
                    terminal <- Some listener

                    { new IDisposable with
                        member _.Dispose() = terminal <- None }

                member _.SendPrompt(_, _, _) =
                    Task.FromResult(Ok(MessageId.create "accepted"))

                member _.SendChildPromptFireAndForget(_, _, _, _) =
                    childPromptCount <- childPromptCount + 1
                    Task.FromResult(Ok())

                member _.AbortSession(_) = Task.FromResult(Ok())
                member _.AbortChildren(_) = Task.FromResult(()) :> Task

                member _.CreateChildSession(_, _) =
                    childCount <- childCount + 1
                    Task.FromResult(Ok childId)

                member _.GetSessionOutput(_) = output }

        let trigger () =
            output <- output @ [ "A version output" ]

            terminal
            |> Option.iter (fun listener ->
                listener
                    childId
                    (TerminalOutcome.Completed(
                        { SessionId = SessionId.create "m-1"
                          RootUserMessageId = MessageId.create "m-1"
                          AssistantMessageId = MessageId.create "m-1"
                          Role = "test"
                          Directory = ""
                          FinalText = "A version output" }
                    )))

        host, trigger, (fun () -> childCount), (fun () -> childPromptCount)

    [<Fact>]
    let ``HostForkRuntime_creates_child_reuses_it_and_joins_A_output`` () =
        task {
            let host, trigger, childCount, _ = makeFake ()
            let bridge = HostForkRuntime(SessionId.create "parent", host)

            let! first = bridge.Fork("agent-1", AgentRole.Coder, "work")
            Assert.Equal(Ok(ForkResult.Created "agent-1"), first)

            trigger ()
            let! joined = bridge.Join()

            match joined with
            | Ok completion -> Assert.equal ("A version output", AgentCompletion.text completion.Outcome)
            | Error error -> Assert.True(false, sprintf "Expected completion, got %A" error)

            let! second = bridge.Reuse("agent-1", "continue")
            Assert.Equal(Ok(ForkResult.Nudged "agent-1"), second)
            Assert.Equal(1, childCount ())
        }

    [<Fact>]
    let ``HostForkRuntime_persists_linkage_before_sending_prompt`` () =
        withTempDir (fun tempDir ->
            task {
                let parentId = SessionId.create "parent-durable"
                let childId = SessionId.create "child-durable"

                use journal =
                    AgentJournal.create tempDir (RuntimeId.create "runtime-durable") 1 DateTimeOffset.UtcNow

                let mutable promptSawLink = false

                let host =
                    { new ISessionHostPort with
                        member _.SubscribeTerminal(_, _) =
                            { new IDisposable with
                                member _.Dispose() = () }

                        member _.SendPrompt(_, _, _) =
                            let session =
                                AgentJournal.snapshot journal
                                |> fun p -> p.AgentProjections.Sessions.TryFind parentId

                            promptSawLink <-
                                session
                                |> Option.bind (fun s -> s.Linkage)
                                |> Option.exists (fun l ->
                                    l.LinkedChildren.ContainsKey(ChildId.create (SessionId.value childId)))

                            Task.FromResult(Ok(MessageId.create "accepted"))

                        member _.SendChildPromptFireAndForget(_, _, _, _) = Task.FromResult(Ok())
                        member _.AbortSession(_) = Task.FromResult(Ok())
                        member _.AbortChildren(_) = Task.FromResult(()) :> Task
                        member _.CreateChildSession(_, _) = Task.FromResult(Ok childId)
                        member _.GetSessionOutput(_) = [] }

                let bridge = HostForkRuntime(parentId, host, journal = journal)
                let! result = bridge.Fork("agent-durable", AgentRole.Coder, "work")
                Assert.Equal(Ok(ForkResult.Created "agent-durable"), result)
                Assert.True(promptSawLink)
            })

    [<Fact>]
    let ``HostForkRuntime_restores_linked_child_for_nudge`` () =
        withTempDir (fun tempDir ->
            task {
                let parentId = SessionId.create "parent-restored"
                let childId = ChildId.create "child-1"

                use journal =
                    AgentJournal.create tempDir (RuntimeId.create "runtime-restored") 1 DateTimeOffset.UtcNow

                let linkFact =
                    AgentFact.AgentForked
                        {| ParentId = parentId
                           ChildId = childId
                           TargetAgent = "agent-restored"
                           Role = Some "fast-coder" |}

                Assert.True(Result.isOk (AgentJournal.appendAgent (StreamId.Session parentId) None linkFact journal))

                let host, _, childCount, _ = makeFake ()
                let bridge = HostForkRuntime(parentId, host, journal = journal)
                let! result = bridge.Reuse("agent-restored", "continue")

                Assert.Equal(Ok(ForkResult.Nudged "agent-restored"), result)
                Assert.Equal(0, childCount ())
            })

    [<Fact>]
    let ``HostForkRuntime_busy_nudge_reuses_one_active_completion`` () =
        task {
            let host, trigger, _, childPromptCount = makeFake ()
            let bridge = HostForkRuntime(SessionId.create "parent-overlap", host)

            let! first = bridge.Fork("agent-overlap", AgentRole.Coder, "start")
            Assert.Equal(Ok(ForkResult.Created "agent-overlap"), first)
            Assert.Equal(1, bridge.PendingRunCount)

            let! nudged = bridge.Reuse("agent-overlap", "continue while busy")
            Assert.Equal(Ok(ForkResult.Nudged "agent-overlap"), nudged)
            Assert.Equal(1, bridge.PendingRunCount)
            Assert.Equal(1, childPromptCount ())

            trigger ()
            let! joined = bridge.Join()

            match joined with
            | Ok completion -> Assert.equal ("A version output", AgentCompletion.text completion.Outcome)
            | Error error -> Assert.True(false, sprintf "Expected completion, got %A" error)

            Assert.Equal(0, bridge.PendingRunCount)
            Assert.Equal(0, bridge.PendingCompletionCount)
        }

    [<Fact>]
    let ``HostForkRuntime_sends_agent_without_model_override`` () =
        withTempDir (fun tempDir ->
            task {
                let parentId = SessionId.create "parent-model"
                let childId = SessionId.create "child-model"
                let captured = ResizeArray<OpenCodePromptOptions>()
                let mutable terminal: (SessionId -> TerminalOutcome -> unit) option = None

                let host =
                    { new ISessionHostPort with
                        member _.SubscribeTerminal(_, listener) =
                            terminal <- Some listener

                            { new IDisposable with
                                member _.Dispose() = terminal <- None }

                        member _.SendPrompt(_, _, options) =
                            captured.Add options
                            Task.FromResult(Ok(MessageId.create "accepted"))

                        member _.SendChildPromptFireAndForget(_, _, _, options) =
                            captured.Add options
                            Task.FromResult(Ok())

                        member _.AbortSession(_) = Task.FromResult(Ok())
                        member _.AbortChildren(_) = Task.FromResult(()) :> Task
                        member _.CreateChildSession(_, _) = Task.FromResult(Ok childId)
                        member _.GetSessionOutput(_) = [] }

                let trigger () =
                    terminal
                    |> Option.iter (fun listener ->
                        listener
                            childId
                            (TerminalOutcome.Completed(
                                { SessionId = SessionId.create "model-terminal"
                                  RootUserMessageId = MessageId.create "model-terminal"
                                  AssistantMessageId = MessageId.create "model-terminal"
                                  Role = "test"
                                  Directory = ""
                                  FinalText = "A version output" }
                            )))

                use journal =
                    AgentJournal.create tempDir (RuntimeId.create "runtime-model") 1 DateTimeOffset.UtcNow

                let bridge = HostForkRuntime(parentId, host, journal = journal)

                let! first = bridge.Fork("agent-model", AgentRole.Coder, "first", agent = "fast-coder")
                Assert.equal (Ok(ForkResult.Created "agent-model"), first)
                Assert.equal (None, captured.[0].Model)
                Assert.equal (Some "fast-coder", captured.[0].Agent)
                trigger ()

                let! second = bridge.Reuse("agent-model", "second")
                Assert.equal (Ok(ForkResult.Nudged "agent-model"), second)
                Assert.True(captured.Count >= 2)
                Assert.equal (None, captured.[1].Model)
                trigger ()
            })

    [<Fact>]
    let ``HostForkRuntime_cancel_invokes_fallback_cancel_for_parent_and_children`` () =
        task {
            let parentId = SessionId.create "parent-cancel"
            let childId = SessionId.create "child-cancel"
            let captured = ResizeArray<SessionId>()

            let host =
                { new ISessionHostPort with
                    member _.SubscribeTerminal(_, _) =
                        { new IDisposable with
                            member _.Dispose() = () }

                    member _.SendPrompt(_, _, _) =
                        Task.FromResult(Ok(MessageId.create "accepted"))

                    member _.SendChildPromptFireAndForget(_, _, _, _) = Task.FromResult(Ok())

                    member _.AbortSession(_) = Task.FromResult(Ok())
                    member _.AbortChildren(_) = Task.FromResult(()) :> Task
                    member _.CreateChildSession(_, _) = Task.FromResult(Ok childId)
                    member _.GetSessionOutput(_) = [] }

            let bridge =
                HostForkRuntime(parentId, host, cancelFallbackRetries = (fun ids -> captured.AddRange(ids)))

            let! first = bridge.Fork("agent-1", AgentRole.Coder, "work")
            Assert.Equal(Ok(ForkResult.Created "agent-1"), first)

            bridge.Cancel()

            Assert.True(captured.Contains(parentId), "parent fallback not cancelled")
            Assert.True(captured.Contains(childId), "child fallback not cancelled")
            Assert.True(bridge.IsCancelled, "runtime should be cancelled")
        }
