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

/// docs/what/enforcer.md — Blogger as Enforcer: the Blogger continuation-transform host.
///
/// ENFORCER-044: when the Host has collected a provider step's tool results and
/// enters the continuation transform, this module re-reads the full assistant
/// snapshot, re-canonicalises every `blog` call, merges them by PartOrdinal, and
/// commits ONE BlogObservationCommitted atomically (ENFORCER-045/154) — the single
/// fact that appends the frame, advances coverage, and records the enforcement
/// half.
///
/// ENFORCER-047/050/051: after the commit the continuation transform parks
/// (no provider request leaves) until the main session offers fresh material;
/// the offer stages the new delta and resumes the parked transform, which
/// injects the delta as a synthetic user message (ENFORCER-051) and returns, so
/// the Host's step loop resumes with a rebuilt provider view from durable frames
/// + typed context (not raw transcript append). Cycles after the first therefore
/// never create a PromptDispatcher side effect.
module EnforcerHost =

    /// Public: build the staged offer context from the same delta the coordinator
    /// computed. Freezes RequestId + ObservedPrefixEpochId at materialization (C5).
    ///
    /// ENFORCER-045 / PERSIST-010: refuse at birth when coverage cannot strictly
    /// advance. A zero-advance window is a known, handleable mapping failure —
    /// return None so no BloggerMain is started. Unknown invariant breaks that
    /// still reach commit keep Diagnostic.fatal (君子不立危墙: 已知拒生, 未知仍杀).
    let internal mainContextFromChunk
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

        match EnforcerFrameRecovery.lastCoveredCursor xTrace chunk.NextCursor with
        | None -> None
        | Some nextCursor when not (XTraceCursor.isAfter nextCursor (RecordCoverage.ingestedThrough previousCoverage)) ->
            None
        | Some nextCursor ->
            let previousSeq =
                previousCoverage |> RecordCoverage.ingestedThrough |> XTraceCursor.sequence

            let nextSeq = XTraceCursor.sequence nextCursor

            let nextDigest =
                EnforcerFrameRecovery.coveredPrefixDigest
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
