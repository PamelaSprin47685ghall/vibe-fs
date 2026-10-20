namespace Wanxiangshu.Process

open System.Text

/// Bounded stdout/stderr collection for one run. The memory bound, the switch to
/// a complete spool file and its failures belong to `wanxiangli/spool`; this module
/// chooses the bound from the estimate and maps the library result to an outcome.
module ProcessOutput =

    /// DSL-class: PhysicalHandle — one run's library spool; owner ProcessOutput, law process-execution-009, proof process-execution-009.
    type OutputCollector =
        { Spool: Spool.OutputSpool
          OutputLimit: int64 }

    /// 内存积压封顶：超过该预算即切到 spool，与 Spool 分块一致。
    let private MemoryBufferBudget: int64 = 204800L

    let create (estimate: ProcessEstimate) : OutputCollector =
        let limit = ProcessEstimate.outputThreshold estimate.EstimatedOutput

        { Spool = Spool.create (int (min limit MemoryBufferBudget))
          OutputLimit = limit }

    let addStdout (collector: OutputCollector) (bytes: byte[]) : unit =
        Spool.append collector.Spool "stdout" bytes

    let addStderr (collector: OutputCollector) (bytes: byte[]) : unit =
        Spool.append collector.Spool "stderr" bytes

    let outputLimit (collector: OutputCollector) = collector.OutputLimit

    let bytesObserved (collector: OutputCollector) = Spool.observed collector.Spool

    let bytesHeld (collector: OutputCollector) = Spool.held collector.Spool

    let spooled (collector: OutputCollector) = Spool.isFile collector.Spool

    let private decode (bytes: byte[]) =
        Encoding.UTF8.GetString(bytes, 0, bytes.Length)

    let buildResult (collector: OutputCollector) (exitCode: int) : Result<ProcessOutcome, ProcessError> =
        match Spool.finish collector.Spool with
        | Spool.Finished.Memory(stdout, stderr) ->
            Ok(ProcessOutcome.Completed(exitCode, decode stdout, decode stderr, false))
        | Spool.Finished.File(path, bytes) -> Ok(ProcessOutcome.Spooled(exitCode, path, bytes, Spool.chunkCount bytes))
        | Spool.Finished.Failed(observed, written, operation, code, message) ->
            Spool.discard collector.Spool

            Error(
                ProcessError.ExecutionFailed(
                    sprintf
                        "output spool %s failed (%s: %s) after %d of %d bytes; process exit code %d, output incomplete"
                        operation
                        (defaultArg code "unknown")
                        message
                        written
                        observed
                        exitCode
                )
            )

    let abandon (collector: OutputCollector) : unit = Spool.discard collector.Spool
