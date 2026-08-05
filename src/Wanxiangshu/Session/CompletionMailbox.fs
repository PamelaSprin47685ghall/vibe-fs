namespace Wanxiangshu.Session

open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
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

/// EXEC-017 / EXEC-018: join wait finishes with results or user-message interrupt.
/// InterruptedByUserMessage is not a ForkError.
type JoinWaitOutcome<'item> =
    | ResultsAvailable of NonEmptyBatch<'item>
    | InterruptedByUserMessage

/// Wake reason for CompletionMailbox.WaitForSignal (EXEC-018).
type MailboxWakeReason =
    | CompletionMayBeAvailable
    | UserInterrupted
    | MailboxCancelled

/// Local interrupt for one join tool-call (EXEC-017).
/// tool-call abort → Signal only; never runtime.Cancel / mailbox.Cancel.
type JoinInterrupt =
    { Wait: Task<unit>
      Signal: unit -> unit }

module JoinInterrupt =
    let create () : JoinInterrupt =
        let tcs =
            TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

        { Wait = tcs.Task
          Signal = fun () -> AsyncSupport.trySetResult tcs () |> ignore }

/// Shared completion mailbox: queue + wake signal.
/// Agent completion: wake only; durable projection is fact source (ARCH-002).
/// PTY completion: queue holds EXEC-015 facts (backend onExit sole writer).
type CompletionMailbox(gate: obj, hasActive: unit -> bool) =
    let completions = Queue<RunCompletion>()
    let waiters = Queue<TaskCompletionSource<MailboxWakeReason>>()
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

    /// Enqueue completion and wake every signal waiter (Publish is notify, not deliver).
    member _.Publish(completion: RunCompletion) =
        lock gate (fun () ->
            if not cancelled then
                completions.Enqueue completion
                wakeAll CompletionMayBeAvailable)

    /// Spurious wake: drop waiters so outer journal/user race does not pile them up.
    /// Safe — callers always re-drain after wake.
    member _.PulseWake() =
        lock gate (fun () ->
            if cancelled then
                wakeAll MailboxCancelled
            else
                wakeAll CompletionMayBeAvailable)

    /// Wait for Publish/Cancel only (no user interrupt). HostForkRuntime races this
    /// against journal change and local interrupt at the outer level.
    member _.WaitForWake() : Task<MailboxWakeReason> =
        let pending =
            lock gate (fun () ->
                if cancelled then
                    Choice1Of2 MailboxCancelled
                elif completions.Count > 0 then
                    Choice1Of2 CompletionMayBeAvailable
                else
                    Choice2Of2(enqueueWaiter ()))

        match pending with
        | Choice1Of2 reason -> Task.FromResult reason
        | Choice2Of2 waiter -> waiter.Task

    /// EXEC-018: wait for completion signal, user interrupt, or permanent cancel.
    member this.WaitForSignal(interrupt: Task<unit>) : Task<MailboxWakeReason> =
        // Race wake vs interrupt without nested task{} (dsl-ownership raw-task budget).
        // kind=0 + reason = wake; kind=1 = user interrupt.
        let wakeTask: Task<obj> =
            emitJsExpr (this.WaitForWake()) "$0.then(function (r) { return { kind: 0, reason: r }; })"

        let interruptTask: Task<obj> =
            emitJsExpr interrupt "$0.then(function () { return { kind: 1 }; })"

        task {
            let! winner = emitJsExpr (wakeTask, interruptTask) "Promise.race([$0, $1])": Task<obj>

            let kind: int = emitJsExpr winner "$0.kind"

            if kind = 0 then
                return emitJsExpr winner "$0.reason": MailboxWakeReason
            else
                // Drop mailbox waiter if still pending (user interrupt won).
                this.PulseWake()
                return UserInterrupted
        }

    /// Bounded drain of queued completions (PTY facts / agent wake payloads).
    member _.DrainAvailable(maxCount: int) : RunCompletion list =
        if maxCount <= 0 then
            []
        else
            lock gate (fun () ->
                [ let mutable n = 0

                  while n < maxCount && completions.Count > 0 do
                      n <- n + 1
                      yield completions.Dequeue() ])

    /// Compatibility: wait once, drain at most one completion (or ForkError).
    member this.Join(?timeoutMs: int) : Task<Result<RunCompletion, ForkError>> =
        let budgetMs = defaultArg timeoutMs 600_000

        let rec loop (remainingMs: int) =
            task {
                let drained = this.DrainAvailable 1

                match drained with
                | completion :: _ -> return Ok completion
                | [] ->
                    if lock gate (fun () -> cancelled) then
                        return Error ForkError.Cancelled
                    elif not (hasActive ()) then
                        return Error ForkError.NothingToJoin
                    elif remainingMs <= 0 then
                        return Error ForkError.TimedOut
                    else
                        let interrupt = PtyTiming.timerTask remainingMs
                        let started = System.DateTimeOffset.UtcNow
                        let! reason = this.WaitForSignal interrupt
                        let elapsed = int (System.DateTimeOffset.UtcNow - started).TotalMilliseconds
                        let next = max 0 (remainingMs - max 0 elapsed)

                        match reason with
                        | MailboxCancelled -> return Error ForkError.Cancelled
                        | UserInterrupted ->
                            match this.DrainAvailable 1 with
                            | completion :: _ -> return Ok completion
                            | [] -> return Error ForkError.TimedOut
                        | CompletionMayBeAvailable -> return! loop next
            }

        loop budgetMs

    /// Lifecycle termination only — not tool-call abort (EXEC-017).
    member _.Cancel() =
        lock gate (fun () ->
            if cancelled then
                false
            else
                cancelled <- true
                wakeAll MailboxCancelled
                true)

    member _.PendingCount = lock gate (fun () -> completions.Count)
    member _.IsCancelled = lock gate (fun () -> cancelled)
