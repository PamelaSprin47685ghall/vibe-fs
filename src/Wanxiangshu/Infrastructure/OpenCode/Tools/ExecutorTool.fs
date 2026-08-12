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

/// Bounded command execution. Provider verbs: `run` (DevOps) and `query-shell` (Inspector).
module ExecutorTool =

    type Request =
        { Command: string
          DeadlineSeconds: float
          OutputBudgetBytes: int64
          WorldLock: bool }

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

    let private decodeRun (args: HostToolArguments) =
        let command = args.Text "command"
        let deadline = args.OptionalNumber "deadline_seconds" |> Option.defaultValue 30.0

        let budget =
            args.OptionalNumber "output_budget_bytes" |> Option.defaultValue 65536.0

        let worldLock =
            match args.OptionalBool "world_lock" with
            | Some value -> Ok value
            | None -> Ok false

        if String.IsNullOrWhiteSpace command then
            Error "Missing command"
        else
            match finitePositive "deadline_seconds" deadline, finiteOutput "output_budget_bytes" budget, worldLock with
            | Ok deadlineSeconds, Ok outputBytes, Ok lock ->
                Ok
                    { Command = command
                      DeadlineSeconds = deadlineSeconds
                      OutputBudgetBytes = outputBytes
                      WorldLock = lock }
            | Error error, _, _
            | _, Error error, _
            | _, _, Error error -> Error error

    let private decodeQueryShell (args: HostToolArguments) =
        let command = args.Text "command"

        if String.IsNullOrWhiteSpace command then
            Error "Missing command"
        else
            Ok
                { Command = command
                  DeadlineSeconds = 30.0
                  OutputBudgetBytes = 65536L
                  WorldLock = false }

    let private consequence (message: string) = tomlObjectWithInstructions [ "# " + message ] []

    let private processConsequence (processError: ProcessError) =
        match processError with
        | ProcessError.TimeoutExceeded _ ->
            consequence "The command was still running when its allowed time ended, so it was stopped."
        | ProcessError.SpawnFailed _ -> consequence "The command could not be started."
        | ProcessError.ProcessCancelled _ -> consequence "The command stopped before it could finish."
        | ProcessError.ExecutionFailed _ -> consequence "The command could not be completed."

    let private execute (scope: ToolRuntimeScope) (request: Request) (context: HostToolContext) =
        task {
            match scope.RuntimeFor context with
            | Error _ -> return consequence "The command cannot run from this execution context."
            | Ok _ ->
                let directory =
                    if String.IsNullOrWhiteSpace context.SessionId then
                        scope.WorkspaceDirectory
                    else
                        scope.DirectoryFor context.SessionId |> Option.orElse scope.WorkspaceDirectory

                let estimate =
                    { EstimatedRuntime = RuntimeSeconds request.DeadlineSeconds
                      EstimatedOutput = OutputBytes request.OutputBudgetBytes
                      EstimatedMemory =
                        if request.WorldLock then
                            EstimatedMemory.Large
                        else
                            EstimatedMemory.Medium }

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
                | Error processError -> return processConsequence processError
                | Ok(ProcessOutcome.Completed(exitCode, stdout, stderr, _)) ->
                    let fields =
                        [ yield "exit_code", TInt exitCode
                          if not (String.IsNullOrWhiteSpace stdout) then
                              yield "stdout", TString stdout
                          if not (String.IsNullOrWhiteSpace stderr) then
                              yield "stderr", TString stderr ]

                    return tomlObject fields
                | Ok(ProcessOutcome.Spooled(exitCode, spoolPath, _totalBytes, _chunkCount)) ->
                    try
                        if String.IsNullOrWhiteSpace context.SessionId then
                            return consequence "The command output cannot be condensed until the caller's authority is established."
                        else
                            let root = SessionId.create context.SessionId

                            let requirePermit () : Task<Result<FamilyRecoveryPermit, string>> =
                                task {
                                    let! recovery = scope.RequireFamilyRecovery root

                                    match recovery with
                                    | FamilyRecovery.FamilyBlocked _ ->
                                        return Error "RECOVERY_BLOCKED: family recovery blocked before run join"
                                    | FamilyRecovery.FamilyWaiting _ ->
                                        return Error "RECOVERY_WAITING: family recovery incomplete before run join"
                                    | FamilyRecovery.FamilyReady permit -> return Ok permit
                                }

                            match! requirePermit () with
                            | Error msg when msg.StartsWith("RECOVERY_BLOCKED:", System.StringComparison.Ordinal) ->
                                return consequence "The command finished, but its large output cannot be reconciled while recovery is blocked."
                            | Error _
                            | Ok _ ->
                                let runtime =
                                    match scope.Journal with
                                    | Some journal ->
                                        Distillation.asDistillationRuntime
                                            (scope.ExecutorRuntimeFor context)
                                            journal
                                            requirePermit
                                    | None -> Distillation.ofForkRuntime (ForkRuntime())

                                let! summary = Distillation.distillSpool runtime spoolPath

                                let instructions =
                                    if System.String.IsNullOrWhiteSpace summary then
                                        []
                                    else
                                        [ summary ]

                                return tomlObjectWithInstructions instructions [ "exit_code", TInt exitCode ]
                    finally
                        Spool.delete spoolPath
        }

    let runSpec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "run"
          Description =
            "Execute a bounded shell command. deadline_seconds and output_budget_bytes are willingness budgets; world_lock acquires the LargeGate."
          Arguments =
            [ "command", ToolHostCodec.stringSchema factory
              "deadline_seconds", ToolHostCodec.numberSchema factory
              "output_budget_bytes", ToolHostCodec.numberSchema factory
              "world_lock", ToolHostCodec.boolSchema factory ]
          Execute =
            fun args context ->
                match decodeRun args with
                | Ok request -> execute scope request context
                | Error decodeError -> task { return consequence decodeError } }

    let queryShellSpec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "query-shell"
          Description =
            "Inspector-only static evidence query. Reveal an existing repository fact; do not create new behavioral observations."
          Arguments = [ "command", ToolHostCodec.stringSchema factory ]
          Execute =
            fun args context ->
                match decodeQueryShell args with
                | Ok request -> execute scope request context
                | Error decodeError -> task { return consequence decodeError } }
