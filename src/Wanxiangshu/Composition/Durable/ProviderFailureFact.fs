namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Participant.Provider.Attempt.Fallback

/// Provider failure fact constructors — bridge from ProviderFailure-owned fact cases
/// into the Composition-owned AgentFact outer routing union.
module ProviderFailureFact =
    let inline FailureRecorded payload =
        AgentFact.ProviderFailure(ProviderFailureFactCases.FailureRecorded payload)

    let inline RetryExhausted payload =
        AgentFact.ProviderFailure(ProviderFailureFactCases.RetryExhausted payload)

    let inline SuccessRecorded payload =
        AgentFact.ProviderFailure(ProviderFailureFactCases.SuccessRecorded payload)
