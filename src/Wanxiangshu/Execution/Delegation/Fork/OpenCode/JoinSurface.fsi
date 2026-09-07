namespace Wanxiangshu.Execution.Delegation.Fork.OpenCode

/// Delegation-owned join wire surface. Inputs and outputs are plain JavaScript
/// data; completion and error unions remain inside the renderer owner.
open System.Threading.Tasks

[<RequireQualifiedAccess>]
module JoinSurface =
    val renderBatch: languageName: string -> items: obj array -> string
    val renderInterrupted: languageName: string -> reason: string -> string
    val renderForkError: languageName: string -> error: string -> string
    val renderOrchestratorBatch: languageName: string -> verdictNames: string array -> string
    val createJoinProbe: unit -> obj
    val joinProbeForkPty: probe: obj -> command: string -> Task<obj>
    val joinProbeCompletePty: probe: obj -> ptyId: string -> unit
    val joinProbePulseWake: probe: obj -> unit
    val joinProbeCancel: probe: obj -> unit
    val joinProbeCounts: probe: obj -> obj
    val createJoinInterrupt: unit -> obj
    val fireJoinInterrupt: handle: obj -> reason: string -> unit
    val joinAvailable: probe: obj -> maxCount: int -> interrupt: obj -> Task<obj>
    val joinAvailableWithPermit: probe: obj -> permitSequence: int -> maxCount: int -> interrupt: obj -> Task<obj>
    val joinMaxBatch: unit -> int
