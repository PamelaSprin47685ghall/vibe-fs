namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Change
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority

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

    val updateOrchestrator:
        apply: (OrchestratorProjection -> OrchestratorProjection) ->
        projection: AgentProjectionSet ->
            AgentProjectionSet

    val updateAuthority:
        sessionId: SessionId ->
        apply: (PromptAuthority.PromptAuthorityProjection -> PromptAuthority.PromptAuthorityProjection) ->
        projection: AgentProjectionSet ->
            AgentProjectionSet
