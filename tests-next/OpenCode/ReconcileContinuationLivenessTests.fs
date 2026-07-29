namespace Wanxiangshu.Next.Tests.OpenCode

open System.Collections.Generic
open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tests.EventDrivenHarness
open Wanxiangshu.Next.Tests.OpenCode.ReconcileContinuationSupport

module ReconcileContinuationLivenessTests =

    [<Fact>]
    let ``Formal terminal supersedes provisional reasoning-only snapshot`` () =
        task {
            let turns = ResizeArray<ReconciledTurn>()
            let provisional = reasoningOnlyMessages "deep-devops"

            let terminal =
                [ msg "u1" "user" (Some "deep-devops") None [||]
                  msg "a1" "assistant" (Some "deep-devops") (Some "stop") [| textPart "done" |] ]

            let reconciler =
                SessionReconciler(SequencedSnapshot([ provisional; terminal ]), fun turn -> turns.Add turn)

            let sessionId = SessionId.create "devops-snapshot-progress"
            bind reconciler sessionId AgentRole.DevOps
            reconciler.HandleSignal(SessionIdle sessionId)
            do! drainMicrotasks 16

            Assert.Single(turns) |> ignore
            Assert.equal(TurnOutcome.TurnCompleted, turns.[0].Outcome)
        }

    [<Fact>]
    let ``Idle received during reconcile is consumed by a trailing pass`` () =
        task {
            let turns = ResizeArray<ReconciledTurn>()
            let unknown = [ msg "u1" "user" (Some "deep-devops") None [||] ]

            let terminal =
                [ msg "u1" "user" (Some "deep-devops") None [||]
                  msg "a1" "assistant" (Some "deep-devops") (Some "stop") [| textPart "done" |] ]

            let snapshot = GatedSnapshot(unknown, terminal)
            let reconciler = SessionReconciler(snapshot, fun turn -> turns.Add turn)
            let sessionId = SessionId.create "devops-trailing-idle"
            bind reconciler sessionId AgentRole.DevOps

            reconciler.HandleSignal(SessionIdle sessionId)
            do! drainMicrotasks 2
            Assert.equal(1, snapshot.Calls)

            // This wake arrives while the first API read is in flight. It must
            // survive that pass rather than being overwritten by cleanup.
            reconciler.HandleSignal(SessionIdle sessionId)
            snapshot.ReleaseFirst()
            do! drainMicrotasks 24

            Assert.Equal(4, snapshot.Calls)
            Assert.Single(turns) |> ignore
            Assert.equal(TurnOutcome.TurnCompleted, turns.[0].Outcome)
        }
