namespace Wanxiangshu.Execution.Delegation.Fork.OpenCode

/// Delegation-owned join wire surface. Inputs and outputs are plain JavaScript
/// data; completion and error unions remain inside the renderer owner.
[<RequireQualifiedAccess>]
module JoinSurface =
    val renderBatch: languageName: string -> items: obj array -> string
    val renderInterrupted: languageName: string -> reason: string -> string
    val renderForkError: languageName: string -> error: string -> string
    val renderOrchestratorBatch: languageName: string -> verdictNames: string array -> string
