namespace Wanxiangshu.Host

open Wanxiangshu.Composition.Durable.Fact

module HostFact =
    let inline PairProgrammingGuidelineAnchored payload =
        AgentFact.Host(HostFactCases.PairProgrammingGuidelineAnchored payload)

    let inline RequirementGroundingRequested payload =
        AgentFact.Host(HostFactCases.RequirementGroundingRequested payload)

    let inline RequirementGroundingMaterialObserved payload =
        AgentFact.Host(HostFactCases.RequirementGroundingMaterialObserved payload)

    let inline RequirementGroundingAnchored payload =
        AgentFact.Host(HostFactCases.RequirementGroundingAnchored payload)

    let inline TipGuidanceDelivered payload =
        AgentFact.Host(HostFactCases.TipGuidanceDelivered payload)

    let inline SessionStartedAtBound payload =
        AgentFact.Host(HostFactCases.SessionStartedAtBound payload)
