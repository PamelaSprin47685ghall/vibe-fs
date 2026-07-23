namespace Wanxiangshu.Next.Process

open System
open System.Threading
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel

module private NodeChildProc =
    [<Import("spawn", "node:child_process")>]
    let spawn (cmd: string) (args: string array) (opts: obj) : obj = jsNative

module ProcessSpawn =

    type private ProcessHandleImpl
        (
            childProc: obj,
            cts: CancellationTokenSource,
            exitTcs: JsTcs<int>,
            stdoutTask: Task<string * bool>,
            stderrTask: Task<string * bool>,
            cmd: Command
        ) =

        let mutable killed = false

        let killProc () =
            task {
                if not killed then
                    killed <- true

                    try
                        childProc?kill ("SIGTERM") |> ignore
                    with _ ->
                        ()

                    try
                        cts.Cancel()
                    with _ ->
                        ()

                return ()
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

        interface ProcessHandle with
            member _.ExitCodeTask = exitTcs.Task
            member _.StdoutTask = stdoutTask
            member _.StderrTask = stderrTask
            member _.IsPty = Option.isSome cmd.PtyOptions
            member _.ResizePty(cols, rows) =
                if Option.isSome cmd.PtyOptions && not (isNull childProc?resize) then
                    try childProc?resize(cols, rows) |> ignore with _ -> ()

            member _.Kill() = killProc ()

            member _.RunToCompletion() =
                Flow.create (fun ctx ct ->
                    task {
                        try
                            let isDeadlineExpired () =
                                cmd.Deadline
                                |> Option.exists (Deadline.isExpired (fun () -> DateTimeOffset.UtcNow))

                            let mutable terminalResult: Result<Fact.ProcessResult, ProcessError> option = None

                            while terminalResult.IsNone do
                                if ct.IsCancellationRequested || cts.IsCancellationRequested then
                                    try
                                        childProc?kill ("SIGTERM") |> ignore
                                    with _ ->
                                        ()

                                    terminalResult <- Some(Error(ProcessError.ProcessCancelled "Operation was cancelled"))
                                elif isDeadlineExpired () then
                                    try
                                        childProc?kill ("SIGTERM") |> ignore
                                    with _ ->
                                        ()

                                    terminalResult <- Some(Error(ProcessError.Timeout "Process deadline expired"))
                                elif exitTcs.IsCompleted then
                                    let! result = getOkResult ()
                                    terminalResult <- Some result
                                else
                                    do! FlowHelpers.sleepJs 10

                            return Option.get terminalResult
                        with ex ->
                            let! _ = killProc ()
                            return Error(ProcessError.ExecutionFailed ex.Message)
                    })

            member _.Dispose() =
                try
                    childProc?kill ("SIGTERM") |> ignore
                with _ ->
                    ()

                try
                    cts.Dispose()
                with _ ->
                    ()

            member _.DisposeAsync() =
                try
                    childProc?kill ("SIGTERM") |> ignore
                with _ ->
                    ()

                try
                    cts.Dispose()
                with _ ->
                    ()

                ValueTask()

    let spawn
        (cmd: Command)
        (ctx: ProcessContext option)
        (cancellation: CancellationToken)
        : Task<Result<ProcessHandle, ProcessError>> =
        task {
            try
                let cwdOpt =
                    cmd.WorkingDirectory
                    |> Option.orElse (ctx |> Option.bind (fun c -> c.WorkingDirectory))

                let opts =
                    {| cwd = cwdOpt |> Option.toObj
                       env = cmd.Environment |> Option.map box |> Option.toObj |}

                let childProc = NodeChildProc.spawn cmd.FileName (List.toArray cmd.Arguments) opts
                let cts = new CancellationTokenSource()
                let exitTcs = JsTcs<int>()
                let mutable spawnError: string option = None

                let stdoutTask = ProcessPump.pumpStream childProc?stdout cts.Token (100 * 1024)
                let stderrTask = ProcessPump.pumpStream childProc?stderr cts.Token (100 * 1024)

                childProc?on (
                    "exit",
                    fun (code: obj) ->
                        let exitCode = if isNull code then 0 else unbox<int> code
                        exitTcs.TrySetResult(exitCode) |> ignore
                )
                |> ignore

                childProc?on (
                    "error",
                    fun (err: obj) ->
                        spawnError <- Some(if isNull err then "Failed to spawn process" else string err)
                        exitTcs.TrySetResult(-1) |> ignore
                )
                |> ignore

                match cmd.Stdin with
                | Some stdin ->
                    try
                        childProc?stdin?write (stdin, "utf-8") |> ignore
                        childProc?stdin?``end`` () |> ignore
                    with _ ->
                        ()
                | None -> ()

                do! FlowHelpers.sleepJs 15

                match spawnError with
                | Some err -> return Error(ProcessError.SpawnFailed err)
                | None ->
                    return
                        Ok(new ProcessHandleImpl(childProc, cts, exitTcs, stdoutTask, stderrTask, cmd) :> ProcessHandle)
            with ex ->
                return Error(ProcessError.SpawnFailed ex.Message)
        }
