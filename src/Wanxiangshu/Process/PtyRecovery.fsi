namespace Wanxiangshu.Process

open System.Threading.Tasks

module internal PtyRecovery =
    val start: binding: PtyRecoveryBinding -> command: string -> cwd: string -> obj
    val drain: directory: string -> key: string -> incarnationId: string option -> graceMs: int -> Task<PtyRecoveryExit>
