namespace Wanxiangshu.Context.Companion.Blogger

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Domain
open Wanxiangshu.Feedback.Enforcer
open Wanxiangshu.Feedback.Enforcer.Guidance
open Wanxiangshu.Journal.ProjectionUpdate
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity

module ContextFactFold =

    let private reject = FoldRejection.reject

    /// PERSIST-010: every Companion frame refusal describes a line a correct
    /// writer could not have produced, so none of them is absorbed.
    ///
    /// A stale frame epoch is the one that looks benign and is not. It means the
    /// line was written against a frame sequence that a squash has already
    /// replaced, so applying it would append an entry describing frames that no
    /// longer exist — and skipping it would lose an entry whose delta was already
    /// consumed. Neither is recoverable, so the fold refuses the journal.
    let private blogOutcome factName result =
        match result with
        | Ok updated -> Ok updated
        | Error(BlogFoldRejection.StaleFrameEpoch(expected, actual)) ->
            reject
                factName
                (sprintf
                    "frame epoch %d is in force but the line was written against %d (PERSIST-010)"
                    (FrameEpochId.value expected)
                    (FrameEpochId.value actual))
        | Error BlogFoldRejection.NonSequentialFrameEpoch ->
            reject factName "squash frame epoch is not the successor of the previous one (PERSIST-010)"
        | Error BlogFoldRejection.IngestCursorNotAdvanced ->
            reject factName "committed entry consumed nothing, so the same delta could be blogged forever (CTX-011)"
        | Error BlogFoldRejection.IngestCursorMismatch ->
            reject factName "entry's previous ingest cursor disagrees with the projection (PERSIST-010)"
        | Error BlogFoldRejection.CoverageRetreated ->
            reject factName "coverage moved backwards within one numbering (CTX-011)"
        | Error(BlogFoldRejection.CoveredFrameCountOutOfRange(claimed, available)) ->
            reject factName (sprintf "squash claimed %d of %d available frames (CTX-012)" claimed available)

    let fold (projection: AgentProjectionSet) (fact: ContextFactCases) : Result<AgentProjectionSet, FoldRejection> =
        // ── failure-driven context recovery (docs/what/context.md) ───────────────────────
        match fact with
        | ContextFactCases.BloggerRequestMaterialized payload ->
            let apply session =
                let cycles = Option.defaultValue BloggerCycleProjection.empty session.BloggerCycles

                BloggerCycleProjection.materialize
                    { RequestId = payload.RequestId
                      MainSessionId = payload.MainSessionId
                      BloggerSessionId = payload.BloggerSessionId
                      RequestKind = payload.RequestKind
                      ContextRef = payload.ContextRef
                      ContextDigest = payload.ContextDigest
                      ObservedPrefixEpochId = payload.ObservedPrefixEpochId
                      PreviousIngestedThroughSequence = payload.PreviousIngestedThroughSequence
                      NextIngestedThroughSequence = payload.NextIngestedThroughSequence
                      FrameEpochId = payload.FrameEpochId
                      SelectedFrameDigests = payload.SelectedFrameDigests
                      PromptKey = payload.PromptKey }
                    cycles
                |> Result.map (fun updated ->
                    { session with
                        BloggerCycles = Some updated })

            match AgentProjection.tryUpdate payload.MainSessionId apply projection with
            | Error reason -> reject "BloggerRequestMaterialized" reason
            | Ok updated -> Ok updated

        | ContextFactCases.BloggerRequestAbandoned payload ->
            let apply session =
                let cycles = Option.defaultValue BloggerCycleProjection.empty session.BloggerCycles

                Ok
                    { session with
                        BloggerCycles =
                            Some(BloggerCycleProjection.abandon payload.RequestId payload.BloggerSessionId cycles) }

            match AgentProjection.tryUpdate payload.MainSessionId apply projection with
            | Error reason -> reject "BloggerRequestAbandoned" reason
            | Ok updated -> Ok updated

        | ContextFactCases.BlogObservationCommitted payload ->
            // ENFORCER-045 + C5: Blog + Enforcement + unified cycle receipt.
            let applyEnforcementAndReceipt session =
                let enforcement =
                    Option.defaultValue EnforcementProjection.empty session.Enforcement

                let cycles = Option.defaultValue BloggerCycleProjection.empty session.BloggerCycles

                EnforcementProjection.applyFromEntry
                    enforcement
                    { MainSessionId = payload.SessionId
                      BloggerSessionId = payload.BloggerSessionId
                      ProviderRun = payload.ProviderRun
                      ToolCallIds = payload.ToolCallIds
                      CycleTextRef = payload.TextRef
                      CycleTextDigest = payload.TextDigest
                      TipRuleId = payload.TipRuleId
                      FieldNameAtCommit = payload.FieldNameAtCommit
                      CycleEvidenceRef = payload.EvidenceRef
                      ObservedPrefixEpochId = payload.ObservedPrefixEpochId }
                |> Result.bind (fun enfUpdated ->
                    BloggerCycleProjection.recordReceipt
                        { ProviderRun = payload.ProviderRun
                          Kind = BlogFrameKind.Entry
                          RequestId = payload.RequestId }
                        cycles
                    |> Result.map (fun cycleUpdated ->
                        { session with
                            Enforcement = Some enfUpdated
                            BloggerCycles = Some cycleUpdated }))

            match AgentProjection.tryUpdate payload.SessionId applyEnforcementAndReceipt projection with
            | Error reason -> reject "BlogObservationCommitted" reason
            | Ok updated ->
                tryUpdateBlog
                    payload.SessionId
                    (BlogProjection.applyEntry
                        payload.FrameEpochId
                        payload.PreviousIngestedThroughSequence
                        payload.NextIngestedThroughSequence
                        payload.PreviousCoverableTurnCutoffExclusive
                        payload.NextCoverableTurnCutoffExclusive
                        payload.NextCoveredPrefixDigest
                        { Kind = BlogFrameKind.Entry
                          Digest = payload.TextDigest
                          TextRef = payload.TextRef
                          CoveredFromSequence = payload.PreviousIngestedThroughSequence
                          CoveredThroughSequence = payload.NextIngestedThroughSequence })
                    updated
                |> blogOutcome "BlogObservationCommitted"

        | ContextFactCases.BlogObservationsSquashed payload ->
            // Blog frames squash + Enforcement tip co-truncate on the same main session
            // (payload.SessionId — same owner BlogObservationCommitted uses for Enforcement).
            let applyReceiptAndTips session =
                let cycles = Option.defaultValue BloggerCycleProjection.empty session.BloggerCycles

                let enforcement =
                    Option.defaultValue EnforcementProjection.empty session.Enforcement
                    |> EnforcementProjection.applySquash payload.CoveredFrameCount

                BloggerCycleProjection.recordReceipt
                    { ProviderRun = payload.ProviderRun
                      Kind = BlogFrameKind.Squash
                      RequestId = payload.RequestId }
                    cycles
                |> Result.map (fun updated ->
                    { session with
                        BloggerCycles = Some updated
                        Enforcement = Some enforcement })

            match AgentProjection.tryUpdate payload.SessionId applyReceiptAndTips projection with
            | Error reason -> reject "BlogObservationsSquashed" reason
            | Ok updated ->
                tryUpdateBlog
                    payload.SessionId
                    (BlogProjection.applySquash
                        payload.PreviousFrameEpochId
                        payload.NextFrameEpochId
                        payload.CoveredFrameCount
                        { Kind = BlogFrameKind.Squash
                          Digest = payload.TextDigest
                          TextRef = payload.TextRef
                          CoveredFromSequence = 0L
                          CoveredThroughSequence = 0L })
                    updated
                |> blogOutcome "BlogObservationsSquashed"

        | ContextFactCases.PrefixRebaseCommitted payload ->
            tryUpdatePrefix
                payload.SessionId
                (PrefixEpochProjection.applyRebase
                    payload.PreviousEpochId
                    payload.NextEpochId
                    { FrozenRecordPrefixRef = payload.FrozenRecordPrefixRef
                      FrozenRecordPrefixDigest = payload.FrozenRecordPrefixDigest
                      CutoffExclusive = payload.CutoffExclusive
                      CoveredPrefixDigest = payload.CoveredPrefixDigest
                      SealRoot = payload.SealRoot
                      SyntheticMessageId = payload.SyntheticMessageId })
                projection
            |> prefixOutcome "PrefixRebaseCommitted" projection

        | ContextFactCases.ContextReanchored payload ->
            // HOST-006: one physical event, two projections. The prefix retires and
            // PrefixCoverage returns to the origin; both must land or neither — a
            // retired prefix beside a cutoff claim in the voided numbering is the
            // state the single fact exists to prevent.
            //
            // Hence one session-level update rather than two chained ones: the
            // atomicity is structural, not something a reader has to verify by
            // tracing whether the second step was reached.
            //
            // Frames and RecordCoverage (IngestedThrough) survive. Host prefix
            // mapping is zeroed (BlogProjection.applyReanchor / COMPANION-008).
            // TipDelivery Full set also clears so post-compaction resolveTipGuidance
            // re-emits Full main.md instead of stranding Main on IdentityOnly.
            AgentProjection.tryUpdate
                payload.SessionId
                (fun session ->
                    session.PrefixEpoch
                    |> Option.defaultValue PrefixEpochProjection.empty
                    |> PrefixEpochProjection.applyReanchor
                        payload.PreviousEpochId
                        payload.NextEpochId
                        payload.ObservedCompactionRun
                    |> Result.map (fun retired ->
                        { session with
                            PrefixEpoch = Some retired
                            Blog = session.Blog |> Option.map BlogProjection.applyReanchor
                            TipDelivery = session.TipDelivery |> Option.map TipDeliveryProjection.applyReanchor }))
                projection
            |> prefixOutcome "ContextReanchored" projection
