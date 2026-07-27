namespace Wanxiangshu.Next.Tests.ReviewTests

open System
open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Review
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport

/// Split out of GuardTests.fs (architecture gate: files <= 300 lines). Covers
/// the HostPort/JournalPort adapter (Guard.guardMissingVerdict / recordVerdict
/// / tryFinish) and durable journal round-trips, as opposed to the pure fold
/// tests in GuardTests.fs.
module GuardDurableTests =

    [<Fact>]
    let ``Duplicate guard key handled idempotently (at-most-once)`` () =
        let sid = SessionId.create "mgr-4"
        let mutable hostCalls = 0

        let hostPort: HostPort =
            { SendGuardPrompt =
                fun _ key _ ->
                    hostCalls <- hostCalls + 1
                    Ok("msg-" + string hostCalls) }

        let mutable currentProj =
            { AgentProjections = AgentFacts.empty
              RuntimeId = None }

        let journalPort: JournalPort =
            { AppendFact =
                fun _ fact ->
                    currentProj <-
                        { currentProj with
                            AgentProjections = AgentFacts.foldAgentFact currentProj.AgentProjections fact }

                    Ok currentProj }

        let key = "prompt-guard-key-1"

        // First call: sends prompt to host port and appends fact
        let res1 =
            Guard.guardMissingVerdict hostPort journalPort sid key "Please review" currentProj

        Assert.True(Result.isOk res1)
        let (proj1, msgId1) = res1 |> Result.defaultWith (fun _ -> failwith "unexpected")
        Assert.Equal(1, hostCalls)
        Assert.Equal(Some "msg-1", msgId1)
        Assert.Equal(Some key, proj1.AgentProjections.Sessions.[sid].ReviewGuard.Value.AcceptedGuardKey)

        // Second call with same key: skips host port call (idempotent)
        let res2 =
            Guard.guardMissingVerdict hostPort journalPort sid key "Please review" proj1

        Assert.True(Result.isOk res2)
        let (_, msgId2) = res2 |> Result.defaultWith (fun _ -> failwith "unexpected")
        Assert.Equal(1, hostCalls) // Host call count did not increase!
        Assert.Equal(None, msgId2)

    [<Fact>]
    let ``Durable port test: append-before-return and double perfect finish allowance`` () =
        withTempDir (fun tempDir ->
            task {
                let runtimeId = RuntimeId.create "rt-guard-durable"
                let sid = SessionId.create "mgr-durable"
                let revSid = SessionId.create "rev-durable"
                let treeHash = "tree-durable-100"
                let now = DateTimeOffset.UtcNow

                use journal = AgentJournal.create tempDir runtimeId 1001 now
                let journalPort = JournalPort.fromAgentJournal journal

                let mutable currentTreeHash = treeHash
                let gitPort: GitPort = { GetTreeHash = fun () -> currentTreeHash }

                // Initial finish check -> NeedsReview
                let initialFinish = Guard.tryFinish gitPort sid (AgentJournal.snapshot journal)
                Assert.Equal(ReviewFinishResult.NeedsReview, initialFinish)

                // Record 1st verdict -> appends to journal and returns projection immediately
                let res1 =
                    Guard.recordVerdict journalPort sid revSid "pr-1" "tc-1" treeHash ReviewGuardVerdict.Perfect None

                Assert.True(Result.isOk res1)
                let proj1 = Result.defaultWith (fun _ -> failwith "unexpected") res1

                // Prove append happened before return: snapshot matches returned projection
                Assert.Equal(proj1, AgentJournal.snapshot journal)
                Assert.Equal(ReviewFinishResult.NeedsReview, Guard.tryFinish gitPort sid proj1)

                // Record guard prompt acceptance for confirmation prompt
                let promptFact =
                    AgentFact.GuardPromptAccepted
                        {| TargetSessionId = sid
                           GuardKey = "guard-key-2"
                           HostMessageId = "confirm-user-2" |}

                let _ = AgentJournal.appendAgent (StreamId.Session sid) None promptFact journal

                // Record 2nd verdict on same tree with distinct run + confirmation root user id
                let res2 =
                    Guard.recordVerdict
                        journalPort
                        sid
                        revSid
                        "pr-2"
                        "tc-2"
                        treeHash
                        ReviewGuardVerdict.Perfect
                        (Some "confirm-user-2")

                Assert.True(Result.isOk res2)
                let proj2 = Result.defaultWith (fun _ -> failwith "unexpected") res2
                Assert.Equal(proj2, AgentJournal.snapshot journal)

                // Double perfect on same tree with confirmation identity -> Confirmed!
                Assert.Equal(ReviewFinishResult.Confirmed, Guard.tryFinish gitPort sid proj2)

                // Git tree hash changes -> tryFinish returns NeedsReview without creating any fact
                currentTreeHash <- "tree-durable-200"
                Assert.Equal(ReviewFinishResult.NeedsReview, Guard.tryFinish gitPort sid proj2)
            })
