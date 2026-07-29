namespace rec Wanxiangshu.Next.Process

open System

/// PTY geometry, kept on the command record for round-trip but ignored by the
/// process runner (handled by the PTY pipeline).
type PtyOptions = { Cols: int; Rows: int }

/// Stable public protocol for one process execution.
type Command =
    { FileName: string
      Arguments: string list
      WorkingDirectory: string option
      Environment: Map<string, string> option
      Stdin: string option
      Deadline: Deadline option
      PtyOptions: PtyOptions option }

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
    { WorkingDirectory: string option
      DefaultTimeout: TimeSpan option }

[<RequireQualifiedAccess>]
type ProcessOutcome =
    | Completed of exitCode: int * stdout: string * stderr: string * spooled: bool
    | Spooled of exitCode: int * spoolPath: string * totalBytes: int64 * chunkCount: int

[<RequireQualifiedAccess>]
type ProcessError =
    | TimeoutExceeded of duration: TimeSpan
    | SpawnFailed of reason: string
    | ProcessCancelled of reason: string
    | ExecutionFailed of reason: string

module ProcessEstimate =
    let private maxBudgetSeconds = TimeSpan.MaxValue.TotalSeconds

    let budget (RuntimeSeconds seconds) =
        let total = 3.0 * seconds
        if Double.IsNaN total || Double.IsInfinity total then
            TimeSpan.MaxValue
        else
            let safe = Math.Min(total, maxBudgetSeconds)
            TimeSpan.FromSeconds safe

    let outputThreshold (OutputBytes bytes) =
        if bytes <= 0L then 0L
        elif bytes > Int64.MaxValue / 3L then Int64.MaxValue
        else bytes * 3L
