namespace rec Wanxiangshu.Process

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.AsyncSupport

module ProcessRunner =

    open Wanxiangshu.Process.NodeProcessHost
    open Wanxiangshu.Process.NodeProcessWait
    open Wanxiangshu.Process.LargeGate
    open Wanxiangshu.Process.ProcessOutput
    open Wanxiangshu.Process.Deadline
    open Wanxiangshu.Process.Spool

    /// EXEC-011: the effective deadline is `min(3 × estimate, administrator ceiling)`.
    /// Read from the context so the ceiling is the one the caller configured, not a
    /// second copy of the rule here.
    let private budgetSpan (estimate: ProcessEstimate) (ctx: ProcessContext) =
        ProcessEstimate.effectiveDeadline estimate.EstimatedRuntime ctx.HardLimit

    let private validateEstimate (estimate: ProcessEstimate) : Result<unit, ProcessError> =
        let (RuntimeSeconds runtime) = estimate.EstimatedRuntime
        let (OutputBytes output) = estimate.EstimatedOutput

        if Double.IsNaN runtime || Double.IsInfinity runtime || runtime <= 0.0 then
            Error(ProcessError.ExecutionFailed "deadline_seconds must be a finite positive number")
        elif output < 0L then
            Error(ProcessError.ExecutionFailed "output_budget_bytes must be non-negative")
        else
            Ok()

    let private spawnHost
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
        : Task<Result<ChildProcess * OutputCollector, ProcessError>> =
        task {
            try
                let collector = create estimate
                let! spawnResult = hostSpawn cmd ctx (addStdout collector) (addStderr collector) ct

                match spawnResult with
                | Ok child -> return Ok(child, collector)
                | Error reason -> return Error(ProcessError.SpawnFailed reason)
            with
            | _ when ct.IsCancellationRequested -> return Error(ProcessError.ProcessCancelled "Cancelled by token")
            | :? OperationCanceledException when ct.IsCancellationRequested ->
                return Error(ProcessError.ProcessCancelled "Cancelled by token")
            | ex -> return Error(ProcessError.ExecutionFailed ex.Message)
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
        (ctx: ProcessContext)
        (ct: CancellationToken)
        : Task<Result<ProcessOutcome, ProcessError>> =
        task {
            match validateEstimate estimate with
            | Error e -> return Error e
            | Ok() ->
                // DSL-MUTABLE: resource — LargeGate permit ownership flag (release-on-exit)
                let mutable gateHeld = false

                try
                    if estimate.EstimatedMemory = EstimatedMemory.Large then
                        do! LargeGate.acquire ct
                        gateHeld <- true

                    match! spawnHost hostSpawn cmd estimate ctx ct with
                    | Error e -> return Error e
                    | Ok(child, collector) ->
                        let clock = fun () -> DateTimeOffset.UtcNow

                        // The applied budget is returned alongside the outcome so the
                        // reported timeout is the one that actually fired. Recomputing it
                        // for the error would be a second read of the ceiling, and a
                        // TimeoutExceeded carrying a different duration than the deadline
                        // that expired is a lie the operator cannot detect.
                        let budget = budgetSpan estimate ctx
                        let! waited = waitForExit child (Deadline.ofBudget (clock ()) budget) ct

                        try
                            child.Kill()
                        with _ ->
                            ()

                        if waited.TimedOut then
                            return Error(ProcessError.TimeoutExceeded budget)
                        else
                            return Ok(buildResult collector waited.ExitCode)
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
        runProgram hostSpawn cmd estimate ctx ct

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
