namespace Wanxiangshu.OpenCode

open System
open System.Threading
open System.Threading.Tasks
open ToolHostCodec
open Wanxiangshu.Domain.SessionRecovery
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Process
open Wanxiangshu.Session

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
        if
            Double.IsNaN value
            || Double.IsInfinity value
            || value < 0.0
            || value > float Int64.MaxValue
        then
            Error(sprintf "%s must be a finite non-negative integer" name)
        elif value <> Math.Floor value then
            Error(sprintf "%s must be an integer" name)
        else
            Ok(int64 value)

    let private decode (args: HostToolArguments) =
        let command = args.Text "command"

        let runtime =
            args.OptionalNumber "estimated_running_secs" |> Option.defaultValue 30.0

        let output =
            args.OptionalNumber "estimated_output_bytes" |> Option.defaultValue 65536.0

        let memory =
            match args.Text "estimated_mem_usage" with
            | "large" -> Ok EstimatedMemory.Large
            | "medium"
            | "" -> Ok EstimatedMemory.Medium
            | _ -> Error "estimated_mem_usage must be medium or large"

        if String.IsNullOrWhiteSpace command then
            Error "Missing command"
        else
            match
                finitePositive "estimated_running_secs" runtime, finiteOutput "estimated_output_bytes" output, memory
            with
            | Ok runtimeSeconds, Ok outputBytes, Ok estimatedMemory ->
                Ok
                    { Command = command
                      EstimatedOutputBytes = outputBytes
                      EstimatedRunningSeconds = runtimeSeconds
                      EstimatedMemory = estimatedMemory }
            | Error error, _, _
            | _, Error error, _
            | _, _, Error error -> Error error

    let private error (message: string) = tomlObject [ "error", TString message ]

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
                      HardLimit = scope.ProcessHardLimit }

                let! result =
                    try
                        ProcessRunner.run command estimate processContext cancellation.Token
                    finally
                        detachAbort ()

                match result with
                | Error processError -> return error (processError.ToString())
                | Ok(ProcessOutcome.Completed(exitCode, stdout, stderr, _)) ->
                    return
                        tomlObject
                            [ "exit_code", TInt exitCode
                              "stdout", TString stdout
                              "stderr", TString stderr ]
                | Ok(ProcessOutcome.Spooled(exitCode, spoolPath, totalBytes, chunkCount)) ->
                    try
                        // P0-RECOVERY-JOIN-001: map/reduce Join requires FamilyReady permit.
                        // Empty SessionId fail closed (no skip). Fresh permit per JoinWithPermit
                        // (map/reduce mutates family closure digest).
                        if String.IsNullOrWhiteSpace context.SessionId then
                            return error "Missing sessionID"
                        else
                            let root = SessionId.create context.SessionId

                            let requirePermit () : Task<Result<FamilyRecoveryPermit, string>> =
                                task {
                                    let! recovery = scope.RequireFamilyRecovery root

                                    match recovery with
                                    | FamilyRecovery.FamilyBlocked _ ->
                                        return Error "RECOVERY_BLOCKED: family recovery blocked before executor join"
                                    | FamilyRecovery.FamilyWaiting _ ->
                                        // Incomplete (HandlesWaiting / transient unreadable):
                                        // not hard RECOVERY_BLOCKED. Retry until Ready or timeout
                                        // inside awaitAgent; no permit issued while waiting.
                                        return Error "RECOVERY_WAITING: family recovery incomplete before executor join"
                                    | FamilyRecovery.FamilyReady permit -> return Ok permit
                                }

                            // Hard-fail only on definitive FamilyBlocked. Waiting/incomplete
                            // must not abort map/reduce; JoinWithPermit retries requirePermit.
                            match! requirePermit () with
                            | Error msg when msg.StartsWith("RECOVERY_BLOCKED:", System.StringComparison.Ordinal) ->
                                return error msg
                            | Error _
                            | Ok _ ->
                                // Journal present → event-driven targeted await. Missing
                                // journal → the pure fork runtime fails every chunk fork
                                // fast, so a spooled summary still degrades to a partial
                                // report instead of being dropped.
                                let runtime =
                                    match scope.Journal with
                                    | Some journal ->
                                        ExecutorSummarize.asExecutorRuntime
                                            (scope.ExecutorRuntimeFor context)
                                            journal
                                            requirePermit
                                    | None -> ExecutorSummarize.ofForkRuntime (ForkRuntime())

                                let! summary = ExecutorSummarize.summarizeSpool runtime spoolPath

                                let instructions =
                                    if System.String.IsNullOrWhiteSpace summary then
                                        []
                                    else
                                        [ summary ]

                                return
                                    tomlObjectWithInstructions
                                        instructions
                                        [ "exit_code", TInt exitCode
                                          "spool_path", TString spoolPath
                                          "total_bytes", TInt64 totalBytes
                                          "chunk_count", TInt chunkCount ]
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
