namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
module DelegatedToolEstimateSurface =
    let replay (expected: int) (calls: string array) : obj =
        let state =
            calls
            |> Array.fold
                (fun state call -> DelegatedToolEstimateProjection.observe (ToolCallId.create call) state)
                (DelegatedToolEstimateProjection.replace expected)

        box
            {| remaining = DelegatedToolEstimateProjection.remaining state
               countedCalls = DelegatedToolEstimateProjection.countedCallCount state |}
