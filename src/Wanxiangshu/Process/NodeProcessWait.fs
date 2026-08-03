namespace Wanxiangshu.Process

open System
open System.Threading
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.AsyncSupport

/// EXEC-011: wait for the real exit; on timeout SIGKILL the process tree and then
/// keep waiting for that real exit.
///
/// Segmented via `Deadline.nextWaitMs` so a huge legal estimate is never clamped
/// to a single 24.8-day timeout.
module NodeProcessWait =

    /// Whether the deadline fired, alongside the process's own exit code.
    ///
    /// The flag is the waiter's knowledge and belongs in its return value, not on
    /// the ChildProcess. It used to live on the exit cell, which let the timeout
    /// path complete that cell with a fabricated `(-1, true)` and return at once —
    /// reporting an exit code the process never produced, while the process itself
    /// was still dying. EXEC-011 requires waiting for the real `onExit` after kill.
    type WaitOutcome = { ExitCode: int; TimedOut: bool }

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

            // Loop until the process's own close/error handler completes the cell.
            // After a kill there is no second deadline: EXEC-011 forbids a competing
            // business timeout, and the OS is the only thing that ends the process.
            while not child.Exited.Value && not cancelled do
                if ct.IsCancellationRequested then
                    cancelled <- true
                elif killSent then
                    let! _ = waitSegment Deadline.MaxTimerWaitMs
                    ()
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
            else
                let! exitCode = child.Exit.Task

                return
                    { ExitCode = exitCode
                      TimedOut = timedOut }
        }
