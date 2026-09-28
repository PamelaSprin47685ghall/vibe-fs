namespace Wanxiangshu.Context.Companion.Blogger.Runtime

open System.Threading.Tasks
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
module BloggerAbandon =
    val byRequestId:
        journal: AgentJournal ->
        requestId: BloggerRequestId ->
        mainSessionId: SessionId ->
        bloggerSessionId: SessionId ->
        reason: string ->
            Task

    val openRequest:
        journal: AgentJournal ->
        mainSessionId: SessionId ->
        bloggerSessionId: SessionId ->
        preferred: BloggerRequestContext option ->
        reason: string ->
            Task

    val staleOpenRequests:
        liveFlight: (SessionId -> BloggerRequestId -> bool) ->
        projections: Wanxiangshu.Composition.Durable.AgentProjectionSet ->
            (SessionId * OpenBloggerRequest) list

    val settleStaleOpenAtLoad: liveFlight: (SessionId -> BloggerRequestId -> bool) -> journal: AgentJournal -> Task
