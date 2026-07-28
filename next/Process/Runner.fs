namespace Wanxiangshu.Next.Process

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.AsyncSupport

type RunnerOutcome = ProcessOutcome
type RunnerError = ProcessError

/// Backward-compatible thin entry points. Internals live in ProcessRunner.
module Runner =

    let calculateDeadline (now: DateTimeOffset) (est: EstimatedRuntime) : Deadline =
        Deadline.ofBudget now (ProcessEstimate.budget est)

    let getLargeGateCount () = LargeGate.getCount ()
    let acquireLargeGate (ct: CancellationToken) = LargeGate.acquire ct
    let releaseLargeGate () = LargeGate.release ()

    let execute
        (cmd: Command)
        (estimate: ProcessEstimate)
        (ctx: ProcessContext)
        (ct: CancellationToken)
        : Task<Result<RunnerOutcome, RunnerError>> =
        ProcessRunner.run cmd estimate ctx ct

    let executeWithLauncher
        (launcher: Command -> CancellationToken -> Task<int * byte[] * byte[]>)
        (cmd: Command)
        (estimate: ProcessEstimate)
        (ctx: ProcessContext)
        (ct: CancellationToken)
        : Task<Result<RunnerOutcome, RunnerError>> =
        let hostSpawn
            (_cmd: Command)
            (_ctx: ProcessContext)
            (onStdout: byte[] -> unit)
            (onStderr: byte[] -> unit)
            (parentCt: CancellationToken)
            : Task<Result<NodeProcessHost.ChildProcess, string>> =
            task {
                let cts = new CancellationTokenSource()
                let exitTcs = TaskCompletionSource<int * bool>()

                let _ =
                    parentCt.Register(fun () ->
                        if not cts.IsCancellationRequested then
                            cts.Cancel())

                let exited = ref false

                let kill () =
                    exited.Value <- true

                    if not cts.IsCancellationRequested then
                        cts.Cancel()

                task {
                    try
                        let! (exitCode, outBytes, errBytes) = launcher cmd cts.Token
                        exited.Value <- true

                        if parentCt.IsCancellationRequested then
                            trySetCanceled exitTcs |> ignore
                        else
                            for chunk in Spool.chunkBytes 8192 outBytes do
                                onStdout chunk

                            for chunk in Spool.chunkBytes 8192 errBytes do
                                onStderr chunk

                            trySetResult exitTcs (exitCode, false) |> ignore
                    with
                    | _ when cts.IsCancellationRequested ->
                        // Parent cancel or timeout: let waitForExit set the final signal.
                        exited.Value <- true
                    | _ ->
                        exited.Value <- true
                        trySetResult exitTcs (-1, false) |> ignore
                }
                |> ignore

                return
                    Ok
                        { NodeProcessHost.Process = null
                          NodeProcessHost.Exit = exitTcs
                          NodeProcessHost.Kill = kill
                          NodeProcessHost.Exited = exited }
            }

        ProcessRunner.runWithHost hostSpawn cmd estimate ctx ct
