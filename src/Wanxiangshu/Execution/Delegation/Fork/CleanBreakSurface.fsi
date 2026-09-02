namespace Wanxiangshu.Execution.Delegation.Fork

/// Clean-break owner surface for legacy completion handling. Decoder branches
/// are plain data; no DTO union leaks.
[<RequireQualifiedAccess>]
module CleanBreakSurface =
    val legacyBody: runId: string -> string
    val decode: body: string -> obj
    val tryDecode: handle: string -> body: string -> obj
    val joinWire: agentName: string -> message: string -> string
