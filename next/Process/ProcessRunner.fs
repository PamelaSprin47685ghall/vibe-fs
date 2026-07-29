module Wanxiangshu.Next.Process.ProcessRunner

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Flow

type ProcessFlow<'a> = Flow<ProcessContext, ProcessError, 'a>

let private runnerFlow = FlowBuilder<ProcessContext, ProcessError>(None)

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

let private liftTask (f: CancellationToken -> Task<'a>) : ProcessFlow<'a> = fromTask (fun _ ct -> f ct)

let private budgetSpan (estimate: ProcessEstimate) =
    ProcessEstimate.budget estimate.EstimatedRuntime

let private largeGateFlow (estimate: ProcessEstimate) : ProcessFlow<unit> =
    if estimate.EstimatedMemory = EstimatedMemory.Large then
        liftTask (fun ct -> task { do! LargeGate.acquire ct })
    else
        runnerFlow { return () }

let private waitFlow (child: NodeProcessHost.ChildProcess) (estimate: ProcessEstimate) : ProcessFlow<int> =
    fromTask (fun _ ct ->
        let clock = fun () -> DateTimeOffset.UtcNow
        let deadline = Deadline.ofBudget (clock ()) (budgetSpan estimate)
        NodeProcessWait.waitForExit child deadline ct)
    |> (fun flow ->
        Flow.create (fun ctx ct ->
            task {
                let! result = Flow.run ctx ct flow

                match result with
                | Error e -> return Error e
                | Ok(exitCode, timedOut) ->
                    if timedOut then
                        return Error(ProcessError.TimeoutExceeded(budgetSpan estimate))
                    else
                        return Ok exitCode
            }))

let private runProgram
    (hostSpawn:
        Command
            -> ProcessContext
            -> (byte[] -> unit)
            -> (byte[] -> unit)
            -> CancellationToken
            -> Task<Result<NodeProcessHost.ChildProcess, string>>)
    (cmd: Command)
    (estimate: ProcessEstimate)
    : ProcessFlow<ProcessOutcome> =
    runnerFlow {
        do! largeGateFlow estimate

        try
            let collector = ProcessOutput.create estimate

            let! spawnResult =
                fromTask (fun ctx ct ->
                    hostSpawn cmd ctx (ProcessOutput.addStdout collector) (ProcessOutput.addStderr collector) ct)

            let! child =
                match spawnResult with
                | Ok child -> runnerFlow { return child }
                | Error reason -> runnerFlow { return! Flow.fail (ProcessError.SpawnFailed reason) }

            try
                let! exitCode = waitFlow child estimate
                return ProcessOutput.buildResult collector exitCode
            finally
                try
                    child.Kill()
                with _ ->
                    ()
        finally
            if estimate.EstimatedMemory = EstimatedMemory.Large then
                LargeGate.release ()
    }

let runWithHost
    (hostSpawn:
        Command
            -> ProcessContext
            -> (byte[] -> unit)
            -> (byte[] -> unit)
            -> CancellationToken
            -> Task<Result<NodeProcessHost.ChildProcess, string>>)
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
