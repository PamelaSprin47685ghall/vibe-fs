namespace Wanxiangshu.Context.Companion.Blogger

open System
open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
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

    /// Map chunk NextCursor (first unconsumed semantic position) to the XTrace
    /// cursor of the last covered part. None means the mapping failed (empty
    /// trace or Host cursor not present on XTrace). Never default to zero:
    /// a silent zero with previous greater than zero would stage a
    /// non-advancing window that dies at commit.
    let private lastCoveredCursor (xTrace: XTraceProjectionState) (nextCursor: SemanticCursor) : XTraceCursor option =
        XTraceProjection.currentGenerationSemanticParts xTrace
        |> List.tryFindBack (fun part ->
            part.Turn < nextCursor.TurnIndex
            || (part.Turn = nextCursor.TurnIndex && part.PartIndex < nextCursor.PartIndex))
        |> Option.map (fun part -> part.Cursor)

    /// Digest of the provider-visible prefix at the coverable cutoff. When the
    /// cutoff does not move the previous digest is kept so a mid-turn chunk
    /// cannot rewrite a proof that still describes the same turns.
    let private coveredPrefixDigest
        (previousCutoff: int)
        (previousDigest: string)
        (nextCutoff: int)
        (projection: ProviderProjection.ProviderSemanticProjection)
        : string =
        if nextCutoff = previousCutoff then
            previousDigest
        else
            let coveredMessages =
                projection.Messages
                |> List.truncate (min nextCutoff (List.length projection.Messages))

            HostDigest.sha256Hex (
                ProviderProjection.renderSemantic
                    { projection with
                        Messages = coveredMessages }
            )

    /// Build the staged offer context from the same delta the coordinator
    /// computed. Freezes RequestId and ObservedPrefixEpochId at
    /// materialization. Refuses at birth when coverage cannot strictly
    /// advance: an unmapped next cursor or a next sequence at or below the
    /// previous ingested sequence returns None so no BloggerMain is started.
    let mainContextFromChunk
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        (observedEpoch: PrefixEpochId)
        (blog: BlogProjectionState)
        (xTrace: XTraceProjectionState)
        (projection: ProviderProjection.ProviderSemanticProjection)
        (chunk: BloggerDeltaChunk)
        : BloggerRequestContext option =
        let previousCoverage =
            blog.Coverage.IngestedThroughSequence
            |> XTraceCursor.create
            |> RecordCoverage.create

        match lastCoveredCursor xTrace chunk.NextCursor with
        | None -> None
        | Some nextCursor when not (XTraceCursor.isAfter nextCursor (RecordCoverage.ingestedThrough previousCoverage)) ->
            None
        | Some nextCursor ->
            let previousSeq =
                previousCoverage |> RecordCoverage.ingestedThrough |> XTraceCursor.sequence

            let nextSeq = XTraceCursor.sequence nextCursor

            let nextDigest =
                coveredPrefixDigest
                    blog.Coverage.CoverableTurnCutoffExclusive
                    blog.Coverage.CoveredPrefixDigest
                    chunk.NextCoverableTurnCutoffExclusive
                    projection

            let deltaDigest = BlobDigest.create (HostDigest.sha256Hex chunk.Toml)

            let requestId =
                BloggerRequestId.create (
                    HostDigest.sha256Hex (
                        String.concat
                            "|"
                            [ SessionId.value mainSessionId
                              SessionId.value bloggerSessionId
                              "main"
                              BlobDigest.value deltaDigest
                              string previousSeq
                              string nextSeq ]
                    )
                )

            Some(
                BloggerRequestContext.Main
                    { RequestId = requestId
                      MainSessionId = mainSessionId
                      BloggerSessionId = bloggerSessionId
                      Items = chunk.Items
                      Toml = chunk.Toml
                      PreviousIngestedThroughSequence = previousSeq
                      NextIngestedThroughSequence = nextSeq
                      PreviousCoverableTurnCutoffExclusive = blog.Coverage.CoverableTurnCutoffExclusive
                      NextCoverableTurnCutoffExclusive = chunk.NextCoverableTurnCutoffExclusive
                      NextCoveredPrefixDigest = nextDigest
                      FrameEpochId = blog.FrameEpochId
                      DeltaDigest = deltaDigest
                      ObservedPrefixEpochId = observedEpoch }
            )

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
        |> Option.bind (mainContextFromChunk mainSessionId bloggerSessionId observedEpoch blog xTrace projection)

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
