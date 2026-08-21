namespace Wanxiangshu.Context.Companion.Blogger

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
module BloggerMainContext =

    let private requireCanonicalProjection result =
        match result with
        | Ok value -> value
        | Error error -> raise (InvalidOperationException error)

    let private openingFloor (journal: AgentJournal option) (mainSessionId: SessionId) =
        journal
        |> Option.bind (fun durable ->
            ManagerOpeningFloor.floorSequence mainSessionId (AgentJournal.snapshot durable).AgentProjections)

    let private nextChunk
        (journal: AgentJournal option)
        (mainSessionId: SessionId)
        (blog: BlogProjectionState)
        (xTrace: XTraceProjectionState)
        (projection: ProviderProjection.ProviderSemanticProjection)
        =
        let effectiveIngested =
            openingFloor journal mainSessionId
            |> Option.map (max blog.Coverage.IngestedThroughSequence)
            |> Option.defaultValue blog.Coverage.IngestedThroughSequence

        BloggerDelta.nextChunk
            BloggerDelta.DeltaLimitBytes
            (XTraceProjection.semanticCursorFor effectiveIngested xTrace)
            blog.Coverage.CoverableTurnCutoffExclusive
            projection.Messages

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
            EnforcerHost.mainContextFromChunk mainSessionId bloggerSessionId observedEpoch blog xTrace projection
        )

    let fromJournal
        (scope: IBloggerRuntimeHost)
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        : Task<BloggerRequestContext option> =
        task {
            let key = SessionId.value bloggerSessionId

            if BloggerRuntimeHost.blocksNew (Some journal) mainSessionId scope key then
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
