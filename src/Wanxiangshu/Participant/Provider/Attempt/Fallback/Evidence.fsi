namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority

module FallbackEvidence =
    val tryCurrentState: sessionId: SessionId -> projection: ProjectionSet -> FallbackProjection option

    val effectiveAgent:
        sessionId: SessionId ->
        projection: ProjectionSet ->
        profile: PromptAuthority.AuthorityExecutionProfile ->
            string
