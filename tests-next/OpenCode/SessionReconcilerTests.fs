namespace Wanxiangshu.Next.Tests.OpenCode

open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tests.EventDrivenHarness

module SessionReconcilerTests =

    type private FakeSnapshot(initial: SessionMessage list) =
        let calls = ResizeArray<string>()
        let mutable messages = initial

        interface ISessionSnapshotPort with
            member _.GetMessages(sessionId) =
                calls.Add(SessionId.value sessionId)
                Task.FromResult(Ok messages)

        member _.Set(next) = messages <- next
        member _.Calls = calls

    type private SequencedSnapshot(snapshots: SessionMessage list list) =
        let calls = ResizeArray<string>()
        let mutable remaining = snapshots

        interface ISessionSnapshotPort with
            member _.GetMessages(sessionId) =
                calls.Add(SessionId.value sessionId)

                let next =
                    match remaining with
                    | head :: tail ->
                        remaining <- tail
                        head
                    | [] -> []

                Task.FromResult(Ok next)

        member _.Calls = calls

    let private textPart text =
        createObj [ "id", box "p1"; "type", box "text"; "text", box text ]

    let private msg id role agent finish parts errorName =
        { Id = MessageId.create id
          Role = role
          Agent = agent
          Finish = finish
          ErrorName = errorName
          Model = None
          Parts = parts
          Raw = createObj [] }

    let private bind (reconciler: SessionReconciler) sessionId userId role =
        reconciler.BindActiveRun
            { SessionId = SessionId.create sessionId
              RunId = None
              RootUserMessageId = Some(MessageId.create userId)
              PhysicalUserMessageId = Some(MessageId.create userId)
              ContinuationMessageIds = Set.empty
              AgentRole = Some role
              Directory = "/tmp/ws" }

    [<Fact>]
    let ``Duplicate_idle_completes_once`` () =
        task {
            let turns = ResizeArray<ReconciledTurn>()

            let snapshot =
                FakeSnapshot(
                    [ msg "u1" "user" None None [||] None
                      msg "a1" "assistant" (Some "coder") (Some "stop") [| textPart "done" |] None ]
                )

            let reconciler =
                SessionReconciler(snapshot :> ISessionSnapshotPort, (fun turn -> turns.Add turn))

            bind reconciler "s1" "u1" AgentRole.Coder
            reconciler.MarkDirty(SessionId.create "s1")
            reconciler.MarkDirty(SessionId.create "s1")
            reconciler.MarkDirty(SessionId.create "s1")
            do! drainMicrotasks 16

            Assert.Equal(1, turns.Count)
            Assert.Equal(TurnOutcome.TurnCompleted, turns.[0].Outcome)
            Assert.True(snapshot.Calls.Count >= 1)
            Assert.True(snapshot.Calls.Count <= 2)
        }

    [<Fact>]
    let ``Unknown_without_assistant_has_no_side_effects`` () =
        task {
            let turns = ResizeArray<ReconciledTurn>()
            let snapshot = FakeSnapshot([ msg "u1" "user" None None [||] None ])

            let reconciler =
                SessionReconciler(snapshot :> ISessionSnapshotPort, (fun turn -> turns.Add turn))

            bind reconciler "s1" "u1" AgentRole.Coder
            reconciler.MarkDirty(SessionId.create "s1")
            do! drainMicrotasks 8
            Assert.Empty(turns)
        }

    [<Fact>]
    let ``Assistant_arriving_after_idle_completes_on_rerun`` () =
        task {
            let turns = ResizeArray<ReconciledTurn>()
            let snapshot = FakeSnapshot([ msg "u1" "user" None None [||] None ])

            let reconciler =
                SessionReconciler(snapshot :> ISessionSnapshotPort, (fun turn -> turns.Add turn))

            bind reconciler "s1" "u1" AgentRole.Coder
            reconciler.MarkDirty(SessionId.create "s1")
            do! drainMicrotasks 8
            Assert.Empty(turns)

            snapshot.Set(
                [ msg "u1" "user" None None [||] None
                  msg "a1" "assistant" (Some "coder") (Some "stop") [| textPart "later" |] None ]
            )

            reconciler.MarkDirty(SessionId.create "s1")
            do! drainMicrotasks 8
            Assert.Equal(1, turns.Count)
            Assert.Equal("a1", MessageId.value turns.[0].AssistantMessageId)
        }

    [<Fact>]
    let ``Single_idle_rereads_until_a_terminal_turn_is_visible`` () =
        task {
            let turns = ResizeArray<ReconciledTurn>()

            let terminal =
                [ msg "u1" "user" None None [||] None
                  msg "a1" "assistant" (Some "coder") (Some "stop") [| textPart "done" |] None ]

            let snapshot =
                SequencedSnapshot(
                    [ [ msg "u1" "user" None None [||] None ]
                      [ msg "u1" "user" None None [||] None ]
                      terminal ]
                )

            let reconciler =
                SessionReconciler(snapshot :> ISessionSnapshotPort, (fun turn -> turns.Add turn))

            bind reconciler "s1" "u1" AgentRole.Coder
            reconciler.MarkDirty(SessionId.create "s1")
            do! drainMicrotasks 16

            Assert.Equal(3, snapshot.Calls.Count)
            Assert.Single(turns) |> ignore
            Assert.Equal("a1", MessageId.value turns.[0].AssistantMessageId)
        }

    [<Fact>]
    let ``Three_unknown_reads_remain_dirty_for_the_next_idle`` () =
        task {
            let turns = ResizeArray<ReconciledTurn>()

            let terminal =
                [ msg "u1" "user" None None [||] None
                  msg "a1" "assistant" (Some "coder") (Some "stop") [| textPart "later" |] None ]

            let snapshot =
                SequencedSnapshot(
                    [ [ msg "u1" "user" None None [||] None ]
                      [ msg "u1" "user" None None [||] None ]
                      [ msg "u1" "user" None None [||] None ]
                      terminal ]
                )

            let reconciler =
                SessionReconciler(snapshot :> ISessionSnapshotPort, (fun turn -> turns.Add turn))

            bind reconciler "s1" "u1" AgentRole.Coder
            reconciler.MarkDirty(SessionId.create "s1")
            do! drainMicrotasks 16
            Assert.Empty(turns)
            Assert.Equal(3, snapshot.Calls.Count)

            reconciler.MarkDirty(SessionId.create "s1")
            do! drainMicrotasks 16
            Assert.Single(turns) |> ignore
            Assert.Equal(4, snapshot.Calls.Count)
        }

    [<Fact>]
    let ``Provider_retry_does_not_enter_reconciler`` () =
        task {
            let turns = ResizeArray<ReconciledTurn>()
            let snapshot = FakeSnapshot([ msg "u1" "user" None None [||] None ])

            let reconciler =
                SessionReconciler(snapshot :> ISessionSnapshotPort, (fun turn -> turns.Add turn))

            reconciler.HandleSignal(
                ProviderRetry
                    { SessionId = SessionId.create "s1"
                      Attempt = "host-attempt"
                      Reason = "retry"
                      MessageId = Some(MessageId.create "u1") }
            )

            do! drainMicrotasks 4

            Assert.Empty(snapshot.Calls)
            Assert.Empty(turns)
        }

    [<Fact>]
    let ``Abort_outcome_from_complete_message`` () =
        task {
            let turns = ResizeArray<ReconciledTurn>()

            let snapshot =
                FakeSnapshot(
                    [ msg "u1" "user" None None [||] None
                      msg "a1" "assistant" (Some "coder") (Some "error") [||] (Some "MessageAbortedError") ]
                )

            let reconciler =
                SessionReconciler(snapshot :> ISessionSnapshotPort, (fun turn -> turns.Add turn))

            bind reconciler "s1" "u1" AgentRole.Coder
            reconciler.MarkDirty(SessionId.create "s1")
            do! drainMicrotasks 8
            Assert.Equal(1, turns.Count)

            match turns.[0].Outcome with
            | TurnOutcome.TurnAborted _ -> ()
            | other -> Assert.True(false, sprintf "expected TurnAborted, got %A" other)
        }

    [<Fact>]
    let ``Continuation preserves authority root and reports physical user message`` () =
        task {
            let turns = ResizeArray<ReconciledTurn>()
            let sid = SessionId.create "s-continuation"

            let snapshot =
                FakeSnapshot(
                    [ msg "u-root" "user" None None [||] None
                      msg "u-confirm" "user" None None [||] None
                      msg "a-confirm" "assistant" (Some "reviewer") (Some "stop") [| textPart "confirmed" |] None ]
                )

            let reconciler =
                SessionReconciler(snapshot :> ISessionSnapshotPort, (fun turn -> turns.Add turn))

            bind reconciler "s-continuation" "u-root" AgentRole.Reviewer
            reconciler.BindContinuationUserMessage(sid, MessageId.create "u-confirm")
            reconciler.MarkDirty(sid)
            do! drainMicrotasks 8

            Assert.Single(turns) |> ignore
            Assert.Equal("u-root", MessageId.value turns.[0].RootUserMessageId)
            Assert.Equal("u-confirm", MessageId.value turns.[0].UserMessageId)
        }

    [<Fact>]
    let ``Wrong_role_does_not_need_continuation`` () =
        let parts = [| createObj [ "id", box "p"; "type", box "text"; "text", box "" ] |]

        Assert.False(
            CompletedTurnClassifier.needsZeroWidthContinuation (Some AgentRole.Executor) TurnOutcome.TurnCompleted parts
        )

        Assert.False(CompletedTurnClassifier.needsZeroWidthContinuation None TurnOutcome.TurnCompleted parts)

        Assert.True(
            CompletedTurnClassifier.needsZeroWidthContinuation (Some AgentRole.Coder) TurnOutcome.TurnCompleted parts
        )
