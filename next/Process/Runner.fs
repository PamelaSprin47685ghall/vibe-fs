namespace Wanxiangshu.Next.Process

open System
open System.Threading
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop

module Runner =

    /// Races the launcher task against the absolute deadline using JS-timer-segmented
    /// waits, so a huge legal budget (tens of days) never overflows int / the JS
    /// timer ceiling the way a single setTimeout(int) would. Mirrors RunnerCore.armTimer.
    /// Rejects with RUNNER_DEADLINE on expiry so executeLauncher maps it to TimeoutExceeded.
    let private withSegmentedDeadline<'T>
        (clock: unit -> DateTimeOffset)
        (deadline: Deadline)
        (work: Task<'T>)
        : Task<'T> =
        let completion = TaskCompletionSource<'T>()
        let mutable timerId: obj = null
        let mutable settled = false

        let clearTimer () =
            if not (isNull timerId) then
                emitJsExpr timerId "clearTimeout($0)" |> ignore
                timerId <- null

        let fireTimeout () =
            if not settled then
                settled <- true
                clearTimer ()
                completion.SetException(Exception "RUNNER_DEADLINE")

        let rec armTimer () =
            let ms = Deadline.nextWaitMs clock deadline

            if ms <= 0 then
                fireTimeout ()
            else
                timerId <-
                    emitJsExpr
                        (ms,
                         (fun () ->
                             if settled then ()
                             elif Deadline.isExpired clock deadline then fireTimeout ()
                             else armTimer ()))
                        "setTimeout($1, $0)"

        armTimer ()

        task {
            try
                let! result = work

                if not settled then
                    settled <- true
                    clearTimer ()
                    completion.SetResult result
            with ex ->
                if not settled then
                    settled <- true
                    clearTimer ()
                    completion.SetException ex
        }
        |> ignore

        completion.Task

    let getLargeGateCount () : int = LargeGate.getCount ()
    let acquireLargeGate (ct: CancellationToken) : Task = LargeGate.acquire ct
    let releaseLargeGate () : unit = LargeGate.release ()

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

    let private executeLauncher
        (launcher: Command -> CancellationToken -> Task<int * byte[] * byte[]>)
        (cmd: Command)
        (estimate: ProcessEstimate)
        (budgetSpan: TimeSpan)
        (ct: CancellationToken)
        : Task<Result<RunnerOutcome, RunnerError>> =
        task {
            try
                let clock = fun () -> DateTimeOffset.UtcNow
                let deadline = Deadline.ofBudget (clock ()) budgetSpan

                let! (exitCode, stdoutBytes, stderrBytes) = withSegmentedDeadline clock deadline (launcher cmd ct)

                if ct.IsCancellationRequested then
                    return Error(RunnerError.ProcessCancelled "Cancelled by token")
                else
                    let totalBytes = int64 stdoutBytes.Length + int64 stderrBytes.Length

                    if totalBytes > outputThreshold estimate then
                        let spool = Spool.startStreamingSpool ()
                        Spool.appendStreamingSpool spool stdoutBytes
                        Spool.appendStreamingSpool spool stderrBytes

                        return
                            Ok(
                                RunnerOutcome.Spooled(
                                    exitCode,
                                    spool.Path,
                                    spool.BytesWritten,
                                    Spool.chunkCount spool.BytesWritten
                                )
                            )
                    else
                        let stdoutText = Text.Encoding.UTF8.GetString(stdoutBytes)
                        let stderrText = Text.Encoding.UTF8.GetString(stderrBytes)
                        return Ok(RunnerOutcome.Completed(exitCode, stdoutText, stderrText, false))
            with
            | ex when ex.Message = "RUNNER_DEADLINE" -> return Error(RunnerError.TimeoutExceeded budgetSpan)
            | :? OperationCanceledException when not ct.IsCancellationRequested ->
                return Error(RunnerError.TimeoutExceeded budgetSpan)
            | :? OperationCanceledException -> return Error(RunnerError.ProcessCancelled "Cancelled by token")
            | ex -> return Error(RunnerError.ExecutionFailed ex.Message)
        }

    /// Execution with an injected launcher for deterministic tests and custom environments.
    let executeWithLauncher
        (launcher: Command -> CancellationToken -> Task<int * byte[] * byte[]>)
        (cmd: Command)
        (estimate: ProcessEstimate)
        (ctx: ProcessContext)
        (ct: CancellationToken)
        : Task<Result<RunnerOutcome, RunnerError>> =
        task {
            let (RuntimeSeconds estSecs) = estimate.EstimatedRuntime
            // Same 3× budget as production path; never int-cast milliseconds
            // (huge legal estimates must not overflow).
            let budgetSpan = TimeSpan.FromSeconds(3.0 * estSecs)
            let isLarge = estimate.EstimatedMemory = EstimatedMemory.Large

            if isLarge then
                do! acquireLargeGate ct

            try
                if ct.IsCancellationRequested then
                    return Error(RunnerError.ProcessCancelled "Cancelled before spawn")
                else
                    return! executeLauncher launcher cmd estimate budgetSpan ct
            finally
                if isLarge then
                    releaseLargeGate ()
        }
