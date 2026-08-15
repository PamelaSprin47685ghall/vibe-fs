namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// JS-native semantic surface for the delegated tool estimate projection
/// (DELEG-022, P6/P9 wave). State crosses as JSON-shaped data
/// ({ remaining: number, counted: string[] }); the F# Set<ToolCallId>
/// translation happens here at the owner boundary.
module DelegatedToolEstimateSurface =

    type EstimateState =
        {| Remaining: int
           Counted: string array |}

    let private toJson (state: DelegatedToolEstimateProjectionState) : EstimateState =
        {| Remaining = state.Remaining
           Counted =
               state.CountedToolCalls
               |> Set.toList
               |> List.map ToolCallId.value
               |> List.toArray |}

    let private ofJson (state: EstimateState) : DelegatedToolEstimateProjectionState =
        { Remaining = state.Remaining
          CountedToolCalls =
              state.Counted
              |> Array.toList
              |> List.map ToolCallId.create
              |> Set.ofList }

    /// Fresh state with an exact remaining budget and no counted calls.
    let replace (expectedToolCalls: int) : EstimateState =
        DelegatedToolEstimateProjection.replace expectedToolCalls |> toJson

    /// Count one distinct real tool call (idempotent per ToolCallId).
    let observe (toolCallId: string) (state: EstimateState) : EstimateState =
        DelegatedToolEstimateProjection.observe (ToolCallId.create toolCallId) (ofJson state)
        |> toJson

    let remaining (state: EstimateState) : int = state.Remaining

    let countedCallCount (state: EstimateState) : int = state.Counted.Length
