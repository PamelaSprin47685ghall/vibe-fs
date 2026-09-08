namespace Wanxiangshu.Context.Companion.Blogger

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Fable.Core.JsInterop
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
module BloggerMainContext =

    let private requireCanonicalProjection result =
        match result with
        | Ok value -> value
        | Error error -> raise (InvalidOperationException error)

    let private nextChunk
        (_journal: AgentJournal option)
        (_mainSessionId: SessionId)
        (blog: BlogProjectionState)
        (xTrace: XTraceProjectionState)
        (projection: ProviderProjection.ProviderSemanticProjection)
        =
        let ingested = blog.Coverage.IngestedThroughSequence |> XTraceCursor.create
        let effectiveIngested = RecordCoverage.create ingested

        BloggerDelta.nextChunk
            BloggerDelta.DeltaLimitBytes
            (XTraceProjection.semanticCursorAfterCoverage effectiveIngested xTrace)
            blog.Coverage.CoverableTurnCutoffExclusive
            projection.Messages

    let private enforcerHostModule: obj =
        emitJsExpr () """
        (() => {
            let mod = null;
            try {
                if (typeof require === 'function') {
                    mod = require('../../../Enforcer/Host.js');
                }
            } catch (_) {}
            if (!mod) {
                try {
                    const procMod = (typeof process !== 'undefined' && typeof process.getBuiltinModule === 'function')
                        ? process.getBuiltinModule('node:module')
                        : null;
                    if (procMod && typeof procMod.createRequire === 'function') {
                        const req = procMod.createRequire(import.meta.url);
                        mod = req('../../../Enforcer/Host.js');
                    }
                } catch (_) {}
            }
            return mod;
        })()
        """

    let private mainContextFromChunkDynamically
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        (observedEpoch: PrefixEpochId)
        (blog: BlogProjectionState)
        (xTrace: XTraceProjectionState)
        (projection: ProviderProjection.ProviderSemanticProjection)
        (chunk: BloggerDeltaChunk)
        : BloggerRequestContext option =
        let raw: obj =
            emitJsExpr (enforcerHostModule, mainSessionId, bloggerSessionId, observedEpoch, blog, xTrace, projection, chunk) """
            (() => {
                if ($0 && typeof $0.mainContextFromChunk === 'function') {
                    const res = $0.mainContextFromChunk($1, $2, $3, $4, $5, $6, $7);
                    return res !== undefined ? res : null;
                }
                return null;
            })()
            """
        if isNull raw then None else Some(unbox<BloggerRequestContext> raw)

    let hasMaterial
        (journal: AgentJournal option)
        (mainSessionId: SessionId)
        (blog: BlogProjectionState)
        (xTrace: XTraceProjectionState)
        (projection: ProviderProjection.ProviderSemanticProjection)
        =
        nextChunk journal mainSessionId blog xTrace projection |> Option.isSome

    let fromProjection
        (journal: AgentJournal option)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        (observedEpoch: PrefixEpochId)
        (blog: BlogProjectionState)
        (xTrace: XTraceProjectionState)
        (projection: ProviderProjection.ProviderSemanticProjection)
        : BloggerRequestContext option =
        nextChunk journal mainSessionId blog xTrace projection
        |> Option.bind (
            mainContextFromChunkDynamically mainSessionId bloggerSessionId observedEpoch blog xTrace projection
        )

    let fromJournal
        (scope: IBloggerRuntimeHost)
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        : Task<BloggerRequestContext option> =
        task {
            if BloggerRuntimeHost.blocksNew (Some journal) mainSessionId then
                return None
            else
                let session =
                    AgentProjection.tryFind mainSessionId (AgentJournal.snapshot journal).AgentProjections
                    |> Option.defaultValue AgentProjection.emptySession

                let blog = session.Blog |> Option.defaultValue BlogProjection.empty
                let xTrace = session.XTrace |> Option.defaultValue XTraceProjection.empty

                let epoch =
                    session.PrefixEpoch
                    |> Option.map (fun prefix -> prefix.EpochId)
                    |> Option.defaultValue PrefixEpochId.initial

                let! projectionResult = XTraceMaterialization.currentProjection journal xTrace
                let projection = requireCanonicalProjection projectionResult
                return fromProjection (Some journal) mainSessionId bloggerSessionId epoch blog xTrace projection
        }
