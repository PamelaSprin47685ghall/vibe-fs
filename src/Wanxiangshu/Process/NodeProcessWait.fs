namespace Wanxiangshu.Process

open System
open System.Threading
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.AsyncSupport

/// EXEC-011: wait for the real exit; on timeout SIGKILL the process tree and then
/// wait for that real exit within a finite kill-ack grace.
///
/// The process lifecycle is expressed directly as F# CE:
///   wait for natural exit or deadline
///   -> on deadline, kill
///   -> wait for kill acknowledgement
///   -> return real exit or kill-unconfirmed.
///
/// No mutable booleans track the current stage; the stage is the current function.
module NodeProcessWait =

    /// Whether the deadline fired, alongside the process's own exit code.
    ///
    /// The flag is the waiter's knowledge and belongs in its return value, not on
    /// the ChildProcess. It used to live on the exit cell, which let the timeout
    /// path complete that cell with a fabricated `(-1, true)` and return at once —
    /// reporting an exit code the process never produced, while the process itself
    /// was still dying. EXEC-011 requires waiting for the real `onExit` after kill,
    /// bounded by `KillAckGraceMs` so kill-unconfirmed does not hang callers.
    type WaitOutcome = { ExitCode: int; TimedOut: bool }

    /// Who won the first race: natural exit, business deadline, or cancellation.
    /// Three distinct events must not share a bool — cancellation is not exit.
    type private WaitSignal =
        | ProcessExited
        | TimerElapsed
        | Cancelled

    /// Result of waiting for the process to exit on its own before the deadline.
    type private ExitOrDeadline =
        | ExitedBeforeDeadline of exitCode: int
        | DeadlineReached
        | WaitCancelled

    /// Result of waiting for the process to exit after SIGKILL.
    type private KillResult =
        | ExitedAfterKill of exitCode: int
        | KillNotAcknowledged
        | KillWaitCancelled

    /// Management bound after SIGKILL for the OS close/onExit path to settle.
    /// Not a second business deadline on the estimate: the business budget already
    /// fired. If the process is still not exited when this elapses, return
    /// TimedOut with ExitCode=-1 (unknown), never fake a successful exit.
    /// Plain `let` so layer-1 tests can read the export (same pattern as Deadline.MaxTimerWaitMs).
    let KillAckGraceMs = 1_000

    /// One race among process exit, timer, and cancellation.
    /// Does not Kill — only reports which signal arrived first.
    ///
    /// The mutable `timerId` and `timerCleared` are physical resource ownership
    /// (JS timer handle and idempotency guard), not business stage.
    let private waitForSignal (child: NodeProcessHost.ChildProcess) (ms: int) (ct: CancellationToken) =
        task {
            let settled = TaskCompletionSource<WaitSignal>()
            // DSL-MUTABLE: resource — JS timer handle
            let mutable timerId = None
            // DSL-MUTABLE: resource — clearTimeout idempotency
            let mutable timerCleared = false

            let clearTimer () =
                if not timerCleared then
                    timerCleared <- true

                    match timerId with
                    | Some id -> emitJsExpr id "clearTimeout($0)" |> ignore
                    | None -> ()

            let onExited _ =
                clearTimer ()
                trySetResult settled ProcessExited |> ignore

            child.OnExited.Add onExited

            if child.Exited.Value then
                clearTimer ()
                child.OnExited.Remove onExited |> ignore
                return ProcessExited
            else
                let onTimeout _ =
                    clearTimer ()
                    trySetResult settled TimerElapsed |> ignore

                timerId <- Some(emitJsExpr (ms, onTimeout) "setTimeout($1, $0)")

                use _ =
                    ct.Register(fun () ->
                        clearTimer ()
                        trySetResult settled Cancelled |> ignore)

                try
                    return! settled.Task
                finally
                    clearTimer ()
                    child.OnExited.Remove onExited |> ignore
        }

    /// Wait for the child to exit on its own, or for the business deadline to fire.
    /// Cancellation is a third outcome — never folded into ProcessExited.
    let private awaitExitOrDeadline (child: NodeProcessHost.ChildProcess) (deadline: Deadline) (ct: CancellationToken) =
        task {
            let clock = fun () -> DateTimeOffset.UtcNow

            if child.Exited.Value then
                let! exitCode = child.Exit.Task
                return ExitedBeforeDeadline exitCode
            else
                match Deadline.nextWaitMs clock deadline with
                | 0 ->
                    // Budget already exhausted.
                    return DeadlineReached
                | ms ->
                    match! waitForSignal child ms ct with
                    | ProcessExited ->
                        let! exitCode = child.Exit.Task
                        return ExitedBeforeDeadline exitCode

                    | TimerElapsed -> return DeadlineReached

                    | Cancelled -> return WaitCancelled
        }

    /// After kill, wait for the real exit up to `KillAckGraceMs`.
    /// Cancellation during kill-ack is KillWaitCancelled — not a hang on Exit.Task.
    let private awaitKillAcknowledgement (child: NodeProcessHost.ChildProcess) (ct: CancellationToken) =
        task {
            if child.Exited.Value then
                let! exitCode = child.Exit.Task
                return ExitedAfterKill exitCode
            else
                match! waitForSignal child KillAckGraceMs ct with
                | ProcessExited ->
                    let! exitCode = child.Exit.Task
                    return ExitedAfterKill exitCode

                | TimerElapsed -> return KillNotAcknowledged

                | Cancelled -> return KillWaitCancelled
        }

    /// Public entry: wait for real exit, then kill, then wait for kill-ack.
    /// Cancellation is propagated as `OperationCanceledException`; the process is
    /// still killed before the exception is raised. Kill only happens in this
    /// top-level workflow (entry cancel, mid-wait cancel, deadline), never inside
    /// waitForSignal.
    let waitForExit
        (child: NodeProcessHost.ChildProcess)
        (deadline: Deadline)
        (ct: CancellationToken)
        : Task<WaitOutcome> =
        task {
            if ct.IsCancellationRequested then
                child.Kill()
                raise (OperationCanceledException(ct))

            match! awaitExitOrDeadline child deadline ct with
            | ExitedBeforeDeadline exitCode ->
                return
                    { ExitCode = exitCode
                      TimedOut = false }

            | WaitCancelled ->
                child.Kill()
                return raise (OperationCanceledException(ct))

            | DeadlineReached ->
                if child.Exited.Value then
                    let! exitCode = child.Exit.Task

                    return
                        { ExitCode = exitCode
                          TimedOut = false }
                else
                    child.Kill()

                    match! awaitKillAcknowledgement child ct with
                    | ExitedAfterKill exitCode -> return { ExitCode = exitCode; TimedOut = true }

                    | KillNotAcknowledged ->
                        // Kill unconfirmed: do not hang on Exit.Task; report TimedOut with
                        // unknown code. Real close may still arrive later on the process.
                        return { ExitCode = -1; TimedOut = true }

                    | KillWaitCancelled ->
                        // Already killed above; only propagate cancellation.
                        child.Kill()
                        return raise (OperationCanceledException(ct))
        }
