namespace Wanxiangshu.Process

open System.Threading.Tasks

module PtyBackend =
    val createPort: unit -> PtyPort

    val recover:
        directory: string -> key: string -> incarnationId: string option -> graceMs: int -> Task<PtyRecoveryExit>
