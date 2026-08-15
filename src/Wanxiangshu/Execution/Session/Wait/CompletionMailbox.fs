namespace Wanxiangshu.Execution.Session.Wait

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
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Process

/// EXEC-018: single-join batch ceiling. One source of truth for runtime + wire.
/// Lives in a module: F# namespaces cannot contain values (FS201).
module JoinBatch =
    /// Not [<Literal>]: Fable inlines Literal and drops the JS export.
    let Max = 32
    /// Facade / domain.mjs export alias (JoinBatch_MaxJoinBatch).
    let MaxJoinBatch = Max

/// EXEC-004 / EXEC-018: non-empty batch of join results.
type NonEmptyBatch<'item> = private NonEmptyBatch of head: 'item * tail: 'item list

module NonEmptyBatch =
    let ofHeadTail (head: 'item) (tail: 'item list) = NonEmptyBatch(head, tail)

    let tryOfList =
        function
        | [] -> None
        | head :: tail -> Some(NonEmptyBatch(head, tail))

    let toList (NonEmptyBatch(head, tail)) = head :: tail

    let length (NonEmptyBatch(_, tail)) = 1 + List.length tail

    let map (f: 'a -> 'b) (NonEmptyBatch(head, tail)) = NonEmptyBatch(f head, List.map f tail)

/// EXEC-017 / EXEC-018: why a join wait ended without results. Typed local
/// interruption only. A queued external user message CAN interrupt wait via
/// UserMessageArrived; it does not cancel mailbox/runtime/child.
/// Drain-before-interrupt remains: completions already available still win.
[<RequireQualifiedAccess>]
type JoinInterruptReason =
    /// The current tool call was aborted locally (Esc / tool-call abort).
    | OperatorAbort
    /// An external user message arrived for this session while join waited.
    | UserMessageArrived
    /// The DevOps join budget elapsed without a completion.
    | DeadlineExpired

/// EXEC-017 / EXEC-018: join wait finishes with results or a typed local
/// interrupt. Interrupted is not a ForkError.
type JoinWaitOutcome<'item> =
    | ResultsAvailable of NonEmptyBatch<'item>
    | Interrupted of JoinInterruptReason

/// Wake reason for CompletionMailbox.WaitForSignal (EXEC-018).
type MailboxWakeReason =
    | CompletionMayBeAvailable
    | LocalInterrupt of JoinInterruptReason
    | MailboxCancelled

/// Local interrupt for one join tool-call (EXEC-017).
/// tool-call abort → Signal OperatorAbort only; never runtime.Cancel /
/// mailbox.Cancel.
type JoinInterrupt =
    { Wait: Task<JoinInterruptReason>
      Signal: JoinInterruptReason -> unit }

module JoinInterrupt =
    let create () : JoinInterrupt =
        let tcs =
            TaskCompletionSource<JoinInterruptReason>(TaskCreationOptions.RunContinuationsAsynchronously)

        { Wait = tcs.Task
          Signal = fun reason -> AsyncSupport.trySetResult tcs reason |> ignore }

/// Dual-channel completion mailbox (GREEN-5 / ARCH-002).
/// Agent channel: wake-only (HandleMayHaveChanged) — Journal is agent fact source.
/// PTY channel: physical PtyJoinItem queue — backend onExit sole writer (EXEC-015).
type CompletionMailbox(gate: obj) =
    let agentWakes = Queue<AgentHandleId>()
    let ptyCompletions = Queue<PtyJoinItem>()
    let waiters = Queue<TaskCompletionSource<MailboxWakeReason>>()
    // DSL-MUTABLE: cancellation — mailbox cancelled latch
    let mutable cancelled = false

    let wakeAll (reason: MailboxWakeReason) =
        [ while waiters.Count > 0 do
              yield waiters.Dequeue() ]
        |> List.iter (fun waiter -> AsyncSupport.trySetResult waiter reason |> ignore)

    let enqueueWaiter () =
        let waiter =
            TaskCompletionSource<MailboxWakeReason>(TaskCreationOptions.RunContinuationsAsynchronously)

        waiters.Enqueue waiter
        waiter

    let hasQueued () =
        agentWakes.Count > 0 || ptyCompletions.Count > 0

    let drainQueue (q: Queue<'T>) maxCount =
        [ // DSL-MUTABLE: algorithm-scratch — queue drain counter
          let mutable n = 0

          while n < maxCount && q.Count > 0 do
              n <- n + 1
              yield q.Dequeue() ]

    /// Agent wake only — never carries completion payload.
    member _.PulseAgentHandle(handle: AgentHandleId) =
        lock gate (fun () ->
            if not cancelled then
                agentWakes.Enqueue handle
                wakeAll CompletionMayBeAvailable)

    /// PTY physical result (EXEC-015). Sole mailbox path for PTY facts.
    member _.PublishPtyCompletion(item: PtyJoinItem) =
        lock gate (fun () ->
            if not cancelled then
                ptyCompletions.Enqueue item
                wakeAll CompletionMayBeAvailable)

    /// Spurious wake: drop waiters so outer journal/user race does not pile them up.
    /// Safe — callers always re-drain after wake.
    member _.PulseWake() =
        lock gate (fun () ->
            if cancelled then
                wakeAll MailboxCancelled
            else
                wakeAll CompletionMayBeAvailable)

    /// Wait for Publish/Pulse/Cancel only (no user interrupt). HostForkRuntime races this
    /// against journal change and local interrupt at the outer level.
    member _.WaitForWake() : Task<MailboxWakeReason> =
        let pending =
            lock gate (fun () ->
                if cancelled then Choice1Of2 MailboxCancelled
                elif hasQueued () then Choice1Of2 CompletionMayBeAvailable
                else Choice2Of2(enqueueWaiter ()))

        match pending with
        | Choice1Of2 reason -> Task.FromResult reason
        | Choice2Of2 waiter -> waiter.Task

    /// EXEC-018: wait for completion signal, typed local interrupt, or
    /// permanent cancel.
    member this.WaitForSignal(interrupt: Task<JoinInterruptReason>) : Task<MailboxWakeReason> =
        // Race wake vs interrupt without nested task{} (dsl-ownership raw-task budget).
        // kind=0 + reason = wake; kind=1 + reason = local interrupt.
        let wakeTask: Task<obj> =
            emitJsExpr (this.WaitForWake()) "$0.then(function (r) { return { kind: 0, reason: r }; })"

        let interruptTask: Task<obj> =
            emitJsExpr interrupt "$0.then(function (r) { return { kind: 1, reason: r }; })"

        task {
            let! winner = emitJsExpr (wakeTask, interruptTask) "Promise.race([$0, $1])": Task<obj>

            let kind: int = emitJsExpr winner "$0.kind"

            if kind = 0 then
                return emitJsExpr winner "$0.reason": MailboxWakeReason
            else
                // Drop mailbox waiter if still pending (local interrupt won).
                this.PulseWake()
                let reason: JoinInterruptReason = emitJsExpr winner "$0.reason"
                return LocalInterrupt reason
        }

    /// Drain agent wake tokens (no payload). Callers re-read Journal after wake.
    member _.DrainAgentWakes(maxCount: int) : AgentHandleId list =
        if maxCount <= 0 then
            []
        else
            lock gate (fun () -> drainQueue agentWakes maxCount)

    /// Bounded drain of queued PTY facts (EXEC-015).
    member _.DrainPtyCompletions(maxCount: int) : PtyJoinItem list =
        if maxCount <= 0 then
            []
        else
            lock gate (fun () -> drainQueue ptyCompletions maxCount)

    /// Lifecycle termination only — not tool-call abort (EXEC-017).
    member _.Cancel() =
        lock gate (fun () ->
            if cancelled then
                false
            else
                cancelled <- true
                wakeAll MailboxCancelled
                true)

    member _.PendingCount = lock gate (fun () -> agentWakes.Count + ptyCompletions.Count)

    member _.PendingPtyCount = lock gate (fun () -> ptyCompletions.Count)
    member _.PendingAgentWakeCount = lock gate (fun () -> agentWakes.Count)
    member _.IsCancelled = lock gate (fun () -> cancelled)
