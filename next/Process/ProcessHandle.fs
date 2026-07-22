namespace Wanxiangshu.Next.Process

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel

module ProcessSpawn =

    type private ProcessHandleImpl
        (
            proc: System.Diagnostics.Process,
            cts: CancellationTokenSource,
            exitTcs: TaskCompletionSource<int>,
            stdoutTask: Task<string * bool>,
            stderrTask: Task<string * bool>,
            cmd: Command
        ) =

        let mutable killed = 0

        let drainPumps () =
            task {
                try
                    let pumpsTask = Task.WhenAll([ (stdoutTask :> Task); (stderrTask :> Task) ])
                    use delayCts = new CancellationTokenSource()
                    let delayTask = Task.Delay(TimeSpan.FromSeconds 5.0, delayCts.Token)
                    let! completedTask = Task.WhenAny(pumpsTask, delayTask)

                    if Object.ReferenceEquals(completedTask, pumpsTask) then
                        delayCts.Cancel()

                        try
                            do! delayTask
                        with
                        | :? OperationCanceledException
                        | :? AggregateException -> ()

                        return
                            if pumpsTask.IsFaulted then
                                Some(pumpsTask.Exception.Flatten() :> exn)
                            else
                                None
                    else
                        return None
                with
                | :? OperationCanceledException -> return None
                | ex -> return Some ex
            }

        let killProc () =
            task {
                if Interlocked.Exchange(&killed, 1) <> 0 then
                    return None
                else
                    try
                        if not proc.HasExited then
                            proc.Kill(true)
                    with _ ->
                        ()

                    try
                        cts.Cancel()
                    with _ ->
                        ()

                    try
                        let delayTask = Task.Delay(2000)
                        let! _ = Task.WhenAny(exitTcs.Task :> Task, delayTask)
                        ()
                    with _ ->
                        ()

                    return! drainPumps ()
            }

        let getOkResult () =
            task {
                let! exitCode = exitTcs.Task
                let! (outStr, outTrunc) = stdoutTask
                let! (errStr, errTrunc) = stderrTask

                return
                    Ok(
                        { ExitCode = exitCode
                          Stdout = outStr
                          Stderr = errStr
                          StdoutTruncated = outTrunc
                          StderrTruncated = errTrunc }
                        : Fact.ProcessResult
                    )
            }

        let waitForExitOrDeadline (linkedCts: CancellationTokenSource) (ct: CancellationToken) =
            task {
                let deadlineTask =
                    match cmd.Deadline with
                    | Some dl -> Task.Delay(Deadline.remaining (fun () -> DateTimeOffset.UtcNow) dl, linkedCts.Token)
                    | None -> Task.Delay(Timeout.Infinite, linkedCts.Token)

                let! completedTask = Task.WhenAny(exitTcs.Task :> Task, deadlineTask)

                if
                    exitTcs.Task.IsCompleted
                    || Object.ReferenceEquals(completedTask, exitTcs.Task :> Task)
                then
                    try
                        linkedCts.Cancel()
                    with _ ->
                        ()

                    try
                        do! deadlineTask
                    with _ ->
                        ()

                    return! getOkResult ()
                else
                    let wasCancelled =
                        ct.IsCancellationRequested
                        || cts.IsCancellationRequested
                        || deadlineTask.IsCanceled

                    let! _ = killProc ()

                    if wasCancelled then
                        return Error(ProcessError.ProcessCancelled "Operation was cancelled")
                    else
                        return Error(ProcessError.Timeout "Process deadline expired")
            }

        let runCompletion (procCtx: ProcessContext) (ct: CancellationToken) =
            task {
                use linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, cts.Token)

                try
                    if exitTcs.Task.IsCompleted then
                        return! getOkResult ()
                    else
                        return! waitForExitOrDeadline linkedCts ct
                with ex ->
                    let! _ = killProc ()
                    return Error(ProcessError.ExecutionFailed(ex.ToString()))
            }

        interface ProcessHandle with
            member _.ExitCodeTask = exitTcs.Task
            member _.StdoutTask = stdoutTask
            member _.StderrTask = stderrTask

            member _.Kill() =
                task {
                    let! exOpt = killProc ()

                    match exOpt with
                    | Some ex -> return raise ex
                    | None -> return ()
                }

            member _.RunToCompletion() =
                Flow.create (fun ctx ct -> runCompletion ctx ct)

            member _.DisposeAsync() =
                ValueTask(
                    task {
                        try
                            let! _ = killProc ()
                            ()
                        with _ ->
                            ()

                        try
                            proc.Dispose()
                        with _ ->
                            ()

                        try
                            cts.Dispose()
                        with _ ->
                            ()
                    }
                )

    let private cleanupFailedProc (proc: System.Diagnostics.Process) (cts: CancellationTokenSource option) =
        try
            if not proc.HasExited then
                proc.Kill(true)
        with _ ->
            ()

        proc.Dispose()

        cts
        |> Option.iter (fun c ->
            try
                c.Dispose()
            with _ ->
                ())

    let private handleWriteResult
        (proc: System.Diagnostics.Process)
        (cts: CancellationTokenSource)
        (writeRes: Result<unit, string>)
        =
        match writeRes with
        | Error "Writing stdin cancelled" ->
            cleanupFailedProc proc (Some cts)
            Error(ProcessError.ProcessCancelled "Writing stdin cancelled")
        | Error "Writing stdin timed out" ->
            cleanupFailedProc proc (Some cts)
            Error(ProcessError.Timeout "Writing stdin timed out")
        | Error err ->
            cleanupFailedProc proc (Some cts)
            Error(ProcessError.SpawnFailed err)
        | Ok() -> Ok()

    let spawn
        (cmd: Command)
        (ctx: ProcessContext option)
        (cancellation: CancellationToken)
        : Task<Result<ProcessHandle, ProcessError>> =
        task {
            let mutable createdProc: System.Diagnostics.Process option = None

            try
                let psi = ProcessPump.configureStartInfo cmd ctx
                let proc = new System.Diagnostics.Process()
                proc.StartInfo <- psi
                createdProc <- Some proc

                if proc.Start() then
                    let cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation)
                    let stdoutTask = ProcessPump.pumpReader proc.StandardOutput cts.Token (100 * 1024)
                    let stderrTask = ProcessPump.pumpReader proc.StandardError cts.Token (100 * 1024)
                    let exitTcs = TaskCompletionSource<int>()
                    proc.EnableRaisingEvents <- true
                    proc.Exited.Add(fun _ -> exitTcs.TrySetResult(proc.ExitCode) |> ignore)

                    if proc.HasExited then
                        exitTcs.TrySetResult(proc.ExitCode) |> ignore

                    let! writeRes = ProcessPump.writeStdinAsync proc cmd.Stdin cts.Token cmd.Deadline

                    match handleWriteResult proc cts writeRes with
                    | Error err -> return Error err
                    | Ok() ->
                        return
                            Ok(new ProcessHandleImpl(proc, cts, exitTcs, stdoutTask, stderrTask, cmd) :> ProcessHandle)
                else
                    proc.Dispose()
                    createdProc <- None
                    return Error(ProcessError.SpawnFailed(sprintf "Failed to start process %s" cmd.FileName))
            with ex ->
                createdProc |> Option.iter (fun p -> cleanupFailedProc p None)
                return Error(ProcessError.SpawnFailed(ex.ToString()))
        }
