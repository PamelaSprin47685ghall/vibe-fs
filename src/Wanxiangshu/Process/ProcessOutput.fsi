namespace Wanxiangshu.Process

/// Bounded stdout/stderr collection for one run. The memory bound, the switch to
/// a complete spool file and its failures belong to `wanxiangli/spool`; this module
/// chooses the bound from the estimate and maps the library result to an outcome.
module ProcessOutput =
    type OutputCollector

    val create: estimate: ProcessEstimate -> OutputCollector
    val addStdout: collector: OutputCollector -> bytes: byte array -> unit
    val addStderr: collector: OutputCollector -> bytes: byte array -> unit
    val outputLimit: collector: OutputCollector -> int64
    val bytesObserved: collector: OutputCollector -> int64
    val bytesHeld: collector: OutputCollector -> int64
    val spooled: collector: OutputCollector -> bool
    /// Seals the collector. A spool write failure is an execution failure: the
    /// partial file is removed and the outcome never claims complete output.
    val buildResult: collector: OutputCollector -> exitCode: int -> Result<ProcessOutcome, ProcessError>
    /// Releases the collector without an outcome (timeout, cancellation): its spool file is removed.
    val abandon: collector: OutputCollector -> unit
