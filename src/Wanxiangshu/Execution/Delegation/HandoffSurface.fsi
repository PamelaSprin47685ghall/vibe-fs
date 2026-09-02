namespace Wanxiangshu.Execution.Delegation

[<RequireQualifiedAccess>]
module HandoffSurface =
    val handoffWindow: previousEnd: obj -> currentEnd: int -> obj
    val render: charge: string -> parentRecord: string -> string
    val childRange: startInclusive: int -> endExclusive: int -> obj
