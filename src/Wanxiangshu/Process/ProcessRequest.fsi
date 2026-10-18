namespace Wanxiangshu.Process

open System
open System.Threading
open System.Threading.Tasks

type Command =
    { FileName: string
      Arguments: string list
      WorkingDirectory: string option
      Environment: Map<string, string> option
      Stdin: string option }

and EstimatedRuntime = RuntimeSeconds of float

and EstimatedOutput = OutputBytes of int64

and [<RequireQualifiedAccess>] EstimatedMemory =
    | Medium
    | Large

and ProcessEstimate =
    { EstimatedRuntime: EstimatedRuntime
      EstimatedOutput: EstimatedOutput
      EstimatedMemory: EstimatedMemory }

and ProcessContext =
    { WorkingDirectory: string option
      HardLimit: TimeSpan }

and [<RequireQualifiedAccess>] ProgramEnd = Exited of int | Signaled of string
and [<RequireQualifiedAccess>] EndEvidence = Confirmed of ProgramEnd | Unconfirmed
and [<RequireQualifiedAccess>] StopRequest =
    | CancellationRequested
    | DeadlineStopRequested of TimeSpan
and [<RequireQualifiedAccess>] CapturedOutput =
    | InMemory of stdout: string * stderr: string
    | Spooled of path: string * bytes: int64 * chunks: int
and [<RequireQualifiedAccess>] NoExecution =
    | InvalidRequest of string
    | CancelledBeforeStart
    | StartRejected of string
and ProcessObservation =
    { End: EndEvidence
      StopRequested: StopRequest option
      Output: CapturedOutput option
      Fault: string option }
and [<RequireQualifiedAccess>] ProcessResult =
    | NotStarted of NoExecution
    | Observed of ProcessObservation

type TextProcessExit = { ExitCode: int; Stdout: string; Stderr: string }
type TextProcessRunner = Command -> Task<Result<TextProcessExit, ProcessResult>>

type RunProcess =
    Command -> ProcessEstimate -> ProcessContext -> CancellationToken -> Task<ProcessResult>

module ProcessResult =
    val requireText: ProcessResult -> Result<TextProcessExit, ProcessResult>
    val describe: ProcessResult -> string

module ProcessEstimate =
    val DefaultHardLimit: TimeSpan
    val effectiveDeadline: EstimatedRuntime -> hardLimit: TimeSpan -> TimeSpan
    val outputThreshold: EstimatedOutput -> int64
