namespace Wanxiangshu.Process

open System
open System.Threading
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.AsyncSupport

/// EXEC-011: wait for the real exit; on timeout SIGKILL the process tree and then
/// wait for that real exit within a finite kill-ack grace.
///
/// Segmented via `Deadline.nextWaitMs` so a huge legal estimate is never clamped
/// to a single 24.8-day timeout. Kill-ack is a separate management bound so a
/// hung close (e.g. pipe held by a grandchild) cannot pin the waiter forever.
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

    /// Management bound after SIGKILL for the OS close/onExit path to settle.
    /// Not a second business deadline on the estimate: the business budget already
    /// fired. If the process is still not exited when this elapses, return
    /// TimedOut with ExitCode=-1 (unknown), never fake a successful exit.
    /// Plain `let` so layer-1 tests can read the export (same pattern as Deadline.MaxTimerWaitMs).
    let KillAckGraceMs = 5_000

    let waitForExit
        (child: NodeProcessHost.ChildProcess)
        (deadline: Deadline)
        (ct: CancellationToken)
        : Task<WaitOutcome> =
        let clock = fun () -> DateTimeOffset.UtcNow

        /// One `setTimeout` race between "the child exited" and "ms elapsed".
        /// True when the timer won.
        let waitSegment (ms: int) =
            task {
                let settled = TaskCompletionSource<bool>()
                let mutable timerId = None
                let mutable timerCleared = false

                let clearTimer () =
                    if not timerCleared then
                        timerCleared <- true

                        match timerId with
                        | Some id -> emitJsExpr id "clearTimeout($0)" |> ignore
                        | None -> ()

                let onExited _ =
                    clearTimer ()
                    trySetResult settled false |> ignore

                child.OnExited.Add onExited

                if child.Exited.Value then
                    clearTimer ()
                    child.OnExited.Remove onExited |> ignore
                    return false
                else
                    let onTimeout _ =
                        clearTimer ()
                        trySetResult settled true |> ignore

                    timerId <- Some(emitJsExpr (ms, onTimeout) "setTimeout($1, $0)")

                    use _ =
                        ct.Register(fun () ->
                            clearTimer ()
                            trySetResult settled false |> ignore)

                    try
                        return! settled.Task
                    finally
                        clearTimer ()
                        child.OnExited.Remove onExited |> ignore
            }

        task {
            let mutable timedOut = false
            let mutable cancelled = false
            let mutable killSent = false
            let mutable killAckExpired = false

            // Business budget → kill → kill-ack grace. Never wait MaxTimerWaitMs
            // after kill (that hung callers when close never arrived).
            while not child.Exited.Value && not cancelled && not killAckExpired do
                if ct.IsCancellationRequested then
                    cancelled <- true
                elif killSent then
                    let! fired = waitSegment KillAckGraceMs

                    if fired && not child.Exited.Value then
                        killAckExpired <- true
                        timedOut <- true
                else
                    match Deadline.nextWaitMs clock deadline with
                    | 0 ->
                        timedOut <- true
                        killSent <- true
                        child.Kill()
                    | ms ->
                        let! fired = waitSegment ms

                        if fired then
                            timedOut <- true
                            killSent <- true
                            child.Kill()

            if cancelled && not child.Exited.Value then
                // A cancelled wait still kills the tree, but its caller is no longer
                // waiting for the code, so completing the cell is left to the real
                // handler. Faking it here is what made "cancelled" and "exited -1"
                // indistinguishable downstream.
                child.Kill()
                return raise (OperationCanceledException(ct))
            elif killAckExpired && not child.Exited.Value then
                // Kill unconfirmed: do not hang on Exit.Task; report TimedOut with
                // unknown code. Real close may still arrive later on the process.
                return { ExitCode = -1; TimedOut = true }
            else
                let! exitCode = child.Exit.Task

                return
                    { ExitCode = exitCode
                      TimedOut = timedOut }
        }
