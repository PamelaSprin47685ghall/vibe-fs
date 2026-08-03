namespace rec Wanxiangshu.Process

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

    /// EXEC-011 fallback ceiling: one hour.
    ///
    /// A real finite value, not a sentinel. The previous code clamped to 36500 days
    /// and called that a bound; at that scale a runaway process is indistinguishable
    /// from an unbounded one, which is exactly what "hard limit 必须有限" rules out.
    let DefaultHardLimit = TimeSpan.FromHours 1.0

    /// EXEC-011: `min(3 × estimated_running_secs, configured hard limit)`.
    ///
    /// The LLM's estimate may not exceed the administrator's ceiling, so this is a
    /// `min` and not a `max`. Non-finite and negative estimates collapse to the
    /// ceiling rather than being rejected here: the estimate is model-supplied, and
    /// `validateEstimate` owns refusing a malformed one.
    let effectiveDeadline (RuntimeSeconds seconds) (hardLimit: TimeSpan) =
        let modelBudget =
            let total = 3.0 * seconds

            if Double.IsNaN total || Double.IsInfinity total || total <= 0.0 then
                hardLimit
            else
                TimeSpan.FromSeconds total

        min modelBudget hardLimit

    let outputThreshold (OutputBytes bytes) =
        if bytes <= 0L then 0L
        elif bytes > Int64.MaxValue / 3L then Int64.MaxValue
        else bytes * 3L
