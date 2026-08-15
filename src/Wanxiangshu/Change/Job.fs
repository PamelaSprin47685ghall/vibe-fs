namespace Wanxiangshu.Change

open Wanxiangshu.Git
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Strength.Persistence
open Wanxiangshu.Strength.Replica

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

/// One Manager execution plus the worktree resource it owns through publish.
///
/// Exactly the fields ORCH-006 persists, so a recovered job and a fresh one are the
/// same value. There is deliberately no `Prompt`: `ManagerJobCreated` does not record
/// one, so nothing after a restart may depend on it. The initial prompt goes straight
/// to `StartManager`, and conflict resumption sends only the conflict instruction to a
/// session that already holds the task (PROMPT-003).
///
/// No completion Task either. ORCH-006 requires `ManagerJobCreated` to carry the
/// Manager's `SessionId`, which exists only after the fork, so the previous shape —
/// start the Manager in the constructor, then persist — could only write the job fact
/// after the Manager had already begun. A crash in that window left a live Manager with
/// no durable job. Starting and awaiting are now two steps the program sequences, with
/// the fact written between them.
type ManagerJob =
    { JobId: ManagerJobId
      ManagerSessionId: SessionId
      ManagerAgent: string
      TargetRef: TargetRef
      Worktree: WorktreeResource }

    member this.Handle =
        { JobId = this.JobId
          WorktreePath = this.Worktree.Path }

/// Completion mailbox for published verdicts. It only stores the final
/// OrchestratorVerdict after FF; the publish program runs as an owned task.
/// EXEC-019: FIFO batch drain, MaxJoinBatch ceiling.
type VerdictMailbox() =
    let gate = obj ()
    let verdicts = Queue<OrchestratorVerdict>()
    let waiters = Queue<TaskCompletionSource<unit>>()
    // DSL-MUTABLE: single-flight — count of in-flight manager jobs under the gate
    let mutable active = 0

    /// Wake one waiter (signal only). Fact source remains the verdicts queue.
    let wakeOne () =
        if waiters.Count > 0 then
            AsyncSupport.trySetResult (waiters.Dequeue()) () |> ignore

    member _.StartJob() =
        lock gate (fun () -> active <- active + 1)

    member _.Publish(verdict: OrchestratorVerdict) =
        lock gate (fun () ->
            active <- max 0 (active - 1)
            verdicts.Enqueue verdict
            wakeOne ())

    /// Non-blocking FIFO drain (up to maxCount). Remaining stay queued.
    member _.DrainAvailable(maxCount: int) : OrchestratorVerdict list =
        let drain () =
            lock gate (fun () ->
                // DSL-MUTABLE: algorithm-scratch — drain-loop counter
                [ let mutable n = 0

                  while n < maxCount && verdicts.Count > 0 do
                      n <- n + 1
                      yield verdicts.Dequeue() ])

        if maxCount <= 0 then [] else drain ()

    member _.HasActive = lock gate (fun () -> active > 0)
    member _.PendingCount = lock gate (fun () -> verdicts.Count)

    /// Wait until a verdict is enqueued or mailbox is idle with empty queue.
    member private _.awaitSignal() : Task<unit> =
        let pending =
            lock gate (fun () ->
                if verdicts.Count > 0 || active = 0 then
                    Choice1Of2()
                else
                    let waiter =
                        TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

                    waiters.Enqueue waiter
                    Choice2Of2 waiter)

        match pending with
        | Choice1Of2() -> Task.FromResult()
        | Choice2Of2 waiter ->
            let descriptor =
                DiagnosticWait.create
                    "manager-job-completion"
                    (CausalOwner.create "OrchestratorJob" [])
                    [ "mailbox", "verdict" ]
                    (WorkflowProducer(CausalOwner.create "ManagerWorkflow" []))
                    [ WaitEscape.ProcessLifetime ]
                    "VerdictMailbox.awaitSignal"

            CausalAwait.awaitTask CausalWaitHub.observer descriptor waiter.Task

    /// Remove this join's waiter from the queue (interrupt won). Completing alone is not enough:
    /// a completed TCS left in the queue would absorb the next Publish wake.
    member private _.dropWaiter(waiter: TaskCompletionSource<unit>) =
        lock gate (fun () ->
            let kept =
                [ while waiters.Count > 0 do
                      let poppedWaiter = waiters.Dequeue()

                      if not (obj.ReferenceEquals(poppedWaiter, waiter)) then
                          yield poppedWaiter ]

            for keptWaiter in kept do
                waiters.Enqueue keptWaiter

            AsyncSupport.trySetResult waiter () |> ignore)

    /// EXEC-019: first verdict wakes; immediately drain backlog; cap MaxJoinBatch; FIFO.
    member this.TryJoinBatch(maxCount: int) : Task<OrchestratorVerdict list> =
        let joinWhenCapped (cap: int) =
            match this.DrainAvailable cap with
            | _ :: _ as ready -> Task.FromResult ready
            | [] ->
                task {
                    do! this.awaitSignal ()
                    return this.DrainAvailable cap
                }

        let cap = min (max 0 maxCount) JoinBatch.Max
        if cap <= 0 then Task.FromResult [] else joinWhenCapped cap

    /// Compatibility single-result join.
    member this.TryJoin() : Task<OrchestratorVerdict option> =
        task {
            let! batch = this.TryJoinBatch 1
            return List.tryHead batch
        }

    member private _.signalOrEnqueue(waiter: TaskCompletionSource<unit>) =
        lock gate (fun () ->
            if verdicts.Count > 0 || active = 0 then
                AsyncSupport.trySetResult waiter () |> ignore
            else
                waiters.Enqueue waiter)

    member private this.resolveEmptyDrain
        (waiter: TaskCompletionSource<unit>)
        (kind: int)
        (winner: obj)
        =
        if kind = 0 then
            // Idle wake with empty queue → Empty sentinel (legacy JoinPublished).
            ResultsAvailable(NonEmptyBatch.ofHeadTail OrchestratorVerdict.Empty [])
        else
            this.dropWaiter waiter
            let reason: JoinInterruptReason = emitJsExpr winner "$0.reason"
            Interrupted reason

    member private this.resolveAfterDrain
        (waiter: TaskCompletionSource<unit>)
        (cap: int)
        (winner: obj)
        =
        let after = this.DrainAvailable cap

        match NonEmptyBatch.tryOfList after with
        | Some batch -> ResultsAvailable batch
        | None ->
            let kind: int = emitJsExpr winner "$0.kind"
            this.resolveEmptyDrain waiter kind winner

    /// EXEC-017 / EXEC-019: drain-first → race wait/interrupt → re-drain.
    /// A local operator abort is not a publish failure.
    member this.JoinAvailable
        (maxCount: int, interrupt: Task<JoinInterruptReason>)
        : Task<JoinWaitOutcome<OrchestratorVerdict>> =
        let cap = min (max 0 maxCount) JoinBatch.Max
        let ready = this.DrainAvailable cap

        match NonEmptyBatch.tryOfList ready with
        | Some batch -> Task.FromResult(ResultsAvailable batch)
        | None when not this.HasActive ->
            Task.FromResult(ResultsAvailable(NonEmptyBatch.ofHeadTail OrchestratorVerdict.Empty []))
        | None ->
            let waiter =
                TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

            this.signalOrEnqueue waiter

            // Race arms as int tags — no nested task{} (dsl-ownership raw-task budget).
            let waitTask: Task<obj> =
                emitJsExpr waiter.Task "$0.then(function () { return { kind: 0 }; })"

            let interruptTask: Task<obj> =
                emitJsExpr interrupt "$0.then(function (r) { return { kind: 1, reason: r }; })"

            let descriptor =
                DiagnosticWait.create
                    "orchestrator-manager-join"
                    (CausalOwner.create "OrchestratorJob" [])
                    [ "mailbox", "verdict" ]
                    (WorkflowProducer(CausalOwner.create "ManagerWorkflow" []))
                    [ WaitEscape.CancelledBy(CausalOwner.create "orchestrator-join-interrupt" [])
                      WaitEscape.ProcessLifetime ]
                    "VerdictMailbox.JoinAvailable"

            task {
                let! winner =
                    CausalAwait.awaitTask
                        CausalWaitHub.observer
                        descriptor
                        (emitJsExpr (waitTask, interruptTask) "Promise.race([$0, $1])": Task<obj>)

                // Always re-drain first (EXEC-018).
                return this.resolveAfterDrain waiter cap winner
            }
