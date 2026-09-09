namespace Wanxiangshu.Context.Companion.Blogger

open System.Threading.Tasks
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
module BloggerMainContext =

    /// Pure construction of a Blogger main request from one delta chunk.
    /// Refuses at birth when coverage cannot strictly advance: an unmapped
    /// next cursor or a next sequence at or below the previous ingested
    /// sequence returns None so no BloggerMain is started.
    val mainContextFromChunk:
        mainSessionId: SessionId ->
        bloggerSessionId: SessionId ->
        observedEpoch: PrefixEpochId ->
        blog: BlogProjectionState ->
        xTrace: XTraceProjectionState ->
        projection: ProviderProjection.ProviderSemanticProjection ->
        chunk: BloggerDeltaChunk ->
            BloggerRequestContext option

    /// True when the current XTrace/Blog state can produce a next Blogger chunk.
    val hasMaterial:
        journal: AgentJournal option ->
        mainSessionId: SessionId ->
        blog: BlogProjectionState ->
        xTrace: XTraceProjectionState ->
        projection: ProviderProjection.ProviderSemanticProjection ->
            bool

    /// Pure derivation of a Blogger main request from durable projection state.
    val fromProjection:
        journal: AgentJournal option ->
        mainSessionId: SessionId ->
        bloggerSessionId: SessionId ->
        observedEpoch: PrefixEpochId ->
        blog: BlogProjectionState ->
        xTrace: XTraceProjectionState ->
        projection: ProviderProjection.ProviderSemanticProjection ->
            BloggerRequestContext option

    /// Async derivation from the live journal, honoring runtime Host flight/drain gates.
    val fromJournal:
        scope: IBloggerRuntimeHost ->
        journal: AgentJournal ->
        mainSessionId: SessionId ->
        bloggerSessionId: SessionId ->
            Task<BloggerRequestContext option>
