namespace Wanxiangshu.Next.Process

open System
open System.Threading
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop

module Runner =

    [<Emit("""
        new Promise((resolve, reject) => {
            const timer = setTimeout(() => reject(new Error('RUNNER_DEADLINE')), $1);
            $0.then(value => { clearTimeout(timer); resolve(value); }, error => { clearTimeout(timer); reject(error); });
        })
    """)>]
    let private withDeadline<'T> (work: Task<'T>) (milliseconds: int) : Task<'T> = jsNative

    let getLargeGateCount () : int = RunnerCore.getLargeGateCount ()

    let acquireLargeGate (ct: CancellationToken) : Task = RunnerCore.acquireLargeGate ct

    let releaseLargeGate () : unit = RunnerCore.releaseLargeGate ()

    let calculateDeadline (now: DateTimeOffset) (est: EstimatedRuntime) : Deadline =
        RunnerPrimitives.calculateDeadline now est

    let killProcessGroup (child: obj) : unit = RunnerPrimitives.killProcessGroup child

    let private outputThreshold (estimate: ProcessEstimate) : int64 =
        let (OutputBytes bytes) = estimate.EstimatedOutput

        if bytes <= 0L then 0L
        elif bytes > Int64.MaxValue / 3L then Int64.MaxValue
        else bytes * 3L

    let execute
        (cmd: Command)
        (estimate: ProcessEstimate)
        (ctx: ProcessContext)
        (ct: CancellationToken)
        : Task<Result<RunnerOutcome, RunnerError>> =
        RunnerCore.execute cmd estimate ctx ct

    /// Execution with injected process launcher port for tests or custom environments.
    let executeWithLauncher
        (launcher: Command -> CancellationToken -> Task<int * byte[] * byte[]>)
        (cmd: Command)
        (estimate: ProcessEstimate)
        (ctx: ProcessContext)
        (ct: CancellationToken)
        : Task<Result<RunnerOutcome, RunnerError>> =
        task {
            let (RuntimeSeconds estSecs) = estimate.EstimatedRuntime

            let budgetSpan = TimeSpan.FromMilliseconds(float (int (3.0 * estSecs * 1000.0)))

            let isLarge = estimate.EstimatedMemory = EstimatedMemory.Large

            if isLarge then
                do! acquireLargeGate ct

            try
                try
                    if ct.IsCancellationRequested then
                        return Error(RunnerError.ProcessCancelled "Cancelled before spawn")
                    else
                        let token = ct

                        try
                            let! (exitCode, stdoutBytes, stderrBytes) =
                                withDeadline (launcher cmd token) (int budgetSpan.TotalMilliseconds)

                            if ct.IsCancellationRequested then
                                return Error(RunnerError.ProcessCancelled "Cancelled by token")
                            else
                                let totalBytes = int64 stdoutBytes.Length + int64 stderrBytes.Length
                                let stdoutStr = Text.Encoding.UTF8.GetString(stdoutBytes)
                                let stderrStr = Text.Encoding.UTF8.GetString(stderrBytes)

                                if totalBytes > outputThreshold estimate then
                                    let combined = Array.append stdoutBytes stderrBytes
                                    let (tempFile, chunks) = Spool.spoolBytesToTempFile combined

                                    return
                                        Ok(RunnerOutcome.Spooled(exitCode, tempFile, totalBytes, chunks.Length, chunks))
                                else
                                    return Ok(RunnerOutcome.Completed(exitCode, stdoutStr, stderrStr, false))
                        with
                        | ex when ex.Message = "RUNNER_DEADLINE" -> return Error(RunnerError.TimeoutExceeded budgetSpan)
                        | :? OperationCanceledException when not ct.IsCancellationRequested ->
                            return Error(RunnerError.TimeoutExceeded budgetSpan)
                        | :? OperationCanceledException ->
                            return Error(RunnerError.ProcessCancelled "Cancelled by token")
                with ex ->
                    return Error(RunnerError.ExecutionFailed ex.Message)
            finally
                if isLarge then
                    releaseLargeGate ()
        }
