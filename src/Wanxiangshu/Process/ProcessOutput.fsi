namespace Wanxiangshu.Process

open System.Collections.Generic

module ProcessOutput =
    type OutputCollector =
        { mutable Stdout: List<byte array>
          mutable Stderr: List<byte array>
          mutable Combined: List<byte array>
          mutable Spool: Spool.StreamingSpool option
          mutable BytesObserved: int64
          mutable Discarded: bool
          OutputLimit: int64 }

    val create: estimate: ProcessEstimate -> OutputCollector
    val createWithOutputLimit: outputLimit: int64 -> OutputCollector
    val addStdout: collector: OutputCollector -> bytes: byte array -> unit
    val addStderr: collector: OutputCollector -> bytes: byte array -> unit
    val discard: collector: OutputCollector -> unit
    val decodeText: bytes: byte array -> string
    val buildResult: collector: OutputCollector -> CapturedOutput
