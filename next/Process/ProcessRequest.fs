namespace Wanxiangshu.Next.Process

open System
open System.Threading
open System.Threading.Tasks

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
    | OutputExceeded of bytesWritten: int64 * spoolPath: string option

[<RequireQualifiedAccess>]
type ProcessError =
    | TimeoutExceeded of duration: TimeSpan
    | SpawnFailed of reason: string
    | ProcessCancelled of reason: string
    | ExecutionFailed of reason: string

module ProcessEstimate =
    let budget (RuntimeSeconds seconds) = TimeSpan.FromSeconds(3.0 * seconds)

    let outputThreshold (OutputBytes bytes) =
        if bytes <= 0L then 0L
        elif bytes > Int64.MaxValue / 3L then Int64.MaxValue
        else bytes * 3L
