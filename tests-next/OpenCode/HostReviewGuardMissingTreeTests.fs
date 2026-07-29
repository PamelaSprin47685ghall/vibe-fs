namespace Wanxiangshu.Next.Tests.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tests.EventDrivenHarness
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport

module HostReviewGuardMissingTreeTests =

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

    let private textPart text = MessagePart.Text text

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

    let private managerTurn sessionId messageId =
        { SessionId = SessionId.create sessionId
          UserMessageId = MessageId.create "u1"
          RootUserMessageId = MessageId.create "u1"
          AssistantMessageId = MessageId.create messageId
          AgentRole = Some AgentRole.Manager
          Directory = "/tmp/ws"
          Parts = [| textPart "manager done" |]
          Finish = Some "stop"
          ErrorName = None
          Model = None
          Outcome = TurnOutcome.TurnCompleted }

    [<Fact>]
    let ``Empty_string_tree_returns_Missing`` () =
        withTempDir (fun directory ->
            task {
                use journal =
                    AgentJournal.create directory (RuntimeId.create "empty-str-tree-runtime") 1 DateTimeOffset.UtcNow

                let port = { GetTreeHash = fun () -> "" }

                match HostReviewGuard.missingTree (Some journal) (Some port) "test-session" with
                | HostReviewGuard.ReviewGuardMissing hash -> Assert.Equal("", hash)
                | other -> Assert.True(false, sprintf "Expected Missing for empty string, got %A" other)
            })

    [<Fact>]
    let ``Whitespace_tree_returns_Missing`` () =
        withTempDir (fun directory ->
            task {
                use journal =
                    AgentJournal.create directory (RuntimeId.create "space-tree-runtime") 1 DateTimeOffset.UtcNow

                let port = { GetTreeHash = fun () -> "   " }

                match HostReviewGuard.missingTree (Some journal) (Some port) "test-session" with
                | HostReviewGuard.ReviewGuardMissing hash -> Assert.Equal("", hash)
                | other -> Assert.True(false, sprintf "Expected Missing for whitespace, got %A" other)
            })

    [<Fact>]
    let ``NO_HEAD_TREE_returns_Missing`` () =
        withTempDir (fun directory ->
            task {
                use journal =
                    AgentJournal.create directory (RuntimeId.create "no-head-tree-runtime") 1 DateTimeOffset.UtcNow

                let port = { GetTreeHash = fun () -> "NO_HEAD_TREE" }

                match HostReviewGuard.missingTree (Some journal) (Some port) "test-session" with
                | HostReviewGuard.ReviewGuardMissing hash -> Assert.Equal("NO_HEAD_TREE", hash)
                | other -> Assert.True(false, sprintf "Expected Missing for NO_HEAD_TREE, got %A" other)
            })

    [<Fact>]
    let ``Empty_tree_hash_returns_Missing`` () =
        withTempDir (fun directory ->
            task {
                use journal =
                    AgentJournal.create directory (RuntimeId.create "empty-hash-runtime") 1 DateTimeOffset.UtcNow

                let emptyHash = "4b825dc642cb6eb9a060e54bf8d69288fbee4904"
                let port = { GetTreeHash = fun () -> emptyHash }

                match HostReviewGuard.missingTree (Some journal) (Some port) "test-session" with
                | HostReviewGuard.ReviewGuardMissing hash -> Assert.Equal(emptyHash, hash)
                | other -> Assert.True(false, sprintf "Expected Missing for empty tree hash, got %A" other)
            })

    [<Fact>]
    let ``Empty_tree_triggers_manager_nudge_through_TerminalPolicies`` () =
        withTempDir (fun directory ->
            task {
                let sessionId = "manager-empty-tree"
                let messageId = "assistant-empty-tree"
                let prompts = ResizeArray<string * string>()
                let nudgeSent = HashSet<string>()
                let managerGuard = HashSet<string>()
                let parents = Dictionary<string, string>()

                use journal =
                    AgentJournal.create directory (RuntimeId.create "empty-tree-nudge-runtime") 1 DateTimeOffset.UtcNow

                registerAuthorityRoot journal sessionId "manager"
                let port = { GetTreeHash = fun () -> "" }

                applyDecide
                    (recordingPort prompts)
                    (Some journal)
                    (Some port)
                    (HashSet())
                    nudgeSent
                    managerGuard
                    parents
                    (managerTurn sessionId messageId)

                do! drainMicrotasks 16

                Assert.NotEmpty(prompts)
                let _, text = prompts.[0]
                Assert.Contains("review", text.ToLowerInvariant())
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
                        applyDecide
                            (recordingPort prompts)
                            (Some journal)
                            (Some throwingPort)
                            (HashSet())
                            (HashSet())
                            (HashSet())
                            (Dictionary())
                            (managerTurn sessionId "assistant-unavailable")

                        None
                    with :? InvalidOperationException as ex ->
                        Some ex

                match caught with
                | Some ex -> Assert.Contains("Review guard unavailable", ex.Message)
                | None -> Assert.True(false, "Review guard unavailable was not raised")
            })
