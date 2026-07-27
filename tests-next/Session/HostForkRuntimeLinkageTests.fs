namespace Wanxiangshu.Next.Tests.SessionTests

open System
open System.Threading.Tasks
open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport

/// AgentLinked/AgentUnlinked fold + restart-linkage behavior (extracted from
/// HostForkRuntimeSessionDeadTests so the session-dead refusal tests stay focused).
module HostForkRuntimeLinkageTests =

    let private noopDisposable () =
        { new IDisposable with
            member _.Dispose() = () }

    let private makeCountingFake () =
        let mutable childPrompt = 0
        let mutable prompt = 0
        let childId = SessionId.create "child-1"

        let host =
            { new ISessionHostPort with
                member _.SubscribeTerminal(_, _) = noopDisposable ()

                member _.SendPrompt(_, _, _) =
                    prompt <- prompt + 1
                    Task.FromResult(Ok(MessageId.create "x"))

                member _.SendChildPromptFireAndForget(_, _, _, _) =
                    childPrompt <- childPrompt + 1
                    Task.FromResult(Ok())

                member _.AbortSession(_) = Task.FromResult(Ok())
                member _.AbortChildren(_) = Task.FromResult(()) :> Task
                member _.CreateChildSession(_, _) = Task.FromResult(Ok childId)
                member _.GetSessionOutput(_) = [] }

        host, (fun () -> childPrompt), (fun () -> prompt)

    let private link (journal: AgentJournal) (parentId: SessionId) (childId: SessionId) (agentId: string) =
        AgentJournal.appendAgent
            (StreamId.Session parentId)
            None
            (AgentFact.AgentLinked
                {| ParentId = parentId
                   ChildId = ChildId.create (SessionId.value childId)
                   TargetAgent = agentId
                   Role = Some "Coder" |})
            journal
        |> ignore

    // --- AgentUnlinked (SSOT §5 bounded projections) ---------------------------
    // A Cancel must persist AgentUnlinked per linked child BEFORE aborting, so a
    // crash mid-Cancel never leaks a link (which would restore a dead child on
    // restart). A leaked abort is recoverable. There is no child-normal-close
    // host event (host docs confirm no `session.deleted`), so a normally
    // completing child intentionally KEEPS its link for Reuse/nudge.

    [<Fact>]
    let ``Cancel_persists_AgentUnlinked_per_linked_child`` () =
        withTempDir (fun d ->
            task {
                let p = SessionId.create "p-unlink"
                let c1 = SessionId.create "child-unlink-1"
                let c2 = SessionId.create "child-unlink-2"

                use j = AgentJournal.create d (RuntimeId.create "r-unlink") 1 DateTimeOffset.UtcNow

                // Seed two linked children directly so restoreChildren populates
                // the runtime's child map (mirrors a prior Fork on restart).
                link j p c1 "agent-unlink-1"
                link j p c2 "agent-unlink-2"

                let host, _, _ = makeCountingFake ()
                let b = HostForkRuntime(p, host, journal = j)
                do! b.Cancel()

                let linkage =
                    AgentJournal.snapshot j
                    |> fun s -> s.AgentProjections.Sessions.TryFind p
                    |> Option.bind (fun sess -> sess.Linkage)
                    |> Option.map (fun l -> l.LinkedChildren)

                Assert.True(linkage.IsSome, "parent linkage projection should exist")
                Assert.True(linkage.Value.IsEmpty, "all children should be unlinked after Cancel")
            })

    [<Fact>]
    let ``Restart_skips a previously unlinked child`` () =
        withTempDir (fun d ->
            task {
                let p = SessionId.create "p-restart"
                let c = SessionId.create "child-restart"

                use j1 =
                    AgentJournal.create d (RuntimeId.create "r-restart-1") 1 DateTimeOffset.UtcNow

                link j1 p c "agent-restart"

                let host1, _, _ = makeCountingFake ()
                let b1 = HostForkRuntime(p, host1, journal = j1)
                do! b1.Cancel()

                (j1 :> IDisposable).Dispose()

                use j2 =
                    AgentJournal.createFromBoot
                        d
                        (RuntimeId.create "r-restart-2")
                        2
                        DateTimeOffset.UtcNow
                        (Boot.boot d)

                let host2, cc2, pc2 = makeCountingFake ()
                let b2 = HostForkRuntime(p, host2, journal = j2)
                let! reuse = b2.Reuse("agent-restart", "continue")

                match reuse with
                | Error e -> Assert.Contains("Unknown agent id", e)
                | Ok _ -> Assert.True(false, "expected unknown-agent error for unlinked child after restart")
                // No prompt/abort side effects are triggered by a Reuse that finds no child.
                Assert.Equal(0, cc2 ())
                Assert.Equal(0, pc2 ())
            })

    [<Fact>]
    let ``AgentUnlinked_fold_is_idempotent_on_duplicates`` () =
        let p = SessionId.create "p-idem"
        let c = ChildId.create "child-idem"

        let linked =
            AgentFacts.foldAgentFactWithEnvelope
                AgentFacts.empty
                { RuntimeId = RuntimeId.create "r-idem"
                  LocalSeq = LocalSeq.create 1L
                  ObservedAt = DateTimeOffset.UtcNow
                  EventId = EventId.create "evt-idem-link"
                  Stream = StreamId.Session p
                  TurnId = None
                  Fact =
                    Fact.Agent(
                        AgentFact.AgentLinked
                            {| ParentId = p
                               ChildId = c
                               TargetAgent = "agent-idem"
                               Role = Some "Coder" |}
                    ) }

        let unlink seq =
            { RuntimeId = RuntimeId.create "r-idem"
              LocalSeq = LocalSeq.create seq
              ObservedAt = DateTimeOffset.UtcNow.AddSeconds(float seq)
              EventId = EventId.create (sprintf "evt-idem-unlink-%d" seq)
              Stream = StreamId.Session p
              TurnId = None
              Fact = Fact.Agent(AgentFact.AgentUnlinked {| ParentId = p; ChildId = c |}) }

        let once = AgentFacts.foldAgentFactWithEnvelope linked (unlink 2L)
        let twice = AgentFacts.foldAgentFactWithEnvelope once (unlink 3L)

        Assert.Equal(once, twice)

        let linkage =
            once.Sessions.TryFind p
            |> Option.bind (fun sess -> sess.Linkage)
            |> Option.map (fun l -> l.LinkedChildren)

        Assert.True(linkage.IsSome && linkage.Value.IsEmpty, "linkage should be empty after unlink")
        // The duplicate unlink must not resurrect or duplicate the link.
        Assert.Equal(once, twice)
