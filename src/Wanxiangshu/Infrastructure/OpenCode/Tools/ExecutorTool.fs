namespace Wanxiangshu.OpenCode

open System
open System.Threading
open System.Threading.Tasks
open ToolHostCodec
open Wanxiangshu.Domain
open Wanxiangshu.Domain.SessionRecovery
open Wanxiangshu.Infrastructure.Resources
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Process
open Wanxiangshu.Session

/// Bounded command execution. Provider verbs: `run` (DevOps) and `query-shell` (Inspector).
module ExecutorTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<RequireQualifiedAccess>]
        module Run =
            [<Literal>]
            let Description = "tool/run/description"

            [<Literal>]
            let MissingCommand = "tool/run/missing-command"

            [<Literal>]
            let FinitePositive = "tool/run/finite-positive"

            [<Literal>]
            let FiniteNonNegativeInteger = "tool/run/finite-non-negative-integer"

            [<Literal>]
            let MustBeInteger = "tool/run/must-be-integer"

            [<Literal>]
            let Timeout = "tool/run/timeout"

            [<Literal>]
            let SpawnFailed = "tool/run/spawn-failed"

            [<Literal>]
            let Cancelled = "tool/run/cancelled"

            [<Literal>]
            let ExecutionFailed = "tool/run/execution-failed"

            [<Literal>]
            let CannotRunFromContext = "tool/run/cannot-run-from-context"

            [<Literal>]
            let CannotCondenseUntilAuthority = "tool/run/cannot-condense-until-authority"

            [<Literal>]
            let LargeOutputRecoveryBlocked = "tool/run/large-output-recovery-blocked"

        [<RequireQualifiedAccess>]
        module QueryShell =
            [<Literal>]
            let Description = "tool/query-shell/description"

            [<Literal>]
            let MissingCommand = "tool/query-shell/missing-command"

    type Request =
        { Command: string
          DeadlineSeconds: float
          OutputBudgetBytes: int64
          WorldLock: bool }

    let private lang (ctx: HostToolContext) =
        if String.IsNullOrWhiteSpace ctx.SessionId then
            ProviderLanguageBinding.readGlobalPreference ()
        else
            ProviderLanguageBinding.ensureRoot (SessionId.create ctx.SessionId)

    let private prose language path =
        ProviderProse.render language path Map.empty

    let private namedProse language path name =
        ProviderProse.render language path (Map [ "name", name ])

    let private finitePositive language (name: string) (value: float) =
        if Double.IsNaN value || Double.IsInfinity value || value <= 0.0 then
            Error(namedProse language Path.Run.FinitePositive name)
        else
            Ok value

    let private finiteOutput language (name: string) (value: float) =
        if
            Double.IsNaN value
            || Double.IsInfinity value
            || value < 0.0
            || value > float Int64.MaxValue
        then
            Error(namedProse language Path.Run.FiniteNonNegativeInteger name)
        elif value <> Math.Floor value then
            Error(namedProse language Path.Run.MustBeInteger name)
        else
            Ok(int64 value)

    let private decodeRun (language: ProviderLanguage) (args: HostToolArguments) =
        let command = args.Text "command"
        let deadline = args.OptionalNumber "deadline_seconds" |> Option.defaultValue 30.0

        let budget =
            args.OptionalNumber "output_budget_bytes" |> Option.defaultValue 65536.0

        let worldLock =
            match args.OptionalBool "world_lock" with
            | Some value -> Ok value
            | None -> Ok false

        if String.IsNullOrWhiteSpace command then
            Error(prose language Path.Run.MissingCommand)
        else
            match
                finitePositive language "deadline_seconds" deadline,
                finiteOutput language "output_budget_bytes" budget,
                worldLock
            with
            | Ok deadlineSeconds, Ok outputBytes, Ok lock ->
                Ok
                    { Command = command
                      DeadlineSeconds = deadlineSeconds
                      OutputBudgetBytes = outputBytes
                      WorldLock = lock }
            | Error error, _, _
            | _, Error error, _
            | _, _, Error error -> Error error

    let private decodeQueryShell (language: ProviderLanguage) (args: HostToolArguments) =
        let command = args.Text "command"

        if String.IsNullOrWhiteSpace command then
            Error(prose language Path.QueryShell.MissingCommand)
        else
            Ok
                { Command = command
                  DeadlineSeconds = 30.0
                  OutputBudgetBytes = 65536L
                  WorldLock = false }

    let private consequence (message: string) =
        tomlObjectWithInstructions [ message ] []

    let private processConsequence language (processError: ProcessError) =
        match processError with
        | ProcessError.TimeoutExceeded _ -> consequence (prose language Path.Run.Timeout)
        | ProcessError.SpawnFailed _ -> consequence (prose language Path.Run.SpawnFailed)
        | ProcessError.ProcessCancelled _ -> consequence (prose language Path.Run.Cancelled)
        | ProcessError.ExecutionFailed _ -> consequence (prose language Path.Run.ExecutionFailed)

    let private execute (scope: ToolRuntimeScope) (request: Request) (context: HostToolContext) =
        task {
            let language = lang context

            match scope.RuntimeFor context with
            | Error _ -> return consequence (prose language Path.Run.CannotRunFromContext)
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
                | Error processError -> return processConsequence language processError
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
                            return consequence (prose language Path.Run.CannotCondenseUntilAuthority)
                        else
                            let root = SessionId.create context.SessionId

                            let requirePermit () : Task<Result<FamilyRecoveryPermit, string>> =
                                task {
                                    let! recovery = scope.RequireFamilyRecovery root

                                    match recovery with
                                    | FamilyRecovery.FamilyBlocked _ -> return Error "RECOVERY_BLOCKED:"
                                    | FamilyRecovery.FamilyWaiting _ -> return Error "RECOVERY_WAITING:"
                                    | FamilyRecovery.FamilyReady permit -> return Ok permit
                                }

                            match! requirePermit () with
                            | Error msg when msg.StartsWith("RECOVERY_BLOCKED", System.StringComparison.Ordinal) ->
                                return consequence (prose language Path.Run.LargeOutputRecoveryBlocked)
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

                                let! summary = Distillation.distillSpool runtime spoolPath language

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
            ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) Path.Run.Description Map.empty
          Arguments =
            [ "command", ToolHostCodec.stringSchema factory
              "deadline_seconds", ToolHostCodec.numberSchema factory
              "output_budget_bytes", ToolHostCodec.numberSchema factory
              "world_lock", ToolHostCodec.boolSchema factory ]
          Execute =
            fun args context ->
                match decodeRun (lang context) args with
                | Ok request -> execute scope request context
                | Error decodeError -> task { return consequence decodeError } }

    let queryShellSpec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "query-shell"
          Description =
            ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) Path.QueryShell.Description Map.empty
          Arguments = [ "command", ToolHostCodec.stringSchema factory ]
          Execute =
            fun args context ->
                match decodeQueryShell (lang context) args with
                | Ok request -> execute scope request context
                | Error decodeError -> task { return consequence decodeError } }
