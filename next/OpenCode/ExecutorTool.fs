namespace Wanxiangshu.Next.OpenCode

open System
open System.Threading
open Thoth.Json
open Wanxiangshu.Next.Process

/// Non-interactive command execution with the request's sole 3x deadline and
/// private Executor-agent summary mailbox.
module ExecutorTool =

    type Request =
        { Command: string
          EstimatedOutputBytes: int64
          EstimatedRunningSeconds: float
          EstimatedMemory: EstimatedMemory }

    let private finitePositive (name: string) (value: float) =
        if Double.IsNaN value || Double.IsInfinity value || value <= 0.0 then
            Error(sprintf "%s must be a finite positive number" name)
        else
            Ok value

    let private finiteOutput (name: string) (value: float) =
        if Double.IsNaN value || Double.IsInfinity value || value < 0.0 || value > float Int64.MaxValue then
            Error(sprintf "%s must be a finite non-negative integer" name)
        elif value <> Math.Floor value then
            Error(sprintf "%s must be an integer" name)
        else
            Ok(int64 value)

    let private decode (args: HostToolArguments) =
        let command = args.Text "command"
        let runtime = args.OptionalNumber "estimated_running_secs" |> Option.defaultValue 30.0
        let output = args.OptionalNumber "estimated_output_bytes" |> Option.defaultValue 65536.0

        let memory =
            match args.Text "estimated_mem_usage" with
            | "large" -> Ok EstimatedMemory.Large
            | "medium"
            | "" -> Ok EstimatedMemory.Medium
            | _ -> Error "estimated_mem_usage must be medium or large"

        if String.IsNullOrWhiteSpace command then
            Error "Missing command"
        else
            match finitePositive "estimated_running_secs" runtime, finiteOutput "estimated_output_bytes" output, memory with
            | Ok runtimeSeconds, Ok outputBytes, Ok estimatedMemory ->
                Ok
                    { Command = command
                      EstimatedOutputBytes = outputBytes
                      EstimatedRunningSeconds = runtimeSeconds
                      EstimatedMemory = estimatedMemory }
            | Error error, _, _
            | _, Error error, _
            | _, _, Error error -> Error error

    let private error (message: string) = ToolHostCodec.jsonObject [ "error", Encode.string message ]

    let private execute (scope: ToolRuntimeScope) (request: Request) (context: HostToolContext) =
        task {
            match scope.RuntimeFor context with
            | Error runtimeError -> return error runtimeError
            | Ok _ ->
                let directory =
                    if String.IsNullOrWhiteSpace context.SessionId then
                        scope.WorkspaceDirectory
                    else
                        scope.DirectoryFor context.SessionId |> Option.orElse scope.WorkspaceDirectory

                let estimate =
                    { EstimatedRuntime = RuntimeSeconds request.EstimatedRunningSeconds
                      EstimatedOutput = OutputBytes request.EstimatedOutputBytes
                      EstimatedMemory = request.EstimatedMemory }

                let command =
                    { FileName = "sh"
                      Arguments = [ "-lc"; request.Command ]
                      WorkingDirectory = directory
                      Environment = None
                      Stdin = None
                      Deadline = None
                      PtyOptions = None }

                use cancellation = new CancellationTokenSource()
                let detachAbort = context.AttachAbort cancellation.Cancel

                let processContext: ProcessContext =
                    { WorkingDirectory = directory
                      DefaultTimeout = None }

                let! result =
                    try
                        ProcessRunner.run command estimate processContext cancellation.Token
                    finally
                        detachAbort ()

                match result with
                | Error processError -> return error (processError.ToString())
                | Ok(ProcessOutcome.Completed(exitCode, stdout, stderr, _)) ->
                    return
                        ToolHostCodec.jsonObject
                            [ "exitCode", Encode.int exitCode
                              "stdout", Encode.string stdout
                              "stderr", Encode.string stderr ]
                | Ok(ProcessOutcome.Spooled(exitCode, spoolPath, totalBytes, chunkCount)) ->
                    try
                        let runtime = scope.ExecutorRuntimeFor context
                        let! summary = ExecutorSummarize.summarizeSpool (ExecutorSummarize.asExecutorRuntime runtime) spoolPath

                        return
                            ToolHostCodec.jsonObject
                                [ "exitCode", Encode.int exitCode
                                  "summary", Encode.string summary
                                  "spoolPath", Encode.string spoolPath
                                  "totalBytes", Encode.int64 totalBytes
                                  "chunkCount", Encode.int chunkCount ]
                    finally
                        Spool.delete spoolPath
        }

    let spec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "executor"
          Description = "Execute a shell command with explicit output, time, and memory estimates."
          Arguments =
            [ "command", ToolHostCodec.stringSchema factory
              "estimated_output_bytes", ToolHostCodec.numberSchema factory
              "estimated_running_secs", ToolHostCodec.numberSchema factory
              "estimated_mem_usage", ToolHostCodec.enumSchema [ "medium"; "large" ] factory ]
          Execute =
            fun args context ->
                match decode args with
                | Ok request -> execute scope request context
                | Error decodeError -> task { return error decodeError } }
