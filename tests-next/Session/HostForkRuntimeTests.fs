namespace Wanxiangshu.Next.Tests.SessionTests

open System
open System.Threading.Tasks
open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tests.JournalTests
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport

module HostForkRuntimeTests =

    let private makeFake () =
        let mutable terminal: (SessionId -> TerminalOutcome -> unit) option = None
        let mutable childCount = 0
        let childId = SessionId.create "child-1"

        let host =
            { new ISessionHostPort with
                member _.SubscribeTerminal(_, listener) =
                    terminal <- Some listener

                    { new IDisposable with
                        member _.Dispose() = terminal <- None }

                member _.SendPrompt(_, _, _) =
                    Task.FromResult(Ok(MessageId.create "accepted"))

                member _.SendChildPromptFireAndForget(_, _, _, _) = Task.FromResult(Ok())

                member _.AbortSession(_) = Task.FromResult(Ok())

                member _.CreateChildSession(_, _) =
                    childCount <- childCount + 1
                    Task.FromResult(Ok childId)

                member _.GetSessionOutput(_) = [ "A version output" ] }

        let trigger () =
            terminal
            |> Option.iter (fun listener -> listener childId (Completed(MessageId.create "m-1")))

        host, trigger, (fun () -> childCount)

    [<Fact>]
    let ``HostForkRuntime_creates_child_reuses_it_and_joins_A_output`` () =
        task {
            let host, trigger, childCount = makeFake ()
            let bridge = HostForkRuntime(SessionId.create "parent", host)

            let! first = bridge.Fork("agent-1", AgentRole.Coder, "work")
            Assert.Equal(Ok(ForkResult.Created "agent-1"), first)

            trigger ()
            let! joined = bridge.Join()

            match joined with
            | Ok completion -> Assert.Equal(Ok "A version output", completion.Outcome)
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
                        member _.CreateChildSession(_, _) = Task.FromResult(Ok childId)
                        member _.GetSessionOutput(_) = [] }

                let bridge = HostForkRuntime(parentId, host, journal = journal)
                let! result = bridge.Fork("agent-durable", AgentRole.Coder, "work")
                Assert.Equal(Ok(ForkResult.Created "agent-durable"), result)
                Assert.True(promptSawLink)
            })
