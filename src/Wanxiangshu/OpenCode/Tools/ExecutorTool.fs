namespace Wanxiangshu.OpenCode

open System
open System.Text
open System.Threading
open System.Threading.Tasks
open ToolHostCodec
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Session.Recovery.SessionRecovery
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host.Contract
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Process

/// Bounded command execution. Provider verb: `run` (DevOps).
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
            let CannotReadOutputUntilAuthority = "tool/run/cannot-read-output-until-authority"

            [<Literal>]
            let OutputTruncated = "tool/run/output-truncated"

            [<Literal>]
            let LargeOutputRecoveryBlocked = "tool/run/large-output-recovery-blocked"

    /// Provider-visible bounded execution with a raw output tail.
    [<Literal>]
    let RunToolName = "run"

    type Request =
        { Command: string
          DeadlineSeconds: float
          OutputBudgetBytes: int64
          WorldLock: bool }

    let private lang (ctx: HostToolContext) =
        ProviderLanguageBinding.forSessionText ctx.SessionId

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

    let private consequence (message: string) =
        tomlObjectWithInstructions [ message ] []

    let private observationFields (observation: ProcessObservation) =
        [ match observation.End with
          | EndEvidence.Confirmed _ -> yield "executed", TBool true
          | EndEvidence.Unconfirmed -> yield "execution_unconfirmed", TBool true
          match observation.End with
          | EndEvidence.Confirmed(ProgramEnd.Exited code) -> yield "exit_code", TInt code
          | EndEvidence.Confirmed(ProgramEnd.Signaled signal) -> yield "signal", TString signal
          | EndEvidence.Unconfirmed -> yield "end_unconfirmed", TBool true
          match observation.StopRequested with
          | Some StopRequest.CancellationRequested -> yield "stop_requested", TString "cancellation"
          | Some(StopRequest.DeadlineStopRequested duration) ->
              yield "stop_requested", TString "deadline"
              yield "deadline_ms", TInt64(int64 duration.TotalMilliseconds)
          | None -> ()
          yield "output_complete", TBool observation.Output.IsSome
          yield "fault_present", TBool observation.Fault.IsSome ]

    let private observationConsequence observation message =
        tomlObjectWithInstructions [ message ] (observationFields observation)

    let private directoryFor (scope: ToolRuntimeScope) (context: HostToolContext) =
        if String.IsNullOrWhiteSpace context.SessionId then
            scope.WorkspaceDirectory
        else
            scope.DirectoryFor context.SessionId |> Option.orElse scope.WorkspaceDirectory

    let private estimatedMemory (worldLock: bool) =
        if worldLock then
            EstimatedMemory.Large
        else
            EstimatedMemory.Medium

    let private completedToml observation (stdout: string) (stderr: string) =
        let fields =
            observationFields observation @
                [ "stdout", TString(if isNull stdout then "" else stdout)
                  "stderr", TString(if isNull stderr then "" else stderr) ]

        tomlObject fields

    let internal formatSpooledOutcome observation (output: string) =
        tomlObject (observationFields observation @ [ "output", TString output ])

    let private formatTruncatedOutcome language observation output =
        let notice = prose language Path.Run.OutputTruncated

        tomlObject
            (observationFields observation @
            [ "output", TString output
              "output_truncated", TBool true
              "output_notice", TString notice ])

    let internal retainOutputTail limitBytes (output: string) =
        Encoding.UTF8.GetBytes output
        |> Spool.retainLatestBytes limitBytes [||]
        |> Spool.alignUtf8Tail
        |> ProcessOutput.decodeText

    let private utf8Length (text: string) = (Encoding.UTF8.GetBytes text).Length

    let private wireBytes (wire: string) = int64 (utf8Length wire)

    let private wireFits budgetBytes wire =
        wireBytes wire <= budgetBytes && ToolResultBound.fitsInWindow wire

    let private minimumOutputBudget language =
        let observations =
            [ EndEvidence.Confirmed(ProgramEnd.Exited Int32.MaxValue)
              EndEvidence.Confirmed(ProgramEnd.Signaled "SIGVTALRM")
              EndEvidence.Unconfirmed ]
            |> List.collect (fun ending ->
                [ None; Some StopRequest.CancellationRequested; Some(StopRequest.DeadlineStopRequested(TimeSpan.FromMilliseconds 922337203685477.0)) ]
                |> List.collect (fun stop ->
                    [ None; Some "" ] |> List.map (fun fault ->
                        { End = ending; StopRequested = stop; Output = None; Fault = fault })))
        [ yield consequence (prose language Path.Run.CannotRunFromContext)
          yield tomlObject [ "executed", TBool false; "not_started", TString "cancelled_before_start"; "diagnostic_truncated", TBool true; "diagnostic", TString "" ]
          for observation in observations do
              yield completedToml observation "" ""
              yield formatTruncatedOutcome language observation ""
              yield tomlObject (observationFields observation @ [ "diagnostic_truncated", TBool true; "diagnostic", TString "" ])
              yield observationConsequence observation (prose language Path.Run.CannotReadOutputUntilAuthority)
              yield observationConsequence observation (prose language Path.Run.LargeOutputRecoveryBlocked) ]
        |> List.map wireBytes
        |> List.max

    let private advanceTailSearch budgetBytes lower upper limit previous wire =
        if wireFits budgetBytes wire then
            limit + 1, upper, wire
        else
            lower, limit - 1, previous

    let private formatBoundedTail language budgetBytes observation (output: string) =
        let mutable lower = 0
        let mutable upper = utf8Length output
        let mutable result = formatTruncatedOutcome language observation ""

        while lower <= upper do
            let limit = lower + (upper - lower) / 2
            let wire = formatTruncatedOutcome language observation (retainOutputTail limit output)

            let nextLower, nextUpper, nextResult =
                advanceTailSearch budgetBytes lower upper limit result wire

            lower <- nextLower
            upper <- nextUpper
            result <- nextResult

        result

    let private diagnosticOutcome budgetBytes fields (diagnostic: string) =
        let complete = tomlObject (fields @ [ "diagnostic", TString diagnostic ])
        if wireFits budgetBytes complete then complete
        else
            let render detail = tomlObject (fields @ [ "diagnostic_truncated", TBool true; "diagnostic", TString detail ])
            let mutable lower = 0
            let mutable upper = utf8Length diagnostic
            let mutable result = render ""
            while lower <= upper do
                let limit = lower + (upper - lower) / 2
                let wire = render (retainOutputTail limit diagnostic)
                let nextLower, nextUpper, nextResult = advanceTailSearch budgetBytes lower upper limit result wire
                lower <- nextLower
                upper <- nextUpper
                result <- nextResult
            result

    let private readSpooledTail (language: ProviderLanguage) (budgetBytes: int64) observation (spoolPath: string) =
        task {
            let limitBytes = int (min (int64 ToolResultBound.HostMaxBytes) budgetBytes)
            let! tail = Spool.readLatestTail limitBytes spoolPath
            let rawTail = ProcessOutput.decodeText tail.Bytes
            let complete = formatSpooledOutcome observation rawTail

            return
                if not tail.Truncated && wireFits budgetBytes complete then
                    complete
                else
                    formatBoundedTail language budgetBytes observation rawTail
        }

    let private finalizeSpooledWithAuthority
        (scope: ToolRuntimeScope)
        (language: ProviderLanguage)
        (root: SessionId)
        (budgetBytes: int64)
        (observation: ProcessObservation)
        (spoolPath: string)
        =
        task {
            let! recovery = scope.RequireCurrentProcessJoin root

            match recovery with
            | FamilyRecovery.FamilyBlocked _ -> return observationConsequence observation (prose language Path.Run.LargeOutputRecoveryBlocked)
            | FamilyRecovery.FamilyWaiting _
            | FamilyRecovery.FamilyReady _ -> return! readSpooledTail language budgetBytes observation spoolPath
        }

    let private finalizeSpooledBody
        (scope: ToolRuntimeScope)
        (language: ProviderLanguage)
        (context: HostToolContext)
        (budgetBytes: int64)
        (observation: ProcessObservation)
        (spoolPath: string)
        =
        task {
            if String.IsNullOrWhiteSpace context.SessionId then
                return observationConsequence observation (prose language Path.Run.CannotReadOutputUntilAuthority)
            else
                return!
                    finalizeSpooledWithAuthority
                        scope
                        language
                        (SessionId.create context.SessionId)
                        budgetBytes
                        observation
                        spoolPath
        }

    let private readSpooledSafely scope language context budgetBytes observation spoolPath =
        task {
            try
                return! finalizeSpooledBody scope language context budgetBytes observation spoolPath
            with error ->
                let failed = { observation with Output = None; Fault = Some error.Message }
                return diagnosticOutcome budgetBytes (observationFields failed) error.Message
        }

    let private finalizeSpooled
        (scope: ToolRuntimeScope)
        (language: ProviderLanguage)
        (context: HostToolContext)
        (budgetBytes: int64)
        (observation: ProcessObservation)
        (spoolPath: string)
        =
        task {
            try
                return! readSpooledSafely scope language context budgetBytes observation spoolPath
            finally
                Spool.delete spoolPath
        }

    let private truncateCompleted
        (language: ProviderLanguage)
        (budgetBytes: int64)
        (observation: ProcessObservation)
        (fullBytes: byte array)
        =
        let limitBytes = int (min (int64 ToolResultBound.HostMaxBytes) budgetBytes)
        let rawTail = ProcessOutput.decodeText fullBytes |> retainOutputTail limitBytes
        formatBoundedTail language budgetBytes observation rawTail

    let private completedOutcome
        (language: ProviderLanguage)
        (budgetBytes: int64)
        (observation: ProcessObservation)
        (stdout: string)
        (stderr: string)
        =
        let fullBytes =
            Encoding.UTF8.GetBytes(stdout + (if String.IsNullOrEmpty stderr then "" else "\n" + stderr))

        let complete = completedToml observation stdout stderr

        if not (wireFits budgetBytes complete) then
            truncateCompleted language budgetBytes observation fullBytes
        else
            complete

    let private noExecutionDetail language reason =
        match reason with
        | NoExecution.InvalidRequest reason -> "invalid_request", reason
        | NoExecution.CancelledBeforeStart -> "cancelled_before_start", prose language Path.Run.Cancelled
        | NoExecution.StartRejected reason -> "start_rejected", reason

    let private notStartedOutcome language budget reason =
        let kind, message = noExecutionDetail language reason
        task { return diagnosticOutcome budget [ "executed", TBool false; "not_started", TString kind ] message }

    let private deleteSpooledOutput output =
        match output with
        | Some(CapturedOutput.Spooled(path, _, _)) -> Spool.delete path
        | _ -> ()

    let private observedOutcome scope request language context (observation: ProcessObservation) =
        match observation.Output, observation.Fault with
        | Some(CapturedOutput.InMemory(stdout, stderr)), None ->
            task { return completedOutcome language request.OutputBudgetBytes observation stdout stderr }
        | Some(CapturedOutput.Spooled(path, _, _)), None ->
            finalizeSpooled scope language context request.OutputBudgetBytes observation path
        | output, fault ->
            deleteSpooledOutput output
            let diagnostic = fault |> Option.defaultValue "Output was not delivered completely"
            task { return diagnosticOutcome request.OutputBudgetBytes (observationFields observation) diagnostic }

    let private interpretOutcome
        (scope: ToolRuntimeScope)
        (request: Request)
        (language: ProviderLanguage)
        (context: HostToolContext)
        (result: ProcessResult)
        =
        match result with
        | ProcessResult.NotStarted reason -> notStartedOutcome language request.OutputBudgetBytes reason
        | ProcessResult.Observed observation -> observedOutcome scope request language context observation

    let private runPrepared
        (run: RunProcess)
        (scope: ToolRuntimeScope)
        (request: Request)
        (context: HostToolContext)
        (language: ProviderLanguage)
        =
        task {
            let directory = directoryFor scope context

            let estimate =
                { EstimatedRuntime = RuntimeSeconds request.DeadlineSeconds
                  EstimatedOutput = OutputBytes request.OutputBudgetBytes
                  EstimatedMemory = estimatedMemory request.WorldLock }

            let command =
                { FileName = "sh"
                  Arguments = [ "-lc"; request.Command ]
                  WorkingDirectory = directory
                  Environment = None
                  Stdin = None }

            use cancellation = new CancellationTokenSource()
            let detachAbort = context.AttachAbort cancellation.Cancel

            let processContext: ProcessContext =
                { WorkingDirectory = directory
                  HardLimit = scope.ProcessHardLimit }

            let! result =
                task {
                    try
                        return! run command estimate processContext cancellation.Token
                    finally
                        detachAbort ()
                }

            return! interpretOutcome scope request language context result
        }

    let private executeInContext run (scope: ToolRuntimeScope) request context language =
        match scope.RuntimeFor context with
        | Error _ -> task { return consequence (prose language Path.Run.CannotRunFromContext) }
        | Ok _ -> runPrepared run scope request context language

    let private execute (run: RunProcess) (scope: ToolRuntimeScope) (request: Request) (context: HostToolContext) =
        task {
            let language = lang context
            let minimum = minimumOutputBudget language

            if request.OutputBudgetBytes < minimum then
                return
                    tomlObject
                        [ "executed", TBool false
                          "rejected", TString "output_budget_too_small"
                          "minimum_output_budget_bytes", TInt64 minimum ]
            else
                return! executeInContext run scope request context language
        }

    let runAdmission: ToolAdmission =
        ToolAdmission.OfficeRole(fun _ r -> r = Role.DevOps)

    let runSpec (run: RunProcess) (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
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
          Admission = runAdmission
          Execute =
            fun args context ->
                match decodeRun (lang context) args with
                | Ok request -> execute run scope request context
                | Error decodeError -> task { return consequence decodeError } }
