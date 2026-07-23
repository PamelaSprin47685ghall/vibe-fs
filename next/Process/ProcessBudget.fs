namespace Wanxiangshu.Next.Process

open System
open System.Threading
open System.Threading.Tasks

[<RequireQualifiedAccess>]
type BudgetOutcome =
    | Completed of exitCode: int * stdout: string * stderr: string * spooled: bool
    | Spooled of exitCode: int * spoolPath: string * totalBytes: int64 * chunkCount: int
    | OutputExceeded of bytesWritten: int64 * spoolPath: string option
    | TimeoutExceeded of duration: TimeSpan
    | ExecutionFailed of reason: string

module ProcessBudget =

    let SpoolThresholdBytes: int64 = int64 Spool.ChunkSizeBytes

    let calculateDeadline (now: DateTimeOffset) (est: EstimatedRuntime) : Deadline = Runner.calculateDeadline now est

    let getLargeGateCount () : int = Runner.getLargeGateCount ()

    let acquireLargeGate (ct: CancellationToken) : Task = Runner.acquireLargeGate ct

    let releaseLargeGate () : unit = Runner.releaseLargeGate ()

    let executeWithBudget
        (cmd: Command)
        (estimate: ProcessEstimate)
        (ctx: ProcessContext)
        (ct: CancellationToken)
        : Task<BudgetOutcome> =
        task {
            let! res = Runner.execute cmd estimate ctx ct

            match res with
            | Ok(RunnerOutcome.Completed(code, stdout, stderr, spooled)) ->
                return BudgetOutcome.Completed(code, stdout, stderr, spooled)
            | Ok(RunnerOutcome.Spooled(code, path, totalBytes, chunkCount, _chunks)) ->
                return BudgetOutcome.Spooled(code, path, totalBytes, chunkCount)
            | Ok(RunnerOutcome.OutputExceeded(bytes, pathOpt)) -> return BudgetOutcome.OutputExceeded(bytes, pathOpt)
            | Error(RunnerError.TimeoutExceeded span) -> return BudgetOutcome.TimeoutExceeded span
            | Error(RunnerError.ProcessCancelled reason) -> return BudgetOutcome.ExecutionFailed("Cancelled: " + reason)
            | Error(RunnerError.SpawnFailed reason) -> return BudgetOutcome.ExecutionFailed("SpawnFailed: " + reason)
            | Error(RunnerError.ExecutionFailed reason) -> return BudgetOutcome.ExecutionFailed reason
        }
