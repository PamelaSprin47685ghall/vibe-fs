namespace rec Wanxiangshu.Next.Process

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Flow
open Wanxiangshu.Next.Kernel.AsyncSupport

module ProcessRunner =

    open Wanxiangshu.Next.Process.NodeProcessHost
    open Wanxiangshu.Next.Process.NodeProcessWait
    open Wanxiangshu.Next.Process.LargeGate
    open Wanxiangshu.Next.Process.ProcessOutput
    open Wanxiangshu.Next.Process.Deadline
    open Wanxiangshu.Next.Process.Spool

    type ProcessFlow<'a> = Flow<ProcessContext, ProcessError, 'a>
    let private ``process`` = FlowBuilder<ProcessContext, ProcessError>(None)

    let private fromTask (f: ProcessContext -> CancellationToken -> Task<'a>) : ProcessFlow<'a> =
        Flow.create (fun ctx ct ->
            task {
                try
                    let! r = f ctx ct
                    return Ok r
                with
                | _ when ct.IsCancellationRequested ->
                    return Error(ProcessError.ProcessCancelled "Cancelled by token")
                | :? OperationCanceledException when ct.IsCancellationRequested ->
                    return Error(ProcessError.ProcessCancelled "Cancelled by token")
                | ex ->
                    return Error(ProcessError.ExecutionFailed ex.Message)
            })

    let private budgetSpan (estimate: ProcessEstimate) =
        ProcessEstimate.budget estimate.EstimatedRuntime

    let private validateEstimate (estimate: ProcessEstimate) : ProcessFlow<unit> =
        ``process`` {
            let (RuntimeSeconds runtime) = estimate.EstimatedRuntime
            let (OutputBytes output) = estimate.EstimatedOutput

            if Double.IsNaN runtime || Double.IsInfinity runtime || runtime <= 0.0 then
                return! Flow.fail (ProcessError.ExecutionFailed "estimated_running_secs must be a finite positive number")
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

            let! spawnResult =
                fromTask (fun ctx ct ->
                    hostSpawn cmd ctx (addStdout collector) (addStderr collector) ct)

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

                    let! (exitCode, timedOut) =
                        fromTask (fun _ ct ->
                            let deadline = Deadline.ofBudget (clock ()) (budgetSpan estimate)
                            waitForExit child deadline ct)

                    if timedOut then
                        return! Flow.fail (ProcessError.TimeoutExceeded(budgetSpan estimate))
                    else
                        return buildResult collector exitCode
                finally
                    try child.Kill() with _ -> ()
            finally
                if gateHeld then LargeGate.release ()
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
                let exitTcs = TaskCompletionSource<int * bool>()
                let onExited = ResizeArray<unit -> unit>()
                let exited = ref false

                let kill () =
                    exited.Value <- true

                    if not cts.IsCancellationRequested then
                        cts.Cancel()

                let child =
                    { Process = null
                      Exit = exitTcs
                      Kill = kill
                      Exited = exited
                      OnExited = onExited }

                task {
                    try
                        let! (exitCode, outBytes, errBytes) = launcher cmd cts.Token
                        exited.Value <- true

                        if parentCt.IsCancellationRequested then
                            trySetCanceled exitTcs |> ignore
                        else
                            for chunk in chunkBytes 8192 outBytes do
                                onStdout chunk

                            for chunk in chunkBytes 8192 errBytes do
                                onStderr chunk

                            trySetResult exitTcs (exitCode, false) |> ignore
                            NodeProcessHost.notifyExited child
                    with
                    | _ when cts.IsCancellationRequested ->
                        // Parent cancel or timeout: let waitForExit set the final signal.
                        exited.Value <- true
                    | _ ->
                        exited.Value <- true
                        trySetResult exitTcs (-1, false) |> ignore
                        NodeProcessHost.notifyExited child
                }
                |> ignore

                return Ok child
            }

        runWithHost hostSpawn cmd estimate ctx ct
