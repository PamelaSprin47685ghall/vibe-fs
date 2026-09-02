namespace Wanxiangshu.Execution.Delegation.Fork

/// Child-recovery owner surface. Durable/snapshot observations are strings and
/// resolution names; terminal proofs and trace events remain typed internally.
[<RequireQualifiedAccess>]
module ChildRecoverySurface =

    val resolve: durable: string -> snapshot: string -> observations: obj array -> body: string -> obj
    val provenTerminal: body: string -> obj
    val trace: events: obj array -> bool
