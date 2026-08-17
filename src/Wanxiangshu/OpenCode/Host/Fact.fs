namespace Wanxiangshu.OpenCode.Host

open Wanxiangshu.Composition.Durable.Fact

/// Host fact constructors — bridge from Host-owned HostFactCases
/// into the Composition-owned AgentFact outer routing union.
module HostFact =
    let inline PairProgrammingGuidelineAnchored payload =
        AgentFact.Host(HostFactCases.PairProgrammingGuidelineAnchored payload)

    let inline TipGuidanceDelivered payload =
        AgentFact.Host(HostFactCases.TipGuidanceDelivered payload)

    let inline SessionStartedAtBound payload =
        AgentFact.Host(HostFactCases.SessionStartedAtBound payload)
