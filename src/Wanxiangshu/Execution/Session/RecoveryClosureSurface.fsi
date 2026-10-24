namespace Wanxiangshu.Execution.Session

[<RequireQualifiedAccess>]
module RecoveryClosureSurface =
    val closureTokens: root: string -> candidates: obj array -> journalSequence: int -> string array
