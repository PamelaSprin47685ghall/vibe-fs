namespace Wanxiangshu.Enforcer

open System
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Context.Trace
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Persistence.Journal

module EnforcerHost =

    val internal mainContextFromChunk:
        Wanxiangshu.Foundation.Identity.SessionId ->
        Wanxiangshu.Foundation.Identity.SessionId ->
        Wanxiangshu.Foundation.Identity.PrefixEpochId ->
        Wanxiangshu.Context.Companion.Blogger.BlogProjectionState ->
        Wanxiangshu.Context.Trace.XTraceProjectionState ->
        Wanxiangshu.Participant.Provider.Projection.ProviderProjection.ProviderSemanticProjection ->
        Wanxiangshu.Context.Companion.Blogger.BloggerDeltaChunk ->
            Wanxiangshu.Context.Companion.Blogger.BloggerRequestContext option
