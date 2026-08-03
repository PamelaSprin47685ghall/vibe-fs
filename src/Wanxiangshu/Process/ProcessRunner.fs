namespace rec Wanxiangshu.Process

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Flow
open Wanxiangshu.Kernel.AsyncSupport

module ProcessRunner =

    open Wanxiangshu.Process.NodeProcessHost
    open Wanxiangshu.Process.NodeProcessWait
    open Wanxiangshu.Process.LargeGate
    open Wanxiangshu.Process.ProcessOutput
    open Wanxiangshu.Process.Deadline
    open Wanxiangshu.Process.Spool

    type ProcessFlow<'a> = Flow<ProcessContext, ProcessError, 'a>
    let private ``process`` = FlowBuilder<ProcessContext, ProcessError>(None)

    let private fromTask (f: ProcessContext -> CancellationToken -> Task<'a>) : ProcessFlow<'a> =
        Flow.create (fun ctx ct ->
            task {
                try
                    let! r = f ctx ct
                    return Ok r
                with
                | _ when ct.IsCancellationRequested -> return Error(ProcessError.ProcessCancelled "Cancelled by token")
                | :? OperationCanceledException when ct.IsCancellationRequested ->
                    return Error(ProcessError.ProcessCancelled "Cancelled by token")
                | ex -> return Error(ProcessError.ExecutionFailed ex.Message)
            })

    /// EXEC-011: the effective deadline is `min(3 × estimate, administrator ceiling)`.
    /// Read from the context so the ceiling is the one the caller configured, not a
    /// second copy of the rule here.
    let private budgetSpan (estimate: ProcessEstimate) (ctx: ProcessContext) =
        ProcessEstimate.effectiveDeadline estimate.EstimatedRuntime ctx.HardLimit

    let private validateEstimate (estimate: ProcessEstimate) : ProcessFlow<unit> =
        ``process`` {
            let (RuntimeSeconds runtime) = estimate.EstimatedRuntime
            let (OutputBytes output) = estimate.EstimatedOutput

            if Double.IsNaN runtime || Double.IsInfinity runtime || runtime <= 0.0 then
                return!
                    Flow.fail (ProcessError.ExecutionFailed "estimated_running_secs must be a finite positive number")
            elif output < 0L then
                return! Flow.fail (ProcessError.ExecutionFailed "estimated_output_bytes must be non-negative")
            elif output > Int64.MaxValue / 3L then
                return! Flow.fail (ProcessError.ExecutionFailed "estimated_output_bytes too large")
            else
                return ()
        }

    let private spawnFlow
        (hostSpawn:
            Command
                -> ProcessContext
                -> (byte[] -> unit)
                -> (byte[] -> unit)
                -> CancellationToken
                -> Task<Result<ChildProcess, string>>)
        (cmd: Command)
        (estimate: ProcessEstimate)
        : ProcessFlow<ChildProcess * OutputCollector> =
        ``process`` {
            let collector = create estimate

            let! spawnResult = fromTask (fun ctx ct -> hostSpawn cmd ctx (addStdout collector) (addStderr collector) ct)

            match spawnResult with
            | Ok child -> return (child, collector)
            | Error reason -> return! Flow.fail (ProcessError.SpawnFailed reason)
        }

    let private runProgram
        (hostSpawn:
            Command
                -> ProcessContext
                -> (byte[] -> unit)
                -> (byte[] -> unit)
                -> CancellationToken
                -> Task<Result<ChildProcess, string>>)
        (cmd: Command)
        (estimate: ProcessEstimate)
        : ProcessFlow<ProcessOutcome> =
        ``process`` {
            do! validateEstimate estimate

            let mutable gateHeld = false

            try
                if estimate.EstimatedMemory = EstimatedMemory.Large then
                    do!
                        fromTask (fun _ ct ->
                            task {
                                do! LargeGate.acquire ct
                                return ()
                            })

                    gateHeld <- true

                let! (child, collector) = spawnFlow hostSpawn cmd estimate

                try
                    let clock = fun () -> DateTimeOffset.UtcNow

                    // The applied budget is returned alongside the outcome so the
                    // reported timeout is the one that actually fired. Recomputing it
                    // for the error would be a second read of the ceiling, and a
                    // TimeoutExceeded carrying a different duration than the deadline
                    // that expired is a lie the operator cannot detect.
                    let! (waited, budget) =
                        fromTask (fun ctx ct ->
                            task {
                                let budget = budgetSpan estimate ctx
                                let! waited = waitForExit child (Deadline.ofBudget (clock ()) budget) ct
                                return waited, budget
                            })

                    if waited.TimedOut then
                        return! Flow.fail (ProcessError.TimeoutExceeded budget)
                    else
                        return buildResult collector waited.ExitCode
                finally
                    try
                        child.Kill()
                    with _ ->
                        ()
            finally
                if gateHeld then
                    LargeGate.release ()
        }

    let runWithHost
        (hostSpawn:
            Command
                -> ProcessContext
                -> (byte[] -> unit)
                -> (byte[] -> unit)
                -> CancellationToken
                -> Task<Result<ChildProcess, string>>)
        (cmd: Command)
        (estimate: ProcessEstimate)
        (ctx: ProcessContext)
        (ct: CancellationToken)
        : Task<Result<ProcessOutcome, ProcessError>> =
        Flow.run ctx ct (runProgram hostSpawn cmd estimate)

    let run
        (cmd: Command)
        (estimate: ProcessEstimate)
        (ctx: ProcessContext)
        (ct: CancellationToken)
        : Task<Result<ProcessOutcome, ProcessError>> =
        runWithHost NodeProcessHost.spawn cmd estimate ctx ct

    /// Test seam that turns a pure launcher (cmd -> (exitCode, stdout, stderr))
    /// into a host so the full process lifecycle can be exercised.
    let runWithLauncher
        (launcher: Command -> CancellationToken -> Task<int * byte[] * byte[]>)
        (cmd: Command)
        (estimate: ProcessEstimate)
        (ctx: ProcessContext)
        (ct: CancellationToken)
        : Task<Result<ProcessOutcome, ProcessError>> =
        let hostSpawn
            (_cmd: Command)
            (_ctx: ProcessContext)
            (onStdout: byte[] -> unit)
            (onStderr: byte[] -> unit)
            (parentCt: CancellationToken)
            : Task<Result<ChildProcess, string>> =
            task {
                let cts = new CancellationTokenSource()
                let exitTcs = TaskCompletionSource<int>()
                let onExited = ResizeArray<unit -> unit>()
                let exited = ref false

                // Mirrors the real host: kill only signals. `exited` is set by the
                // launcher's own completion path below, which stands in for the
                // close handler — a seam that marked the child exited on kill would
                // let waitForExit return before the simulated process finished, and
                // no test could then observe the EXEC-011 kill-then-wait order.
                let kill () =
                    if not cts.IsCancellationRequested then
                        cts.Cancel()

                let child =
                    { Process = null
                      Exit = exitTcs
                      Kill = kill
                      Exited = exited
                      OnExited = onExited }

                let finish (code: int) =
                    exited.Value <- true
                    trySetResult exitTcs code |> ignore
                    NodeProcessHost.notifyExited child

                task {
                    try
                        let! (exitCode, outBytes, errBytes) = launcher cmd cts.Token

                        if parentCt.IsCancellationRequested then
                            exited.Value <- true
                            trySetCanceled exitTcs |> ignore
                        else
                            for chunk in chunkBytes 8192 outBytes do
                                onStdout chunk

                            for chunk in chunkBytes 8192 errBytes do
                                onStderr chunk

                            finish exitCode
                    with
                    // A killed process still reports an exit. Leaving the cell unset
                    // here would hang every waiter, since only the real exit completes
                    // it now.
                    | _ when cts.IsCancellationRequested -> finish -1
                    | _ -> finish -1
                }
                |> ignore

                return Ok child
            }

        runWithHost hostSpawn cmd estimate ctx ct
