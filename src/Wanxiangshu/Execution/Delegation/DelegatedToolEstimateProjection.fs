namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Kernel.Identity

type DelegatedToolEstimateProjectionState =
    { Remaining: int
      CountedToolCalls: Set<ToolCallId> }

[<RequireQualifiedAccess>]
module DelegatedToolEstimateProjection =

    let replace expectedToolCalls =
        { Remaining = expectedToolCalls
          CountedToolCalls = Set.empty }

    let observe toolCallId state =
        if state.Remaining = 0 || Set.contains toolCallId state.CountedToolCalls then
            state
        else
            { Remaining = state.Remaining - 1
              CountedToolCalls = Set.add toolCallId state.CountedToolCalls }

    let remaining state = state.Remaining

    let countedCallCount state = Set.count state.CountedToolCalls
