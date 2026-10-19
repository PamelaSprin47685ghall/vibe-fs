namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Context.Prefix
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation

module ProjectionUpdate =
    val prefixOutcome:
        factName: string -> projection: 'a -> result: Result<'a, PrefixFoldRejection> -> Result<'a, FoldRejection>

    val updateSession:
        sessionId: SessionId ->
        apply: (SessionAgentProjection -> SessionAgentProjection) ->
        projection: AgentProjectionSet ->
            AgentProjectionSet

    val tryUpdatePrefix:
        sessionId: SessionId ->
        apply: (ActivePrefixEpoch -> Result<ActivePrefixEpoch, 'rejection>) ->
        projection: AgentProjectionSet ->
            Result<AgentProjectionSet, 'rejection>

    val retireAuxiliaryInjectionVisibility: session: SessionAgentProjection -> SessionAgentProjection

    // Single-field fact families: the domain fold owns the slice decision and
    // composition only writes the slice back (delegation-029 / durable-events-023).

    val applyFission:
        projection: AgentProjectionSet -> fact: FissionFactCases -> Result<AgentProjectionSet, FoldRejection>
