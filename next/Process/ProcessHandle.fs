namespace Wanxiangshu.Next.Process

open System
open System.IO
open System.Text
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel

module ProcessSpawn =

    let private pumpReader (reader: TextReader) (cancellation: CancellationToken) (maxChars: int) =
        task {
            let sb = StringBuilder()
            let buffer = Array.zeroCreate<char> 4096
            let mutable truncated, reading = false, true
            let mutable pumpEx: exn option = None

            while reading && not cancellation.IsCancellationRequested do
                try
                    let! count = reader.ReadAsync(buffer.AsMemory(), cancellation).AsTask()

                    if count = 0 then
                        reading <- false
                    elif truncated then
                        ()
                    elif sb.Length + count > maxChars then
                        let allowed = Math.Max(0, maxChars - sb.Length)

                        if allowed > 0 then
                            sb.Append(buffer, 0, allowed) |> ignore

                        truncated <- true
                    else
                        sb.Append(buffer, 0, count) |> ignore
                with
                | :? OperationCanceledException
                | :? ObjectDisposedException -> reading <- false
                | ex ->
                    pumpEx <- Some ex
                    reading <- false

            match pumpEx with
            | Some ex -> return raise ex
            | None -> return (sb.ToString(), truncated)
        }

    let private configureStartInfo (cmd: Command) (ctx: ProcessContext option) =
        let psi =
            System.Diagnostics.ProcessStartInfo(
                FileName = cmd.FileName,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            )

        for arg in cmd.Arguments do
            psi.ArgumentList.Add(arg)

        (cmd.WorkingDirectory
         |> Option.orElse (ctx |> Option.bind (fun c -> c.WorkingDirectory)))
        |> Option.iter (fun w -> psi.WorkingDirectory <- w)

        cmd.Environment
        |> Option.iter (fun env ->
            for KeyValue(k, v) in env do
                psi.Environment.[k] <- v)

        psi

    let private writeStdinAsync
        (proc: System.Diagnostics.Process)
        (stdinOpt: string option)
        (ct: CancellationToken)
        (dlOpt: Deadline option)
        : Task<Result<unit, string>> =
        let writeWithToken (token: CancellationToken) =
            task {
                try
                    match stdinOpt with
                    | None ->
                        do! proc.StandardInput.DisposeAsync().AsTask()
                        return Ok()
                    | Some input ->
                        let bytes = Encoding.UTF8.GetBytes(input)
                        do! proc.StandardInput.BaseStream.WriteAsync(ReadOnlyMemory(bytes), token).AsTask()
                        do! proc.StandardInput.BaseStream.FlushAsync(token)
                        do! proc.StandardInput.DisposeAsync().AsTask()
                        return Ok()
                with
                | :? OperationCanceledException ->
                    if ct.IsCancellationRequested then
                        return Error "Writing stdin cancelled"
                    elif dlOpt |> Option.exists (Deadline.isExpired (fun () -> DateTimeOffset.UtcNow)) then
                        return Error "Writing stdin timed out"
                    else
                        return Error "Writing stdin cancelled"
                | ex -> return Error(sprintf "Failed writing stdin: %s" ex.Message)
            }

        match dlOpt with
        | None -> writeWithToken ct
        | Some dl ->
            task {
                let rem = Deadline.remaining (fun () -> DateTimeOffset.UtcNow) dl
                use deadlineCts = new CancellationTokenSource(rem)

                use linkedCts =
                    CancellationTokenSource.CreateLinkedTokenSource(ct, deadlineCts.Token)

                return! writeWithToken linkedCts.Token
            }

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

        let runCompletion (procCtx: ProcessContext) (ct: CancellationToken) =
            task {
                use linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, cts.Token)

                try
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

                    if exitTcs.Task.IsCompleted then
                        return! getOkResult ()
                    else
                        let deadlineTask =
                            match cmd.Deadline with
                            | Some dl ->
                                Task.Delay(Deadline.remaining (fun () -> DateTimeOffset.UtcNow) dl, linkedCts.Token)
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

    let spawn
        (cmd: Command)
        (ctx: ProcessContext option)
        (cancellation: CancellationToken)
        : Task<Result<ProcessHandle, ProcessError>> =
        task {
            try
                let psi = configureStartInfo cmd ctx
                let proc = new System.Diagnostics.Process()
                proc.StartInfo <- psi

                if not (proc.Start()) then
                    proc.Dispose()
                    return Error(ProcessError.SpawnFailed(sprintf "Failed to start process %s" cmd.FileName))
                else
                    let cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation)
                    let stdoutTask = pumpReader proc.StandardOutput cts.Token (100 * 1024)
                    let stderrTask = pumpReader proc.StandardError cts.Token (100 * 1024)

                    let exitTcs = TaskCompletionSource<int>()
                    proc.EnableRaisingEvents <- true
                    proc.Exited.Add(fun _ -> exitTcs.TrySetResult(proc.ExitCode) |> ignore)

                    if proc.HasExited then
                        exitTcs.TrySetResult(proc.ExitCode) |> ignore

                    let! writeRes = writeStdinAsync proc cmd.Stdin cts.Token cmd.Deadline

                    match writeRes with
                    | Error "Writing stdin cancelled" ->
                        try
                            if not proc.HasExited then
                                proc.Kill(true)
                        with _ ->
                            ()

                        proc.Dispose()
                        cts.Dispose()
                        return Error(ProcessError.ProcessCancelled "Writing stdin cancelled")
                    | Error "Writing stdin timed out" ->
                        try
                            if not proc.HasExited then
                                proc.Kill(true)
                        with _ ->
                            ()

                        proc.Dispose()
                        cts.Dispose()
                        return Error(ProcessError.Timeout "Writing stdin timed out")
                    | Error err ->
                        try
                            if not proc.HasExited then
                                proc.Kill(true)
                        with _ ->
                            ()

                        proc.Dispose()
                        cts.Dispose()
                        return Error(ProcessError.SpawnFailed err)
                    | Ok() ->
                        return
                            Ok(new ProcessHandleImpl(proc, cts, exitTcs, stdoutTask, stderrTask, cmd) :> ProcessHandle)
            with ex ->
                return Error(ProcessError.SpawnFailed(ex.ToString()))
        }
