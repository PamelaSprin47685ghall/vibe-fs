namespace Wanxiangshu.Next.Process

open System
open System.IO
open System.Text
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel

type EstimatedRuntime = RuntimeSeconds of float
type EstimatedOutput = OutputBytes of int64

[<RequireQualifiedAccess>]
type EstimatedMemory =
    | Medium
    | Large

type ProcessEstimate =
    { EstimatedRuntime: EstimatedRuntime
      EstimatedOutput: EstimatedOutput
      EstimatedMemory: EstimatedMemory }

[<RequireQualifiedAccess>]
type BudgetOutcome =
    | Completed of exitCode: int * stdout: string * stderr: string * spooled: bool
    | Spooled of exitCode: int * spoolPath: string * totalBytes: int64 * chunkCount: int
    | OutputExceeded of bytesWritten: int64 * spoolPath: string option
    | TimeoutExceeded of duration: TimeSpan
    | ExecutionFailed of reason: string

module ProcessBudget =

    let SpoolThresholdBytes: int64 = 200L * 1024L // 200KB

    let private largeGate = new SemaphoreSlim(1, 1)

    let calculateDeadline (now: DateTimeOffset) (RuntimeSeconds estSecs: EstimatedRuntime) : Deadline =
        let budgetSecs = 3.0 * estSecs
        Deadline.ofBudget now (TimeSpan.FromSeconds budgetSecs)

    let getLargeGateCount () : int = largeGate.CurrentCount

    let acquireLargeGate (ct: CancellationToken) : Task = largeGate.WaitAsync(ct)

    let releaseLargeGate () : unit =
        try
            largeGate.Release() |> ignore
        with _ ->
            ()

    let executeWithBudget
        (cmd: Command)
        (estimate: ProcessEstimate)
        (ctx: ProcessContext)
        (ct: CancellationToken)
        : Task<BudgetOutcome> =
        task {
            let (RuntimeSeconds estSecs) = estimate.EstimatedRuntime
            let (OutputBytes estBytes) = estimate.EstimatedOutput

            let budgetSpan = TimeSpan.FromSeconds(3.0 * estSecs)
            let deadline = Deadline.ofBudget DateTimeOffset.UtcNow budgetSpan
            let cmdWithDeadline = { cmd with Deadline = Some deadline }

            let isLarge = estimate.EstimatedMemory = EstimatedMemory.Large

            if isLarge then
                do! largeGate.WaitAsync(ct)

            try
                let! runResult = ProcessFlows.runFlow ctx ct (ProcessFlows.execute cmdWithDeadline)

                match runResult with
                | Ok procResult ->
                    let stdoutBytes = int64 (Encoding.UTF8.GetByteCount(procResult.Stdout))
                    let stderrBytes = int64 (Encoding.UTF8.GetByteCount(procResult.Stderr))
                    let totalBytes = stdoutBytes + stderrBytes

                    let maxAllowedBytes = 3L * estBytes

                    if totalBytes > maxAllowedBytes || totalBytes > SpoolThresholdBytes then
                        let tempFile = Path.GetTempFileName()
                        let fullOutput = procResult.Stdout + procResult.Stderr
                        File.WriteAllText(tempFile, fullOutput)

                        let chunkSize = int SpoolThresholdBytes

                        let chunkCount =
                            if fullOutput.Length = 0 then
                                0
                            else
                                (fullOutput.Length + chunkSize - 1) / chunkSize

                        if totalBytes > maxAllowedBytes then
                            return BudgetOutcome.OutputExceeded(totalBytes, Some tempFile)
                        else
                            return BudgetOutcome.Spooled(procResult.ExitCode, tempFile, totalBytes, chunkCount)
                    else
                        return BudgetOutcome.Completed(procResult.ExitCode, procResult.Stdout, procResult.Stderr, false)

                | Error(ProcessError.Timeout _) -> return BudgetOutcome.TimeoutExceeded budgetSpan

                | Error(ProcessError.ProcessCancelled reason) ->
                    return BudgetOutcome.ExecutionFailed("Cancelled: " + reason)

                | Error(ProcessError.SpawnFailed reason) ->
                    return BudgetOutcome.ExecutionFailed("SpawnFailed: " + reason)

                | Error(ProcessError.ExecutionFailed reason) -> return BudgetOutcome.ExecutionFailed(reason)
            finally
                if isLarge then
                    try
                        largeGate.Release() |> ignore
                    with _ ->
                        ()
        }
