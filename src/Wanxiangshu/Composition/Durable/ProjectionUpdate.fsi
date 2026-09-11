namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Enforcer.InstitutionalLearning
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Attention
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Concern

module ProjectionUpdate =
    val prefixOutcome:
        factName: string -> projection: 'a -> result: Result<'a, PrefixFoldRejection> -> Result<'a, FoldRejection>

    val updateSession:
        sessionId: SessionId ->
        apply: (SessionAgentProjection -> SessionAgentProjection) ->
        projection: AgentProjectionSet ->
            AgentProjectionSet

    val updateCompanion:
        sessionId: SessionId ->
        apply: (CompanionProjection -> CompanionProjection) ->
        projection: AgentProjectionSet ->
            AgentProjectionSet

    val tryUpdateBlog:
        sessionId: SessionId ->
        apply: (BlogProjectionState -> Result<BlogProjectionState, 'rejection>) ->
        projection: AgentProjectionSet ->
            Result<AgentProjectionSet, 'rejection>

    val tryUpdatePrefix:
        sessionId: SessionId ->
        apply: (ActivePrefixEpoch -> Result<ActivePrefixEpoch, 'rejection>) ->
        projection: AgentProjectionSet ->
            Result<AgentProjectionSet, 'rejection>

    val retireAuxiliaryInjectionVisibility: session: SessionAgentProjection -> SessionAgentProjection

    val updateAuthority:
        sessionId: SessionId ->
        apply: (PromptAuthority.PromptAuthorityProjection -> PromptAuthority.PromptAuthorityProjection) ->
        projection: AgentProjectionSet ->
            AgentProjectionSet

    // Single-field fact families: the domain fold owns the slice decision and
    // composition only writes the slice back (DELEG-029 / DURABLE-EVENTS-023).

    val applyFission:
        projection: AgentProjectionSet -> fact: FissionFactCases -> Result<AgentProjectionSet, FoldRejection>

    val applyConcern:
        projection: AgentProjectionSet -> fact: ConcernFactCases -> Result<AgentProjectionSet, FoldRejection>

    val applyAttention:
        projection: AgentProjectionSet -> fact: AttentionFactCases -> Result<AgentProjectionSet, FoldRejection>

    val applyAttentionLearning:
        projection: AgentProjectionSet ->
        fact: InstitutionalLearningFactCases ->
            Result<AgentProjectionSet, FoldRejection>

    val applyInstitutionalLearning:
        projection: AgentProjectionSet ->
        fact: InstitutionalLearningFactCases ->
            Result<AgentProjectionSet, FoldRejection>
