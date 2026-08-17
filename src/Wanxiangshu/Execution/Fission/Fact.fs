namespace Wanxiangshu.Execution.Fission

open Wanxiangshu.Composition.Durable.Fact

/// Fission fact constructors — bridge from Fission-owned FissionFactCases
/// into the Composition-owned AgentFact outer routing union.
module FissionFact =
    let inline FissionAdmitted payload =
        AgentFact.Fission(FissionFactCases.FissionAdmitted payload)

    let inline FissionLaneMaterialized payload =
        AgentFact.Fission(FissionFactCases.FissionLaneMaterialized payload)

    let inline FissionCompletionCaptured payload =
        AgentFact.Fission(FissionFactCases.FissionCompletionCaptured payload)

    let inline FissionCompletionDelivered payload =
        AgentFact.Fission(FissionFactCases.FissionCompletionDelivered payload)

    let inline FissionExternalAffinityBound payload =
        AgentFact.Fission(FissionFactCases.FissionExternalAffinityBound payload)

    let inline FissionTakeoverStarted payload =
        AgentFact.Fission(FissionFactCases.FissionTakeoverStarted payload)

    let inline FissionConverged payload =
        AgentFact.Fission(FissionFactCases.FissionConverged payload)

    let inline FissionFailed payload =
        AgentFact.Fission(FissionFactCases.FissionFailed payload)
