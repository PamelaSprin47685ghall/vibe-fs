namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Foundation.Identity

type DelegatedToolEstimateProjectionState =
    { Remaining: int
      CountedToolCalls: Set<ToolCallId> }

[<RequireQualifiedAccess>]
module DelegatedToolEstimateProjection =
    val replace: expectedToolCalls: int -> DelegatedToolEstimateProjectionState
    val observe: toolCallId: ToolCallId -> state: DelegatedToolEstimateProjectionState -> DelegatedToolEstimateProjectionState
    val remaining: state: DelegatedToolEstimateProjectionState -> int
    val countedCallCount: state: DelegatedToolEstimateProjectionState -> int
