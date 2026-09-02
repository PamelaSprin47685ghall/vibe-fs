namespace Wanxiangshu.OpenCode.Host

open System.Threading.Tasks

module RequirementGroundingRepositorySurface =
    val dispose: runtime: obj -> unit
    val runFirstAttempt: workspace: string -> sessionId: string -> program: string -> Task<obj>
    val runWithObservationFailure: workspace: string -> sessionId: string -> program: string -> Task<obj>
