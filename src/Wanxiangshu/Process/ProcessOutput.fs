namespace Wanxiangshu.Process

open System
open System.Collections.Generic
open System.Text

/// stdout/stderr aggregation, byte counting and spool-threshold handling.
module ProcessOutput =

    /// DSL-state-combination: physical — stdout/stderr byte-buffer spool state
    type OutputCollector =
        { mutable Stdout: List<byte[]>
          mutable Stderr: List<byte[]>
          mutable Combined: List<byte[]>
          mutable Spool: Spool.StreamingSpool option
          mutable BytesObserved: int64
          OutputLimit: int64 }

    let create (estimate: ProcessEstimate) : OutputCollector =
        { Stdout = List<byte[]>()
          Stderr = List<byte[]>()
          Combined = List<byte[]>()
          Spool = None
          BytesObserved = 0L
          OutputLimit = ProcessEstimate.outputThreshold estimate.EstimatedOutput }

    /// 内存积压封顶：超过该预算即在到达 OutputLimit 之前切到 spool，避免跨过
    /// OutputLimit 那一刻把积压的全部字节一次性同步 dump。与 Spool 分块一致。
    let private MemoryBufferBudget: int64 = 204800L

    let private switchToSpool collector =
        let active = Spool.startStreamingSpool ()

        for previous in collector.Combined do
            Spool.appendStreamingSpool active previous

        collector.Combined.Clear()
        collector.Stdout.Clear()
        collector.Stderr.Clear()
        collector.Spool <- Some active

    let private appendNewMemory (collector: OutputCollector) (target: List<byte[]>) (bytes: byte[]) =
        let switchThreshold = min collector.OutputLimit MemoryBufferBudget
        target.Add bytes
        collector.Combined.Add bytes

        if collector.BytesObserved > switchThreshold then
            switchToSpool collector

    let private appendInMemory collector target bytes =
        match collector.Spool with
        | Some active -> Spool.appendStreamingSpool active bytes
        | None -> appendNewMemory collector target bytes

    let private addChunk (collector: OutputCollector) (target: List<byte[]>) (bytes: byte[]) =
        if not (isNull bytes) && bytes.Length > 0 then
            collector.BytesObserved <- collector.BytesObserved + int64 bytes.Length
            appendInMemory collector target bytes

    let addStdout (collector: OutputCollector) (bytes: byte[]) : unit =
        addChunk collector collector.Stdout bytes

    let addStderr (collector: OutputCollector) (bytes: byte[]) : unit =
        addChunk collector collector.Stderr bytes

    let private concatBytes (parts: List<byte[]>) =
        if parts.Count = 0 then
            [||]
        else
            parts |> Seq.toArray |> Array.concat

    let buildResult (collector: OutputCollector) (exitCode: int) : ProcessOutcome =
        match collector.Spool with
        | Some active ->
            ProcessOutcome.Spooled(exitCode, active.Path, active.BytesWritten, Spool.chunkCount active.BytesWritten)
        | None ->
            let stdoutBytes = concatBytes collector.Stdout
            let stderrBytes = concatBytes collector.Stderr
            let stdoutText = Encoding.UTF8.GetString(stdoutBytes, 0, stdoutBytes.Length)
            let stderrText = Encoding.UTF8.GetString(stderrBytes, 0, stderrBytes.Length)

            ProcessOutcome.Completed(exitCode, stdoutText, stderrText, false)
