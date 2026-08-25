// primary_owner: provider-attempt-recovery — ProviderAttemptRecovery.ProjectionSurface — KEEP — FallbackFact bridge surface
namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open Wanxiangshu.Composition.Durable.Fact

/// Fallback fact constructors — bridge from Fallback-owned FallbackFactCases
/// into the Composition-owned AgentFact outer routing union.
module FallbackFact =
    let inline FallbackCursorAdvanced payload =
        AgentFact.Fallback(FallbackFactCases.FallbackCursorAdvanced payload)

    let inline FallbackExhausted payload =
        AgentFact.Fallback(FallbackFactCases.FallbackExhausted payload)

    let inline FallbackSucceeded payload =
        AgentFact.Fallback(FallbackFactCases.FallbackSucceeded payload)
