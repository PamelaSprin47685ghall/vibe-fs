namespace Wanxiangshu.Execution.Delegation

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
module DelegatedToolEstimateLedger =
    val tryState: port: DelegatedToolEstimatePort -> sessionId: SessionId -> DelegatedToolEstimateProjectionState option
    val tryRemaining: port: DelegatedToolEstimatePort -> sessionId: SessionId -> int option
    val replace: port: DelegatedToolEstimatePort -> sessionId: SessionId -> expectedToolCalls: int -> Task<unit>
    val observe: port: DelegatedToolEstimatePort -> sessionId: SessionId -> toolCallId: ToolCallId -> Task<unit>
