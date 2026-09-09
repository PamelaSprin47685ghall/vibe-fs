namespace Wanxiangshu.Change

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Git

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
type VerdictMailbox(observer: IWaitObserver) =
    let gate = obj ()
    // DSL-MUTABLE: resource — verdict FIFO queue
    let verdicts = Queue<OrchestratorVerdict>()
    // DSL-MUTABLE: resource — waiter queue
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

            CausalAwait.awaitTask observer descriptor waiter.Task

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

    member private _.signalOrEnqueue(waiter: TaskCompletionSource<unit>) =
        lock gate (fun () ->
            if verdicts.Count > 0 || active = 0 then
                AsyncSupport.trySetResult waiter () |> ignore
            else
                waiters.Enqueue waiter)

    member private this.resolveEmptyDrain
        (waiter: TaskCompletionSource<unit>)
        (winner: Choice<unit, JoinInterruptReason>)
        : JoinWaitOutcome<OrchestratorVerdict> =
        match winner with
        | Choice1Of2() ->
            // Idle wake with empty queue → Empty sentinel.
            ResultsAvailable(NonEmptyBatch.ofHeadTail OrchestratorVerdict.Empty [])
        | Choice2Of2 reason ->
            this.dropWaiter waiter
            Interrupted reason

    member private this.resolveAfterDrain
        (waiter: TaskCompletionSource<unit>)
        (cap: int)
        (winner: Choice<unit, JoinInterruptReason>)
        : JoinWaitOutcome<OrchestratorVerdict> =
        match NonEmptyBatch.tryOfList (this.DrainAvailable cap) with
        | Some batch -> ResultsAvailable batch
        | None -> this.resolveEmptyDrain waiter winner

    /// Drain-first → race wait/interrupt → re-drain.
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

            // Signal arm first: simultaneous settlement favors the verdict signal.
            let signalOf () : Choice<unit, JoinInterruptReason> = Choice1Of2()

            let interruptOf (reason: JoinInterruptReason) : Choice<unit, JoinInterruptReason> = Choice2Of2 reason

            let signalArm: Task<Choice<unit, JoinInterruptReason>> =
                emitJsExpr (waiter.Task, signalOf) "$0.then($1)"

            let interruptArm: Task<Choice<unit, JoinInterruptReason>> =
                emitJsExpr (interrupt, interruptOf) "$0.then($1)"

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
                let! (winner: Choice<unit, JoinInterruptReason>) =
                    CausalAwait.awaitTask
                        observer
                        descriptor
                        (emitJsExpr (signalArm, interruptArm) "Promise.race([$0, $1])")

                // Always re-drain first: a verdict that arrived before the race
                // settled takes precedence over the interrupt.
                return this.resolveAfterDrain waiter cap winner
            }
