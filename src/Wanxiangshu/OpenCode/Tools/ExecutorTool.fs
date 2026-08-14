namespace Wanxiangshu.OpenCode

open System
open System.Threading
open System.Threading.Tasks
open ToolHostCodec
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Execution.Session.Recovery.SessionRecovery
open Wanxiangshu.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Process
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

/// Bounded command execution. Provider verbs: `run` (DevOps) and `query-shell` (Inspector).
module ExecutorTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<RequireQualifiedAccess>]
        module Run =
            [<Literal>]
            let Description = "tool/run/description"

            [<Literal>]
            let ArgCommand = "tool/run/arg-command"

            [<Literal>]
            let ArgDeadlineSeconds = "tool/run/arg-deadline_seconds"

            [<Literal>]
            let ArgOutputBudgetBytes = "tool/run/arg-output_budget_bytes"

            [<Literal>]
            let ArgWorldLock = "tool/run/arg-world_lock"

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
            let ArgCommand = "tool/query-shell/arg-command"

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
        let language = ProviderLanguageBinding.readGlobalPreference ()

        { Name = "run"
          Description = prose language Path.Run.Description
          Arguments =
            [ "command", ToolHostCodec.stringSchemaDescribed (prose language Path.Run.ArgCommand) factory
              "deadline_seconds",
              ToolHostCodec.numberSchemaDescribed (prose language Path.Run.ArgDeadlineSeconds) factory
              "output_budget_bytes",
              ToolHostCodec.numberSchemaDescribed (prose language Path.Run.ArgOutputBudgetBytes) factory
              "world_lock", ToolHostCodec.boolSchemaDescribed (prose language Path.Run.ArgWorldLock) factory ]
          Execute =
            fun args context ->
                match decodeRun (lang context) args with
                | Ok request -> execute scope request context
                | Error decodeError -> task { return consequence decodeError } }

    let queryShellSpec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        let language = ProviderLanguageBinding.readGlobalPreference ()

        { Name = "query-shell"
          Description = prose language Path.QueryShell.Description
          Arguments =
            [ "command", ToolHostCodec.stringSchemaDescribed (prose language Path.QueryShell.ArgCommand) factory ]
          Execute =
            fun args context ->
                match decodeQueryShell (lang context) args with
                | Ok request -> execute scope request context
                | Error decodeError -> task { return consequence decodeError } }
