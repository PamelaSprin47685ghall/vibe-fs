namespace rec Wanxiangshu.Process

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Foundation.AsyncSupport

module ProcessRunner =
    open NodeProcessHost
    open ProcessOutput

    type private HostSpawn =
        Command -> ProcessContext -> (byte[] -> unit) -> (byte[] -> unit) -> CancellationToken -> Task<Result<ChildProcess, ProcessResult>>

    let private validate (estimate: ProcessEstimate) (ctx: ProcessContext) =
        let (RuntimeSeconds runtime) = estimate.EstimatedRuntime
        let (OutputBytes output) = estimate.EstimatedOutput
        if Double.IsNaN runtime || Double.IsInfinity runtime || runtime <= 0.0 then
            Some "deadline_seconds must be a finite positive number"
        elif output < 0L then
            Some "output_budget_bytes must be non-negative"
        elif Double.IsNaN ctx.HardLimit.TotalMilliseconds || Double.IsInfinity ctx.HardLimit.TotalMilliseconds || ctx.HardLimit <= TimeSpan.Zero then
            Some "hard limit must be finite and positive"
        else None

    let private observe (child: ChildProcess) collector (stop: StopRequest option) =
        let ending = child.Observe()

        let fault =
            child.Failure.Value
            |> Option.orElse (
                if
                    child.Exited.Value
                    && ending = EndEvidence.Unconfirmed
                    && child.Rejection.Value.IsNone
                then
                    Some "Target end was not confirmed"
                else
                    None
            )

        let output =
            if child.Exited.Value && fault.IsNone && stop.IsNone then
                Some(buildResult collector)
            else
                discard collector
                None

        match child.Rejection.Value with
        | Some reason ->
            discard collector
            ProcessResult.NotStarted reason
        | None ->
            ProcessResult.Observed
                { End = ending
                  StopRequested = stop
                  Output = output
                  Fault = fault }

    let private stopQuietly (child: ChildProcess) =
        try child.Kill()
        with error -> child.Failure.Value <- child.Failure.Value |> Option.orElse (Some error.Message)

    let private waitForChild (child: ChildProcess) collector estimate ctx (ct: CancellationToken) =
        task {
            let budget = ProcessEstimate.effectiveDeadline estimate.EstimatedRuntime ctx.HardLimit
            try
                let! waited = NodeProcessWait.waitForExit child (Deadline.ofBudget DateTimeOffset.UtcNow budget) ct
                let stop =
                    waited.StopRequested |> Option.map (function
                        | NodeProcessWait.Cancellation -> StopRequest.CancellationRequested
                        | NodeProcessWait.Deadline -> StopRequest.DeadlineStopRequested budget)
                return observe child collector stop
            with error ->
                stopQuietly child
                child.Failure.Value <- child.Failure.Value |> Option.orElse (Some error.Message)
                return observe child collector (if ct.IsCancellationRequested then Some StopRequest.CancellationRequested else None)
        }

    let private spawnAndObserve (host: HostSpawn) cmd collector estimate ctx ct =
        task {
            match! host cmd ctx (addStdout collector) (addStderr collector) ct with
            | Error result ->
                discard collector
                return result
            | Ok child -> return! waitForChild child collector estimate ctx ct
        }

    let private runStarted (host: HostSpawn) cmd estimate ctx ct =
        task {
            let collector = create estimate
            try
                return! spawnAndObserve host cmd collector estimate ctx ct
            with error ->
                discard collector
                return ProcessResult.Observed
                    { End = EndEvidence.Unconfirmed
                      StopRequested = if ct.IsCancellationRequested then Some StopRequest.CancellationRequested else None
                      Output = None
                      Fault = Some error.Message }
        }

    let private acquireGate (ct: CancellationToken) =
        task {
            try
                do! LargeGate.acquire ct
                return Some true
            with _ when ct.IsCancellationRequested -> return None
        }

    let private maybeAcquireGate estimate ct =
        if estimate.EstimatedMemory = EstimatedMemory.Large then acquireGate ct
        else Task.FromResult(Some false)

    let private runIfLive host cmd estimate ctx (ct: CancellationToken) =
        if ct.IsCancellationRequested then Task.FromResult(ProcessResult.NotStarted NoExecution.CancelledBeforeStart)
        else runStarted host cmd estimate ctx ct

    let private runWithGate host cmd estimate ctx ct held =
        task {
            try return! runIfLive host cmd estimate ctx ct
            finally
                if held then LargeGate.release()
        }

    let private afterAcquisition host cmd estimate ctx ct acquired =
        match acquired with
        | None -> Task.FromResult(ProcessResult.NotStarted NoExecution.CancelledBeforeStart)
        | Some held -> runWithGate host cmd estimate ctx ct held

    let private runValidated host cmd estimate ctx (ct: CancellationToken) =
        task {
            if ct.IsCancellationRequested then return ProcessResult.NotStarted NoExecution.CancelledBeforeStart
            else
                let! acquired = maybeAcquireGate estimate ct
                return! afterAcquisition host cmd estimate ctx ct acquired
        }

    let runWithHost (host: HostSpawn) cmd estimate ctx ct =
        match validate estimate ctx with
        | Some reason -> Task.FromResult(ProcessResult.NotStarted(NoExecution.InvalidRequest reason))
        | None -> runValidated host cmd estimate ctx ct

    let run cmd estimate ctx ct = runWithHost NodeProcessHost.spawn cmd estimate ctx ct

    let runWithLauncher
        (launcher: Command -> CancellationToken -> Task<EndEvidence * byte[] * byte[]>)
        cmd estimate ctx ct =
        let host command _ onStdout onStderr _ =
            task {
                let cancellation = new CancellationTokenSource()
                let ending = ref EndEvidence.Unconfirmed
                let child =
                    { Process = null
                      Observe = fun () -> ending.Value
                      Kill = cancellation.Cancel
                      Exited = ref false
                      OnExited = ResizeArray()
                      Failure = ref None
                      Rejection = ref None }
                task {
                    try
                        let! evidence, stdout, stderr = launcher command cancellation.Token
                        ending.Value <- evidence
                        for chunk in Spool.chunkBytes 8192 stdout do onStdout chunk
                        for chunk in Spool.chunkBytes 8192 stderr do onStderr chunk
                    with error -> child.Failure.Value <- Some error.Message
                    child.Exited.Value <- true
                    notifyExited child
                    cancellation.Dispose()
                } |> ignore
                return Ok child
            }
        runWithHost host cmd estimate ctx ct
