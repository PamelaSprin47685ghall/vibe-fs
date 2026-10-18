namespace rec Wanxiangshu.Process

open System
open System.Threading
open System.Threading.Tasks

/// Stable public protocol for one process execution.
type Command =
    { FileName: string
      Arguments: string list
      WorkingDirectory: string option
      Environment: Map<string, string> option
      Stdin: string option }

type EstimatedRuntime = RuntimeSeconds of float
type EstimatedOutput = OutputBytes of int64

[<RequireQualifiedAccess>]
type EstimatedMemory =
    | Medium
    | Large

type ProcessEstimate =
    { EstimatedRuntime: EstimatedRuntime
      EstimatedOutput: EstimatedOutput
      EstimatedMemory: EstimatedMemory }

type ProcessContext =
    {
        WorkingDirectory: string option
        /// EXEC-011: the administrator's ceiling on any single process.
        ///
        /// Not optional. The clause requires a finite hard limit, and an `option` here
        /// would make "no limit configured" expressible — which is the unbounded case
        /// it forbids. Callers that have no configuration pass
        /// `ProcessEstimate.DefaultHardLimit`.
        HardLimit: TimeSpan
    }

[<RequireQualifiedAccess>]
type ProgramEnd =
    | Exited of int
    | Signaled of string

[<RequireQualifiedAccess>]
type EndEvidence =
    | Confirmed of ProgramEnd
    | Unconfirmed

[<RequireQualifiedAccess>]
type StopRequest =
    | CancellationRequested
    | DeadlineStopRequested of TimeSpan

[<RequireQualifiedAccess>]
type CapturedOutput =
    | InMemory of stdout: string * stderr: string
    | Spooled of path: string * bytes: int64 * chunks: int

[<RequireQualifiedAccess>]
type NoExecution =
    | InvalidRequest of string
    | CancelledBeforeStart
    | StartRejected of string

type ProcessObservation =
    { End: EndEvidence
      StopRequested: StopRequest option
      Output: CapturedOutput option
      Fault: string option }

[<RequireQualifiedAccess>]
type ProcessResult =
    | NotStarted of NoExecution
    | Observed of ProcessObservation

type TextProcessExit =
    { ExitCode: int
      Stdout: string
      Stderr: string }

type TextProcessRunner = Command -> Task<Result<TextProcessExit, ProcessResult>>

type RunProcess =
    Command -> ProcessEstimate -> ProcessContext -> CancellationToken -> Task<ProcessResult>

module ProcessResult =
    let requireText result =
        match result with
        | ProcessResult.Observed
            { End = EndEvidence.Confirmed(ProgramEnd.Exited code)
              StopRequested = None
              Output = Some(CapturedOutput.InMemory(stdout, stderr))
              Fault = None } ->
            Ok { ExitCode = code; Stdout = stdout; Stderr = stderr }
        | other -> Error other

    let private outputDescription output =
        match output with
        | None -> "not delivered"
        | Some(CapturedOutput.InMemory _) -> "complete text"
        | Some(CapturedOutput.Spooled _) -> "mixed-channel spool cannot supply separate text streams"

    let describe result =
        match result with
        | ProcessResult.NotStarted reason -> sprintf "Process not started: %A" reason
        | ProcessResult.Observed observation ->
            sprintf "Process end: %A; stop: %A; output: %s; fault: %A"
                observation.End observation.StopRequested (outputDescription observation.Output) observation.Fault

module ProcessEstimate =

    /// EXEC-011 fallback ceiling: one hour.
    ///
    /// A real finite value, not a sentinel. The previous code clamped to 36500 days
    /// and called that a bound; at that scale a runaway process is indistinguishable
    /// from an unbounded one, which is exactly what "hard limit 必须有限" rules out.
    let DefaultHardLimit = TimeSpan.FromHours 1.0

    /// EXEC-011 / GrandRewrite: `min(deadline_seconds, configured hard limit)`.
    /// Provider willingness is applied at face value — no ×3 inflation.
    let effectiveDeadline (RuntimeSeconds seconds) (hardLimit: TimeSpan) =
        let modelBudget =
            if Double.IsNaN seconds || Double.IsInfinity seconds || seconds <= 0.0 then
                hardLimit
            else
                TimeSpan.FromSeconds seconds

        min modelBudget hardLimit

    let outputThreshold (OutputBytes bytes) = if bytes <= 0L then 0L else bytes
