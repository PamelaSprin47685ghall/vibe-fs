namespace Wanxiangshu.Enforcer.Cycle

open System
open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

/// How a legalised cycle becomes a durable fact: append-failure
/// classification, ordinary observation commit and squash commit.
module EnforcerCycleCommit =

    /// Item 14: three commit outcomes. Park only on KnownCommitted.
    [<RequireQualifiedAccess>]
    type CycleCommitOutcome =
        | KnownCommitted
        | KnownNotCommitted of reason: string
        | CommitUnknown of reason: string

    let private classifyAppendFailure (failure: JournalAppendFailure) : CycleCommitOutcome =
        match failure with
        | WriteUnknown(_, _) -> CycleCommitOutcome.CommitUnknown(JournalAppendFailure.describe failure)
        | FactRejected(_, _) -> CycleCommitOutcome.KnownNotCommitted(JournalAppendFailure.describe failure)

    /// Commit one cycle: blobs first, then the single BlogObservationCommitted
    /// append (PERSIST-009 shape: durable effect → fact). The fold refuses a
    /// duplicate ProviderRun, so replay of an already-committed step is a no-op
    /// at the caller's idempotency check (ENFORCER-154).
    ///
    /// ENFORCER-045: coverage advance is ONLY the staged typed context. Re-deriving
    /// from XTrace head is forbidden — that path freezes PrefixCoverage at 0 and
    /// leaves CoveredPrefixDigest empty, so CTX-011 probes never arm.
    let commitCycle
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (toolCallIds: ToolCallId list)
        (merged: EnforcerCycle.MergedCycle)
        (declared: BloggerMainRequestContext option)
        : Task<CycleCommitOutcome> =
        task {
            let projections = AgentJournal.snapshot journal

            let already =
                projections.AgentProjections.Sessions
                |> Map.tryFind mainSessionId
                |> Option.bind (fun session -> session.Enforcement)
                |> Option.map (fun state -> EnforcementProjection.tryFindByProviderRun providerRun state)
                |> Option.flatten

            // CommitUnknown reconcile: receipt already present → treat as KnownCommitted.
            match already with
            | Some _ -> return CycleCommitOutcome.KnownCommitted
            | None ->
                match declared with
                | None ->
                    return
                        CycleCommitOutcome.KnownNotCommitted "blog cycle has no staged coverage context (ENFORCER-045)"
                | Some coverage ->
                    // PERSIST-010 precheck (writer-side CAS): fold rejects IngestCursorMismatch
                    // only AFTER the line is durable, which poisons the journal. Staged
                    // PreviousIngestedThroughSequence is frozen at materialization; concurrent
                    // commit / crash-resume may advance coverage first. Refuse before append so
                    // failure is KnownNotCommitted (recoverable abandon), never FactRejected.
                    let liveBlog =
                        projections.AgentProjections.Sessions
                        |> Map.tryFind mainSessionId
                        |> Option.bind (fun session -> session.Blog)
                        |> Option.defaultValue BlogProjection.empty

                    let liveIngest = liveBlog.Coverage.IngestedThroughSequence
                    let liveCutoff = liveBlog.Coverage.CoverableTurnCutoffExclusive
                    let liveFrameEpoch = liveBlog.FrameEpochId

                    if coverage.PreviousIngestedThroughSequence <> liveIngest then
                        return
                            CycleCommitOutcome.KnownNotCommitted(
                                sprintf
                                    "staged previous ingest cursor %d disagrees with projection %d (PERSIST-010 precheck)"
                                    coverage.PreviousIngestedThroughSequence
                                    liveIngest
                            )
                    elif coverage.PreviousCoverableTurnCutoffExclusive <> liveCutoff then
                        return
                            CycleCommitOutcome.KnownNotCommitted(
                                sprintf
                                    "staged previous coverable cutoff %d disagrees with projection %d (PERSIST-010 precheck)"
                                    coverage.PreviousCoverableTurnCutoffExclusive
                                    liveCutoff
                            )
                    elif coverage.FrameEpochId <> liveFrameEpoch then
                        return
                            CycleCommitOutcome.KnownNotCommitted(
                                sprintf
                                    "staged frame epoch %d disagrees with projection %d (PERSIST-010 precheck)"
                                    (FrameEpochId.value coverage.FrameEpochId)
                                    (FrameEpochId.value liveFrameEpoch)
                            )
                    elif coverage.NextIngestedThroughSequence <= coverage.PreviousIngestedThroughSequence then
                        return CycleCommitOutcome.KnownNotCommitted "coverage did not advance"
                    else
                        // C5: use epoch frozen at request materialization, never live PrefixEpoch.
                        let epoch = coverage.ObservedPrefixEpochId

                        match! journal.WriteBlob merged.MergedText with
                        | Error error -> return CycleCommitOutcome.KnownNotCommitted error
                        | Ok textBlob ->
                            // ENFORCER-045 tip v2: TipRuleId + FieldNameAtCommit on the fact;
                            // no score-vector blob (ENFORCER-072).
                            let! evidenceResult =
                                match merged.MergedEvidence with
                                | "" -> Task.FromResult(Ok None)
                                | evidence ->
                                    task {
                                        match! journal.WriteBlob evidence with
                                        | Ok blob -> return Ok(Some blob)
                                        | Error error -> return Error error
                                    }

                            match evidenceResult with
                            | Error error -> return CycleCommitOutcome.KnownNotCommitted error
                            | Ok evidenceRef ->
                                // Re-read after blobs: only coverage-advancing facts race us;
                                // refuse still-stale staged cursor without writing the fact.
                                let latestBlog =
                                    AgentJournal.snapshot journal
                                    |> fun snap -> snap.AgentProjections.Sessions
                                    |> Map.tryFind mainSessionId
                                    |> Option.bind (fun session -> session.Blog)
                                    |> Option.defaultValue BlogProjection.empty

                                if
                                    coverage.PreviousIngestedThroughSequence
                                    <> latestBlog.Coverage.IngestedThroughSequence
                                    || coverage.PreviousCoverableTurnCutoffExclusive
                                       <> latestBlog.Coverage.CoverableTurnCutoffExclusive
                                    || coverage.FrameEpochId <> latestBlog.FrameEpochId
                                then
                                    return
                                        CycleCommitOutcome.KnownNotCommitted(
                                            sprintf
                                                "staged previous ingest cursor %d disagrees with projection %d after blob write (PERSIST-010 precheck)"
                                                coverage.PreviousIngestedThroughSequence
                                                latestBlog.Coverage.IngestedThroughSequence
                                        )
                                else
                                    let tip = merged.CanonicalTip

                                    let fact =
                                        ContextFact.BlogObservationCommitted
                                            {| SessionId = mainSessionId
                                               BloggerSessionId = bloggerSessionId
                                               RequestId = coverage.RequestId
                                               FrameEpochId = coverage.FrameEpochId
                                               PreviousIngestedThroughSequence =
                                                coverage.PreviousIngestedThroughSequence
                                               NextIngestedThroughSequence = coverage.NextIngestedThroughSequence
                                               PreviousCoverableTurnCutoffExclusive =
                                                coverage.PreviousCoverableTurnCutoffExclusive
                                               NextCoverableTurnCutoffExclusive =
                                                coverage.NextCoverableTurnCutoffExclusive
                                               NextCoveredPrefixDigest = coverage.NextCoveredPrefixDigest
                                               TextRef = textBlob.BlobRef
                                               TextDigest = textBlob.BlobDigest
                                               ProviderRun = providerRun
                                               ToolCallIds = toolCallIds
                                               TipRuleId = tip.RuleId
                                               FieldNameAtCommit = Some tip.FieldName
                                               EvidenceRef = evidenceRef |> Option.map (fun blob -> blob.BlobRef)
                                               ObservedPrefixEpochId = epoch |}

                                    match!
                                        AgentJournal.appendAgent
                                            (StreamId.Session mainSessionId)
                                            (Some providerRun)
                                            fact
                                            journal
                                    with
                                    | Error failure -> return classifyAppendFailure failure
                                    | Ok _ -> return CycleCommitOutcome.KnownCommitted
        }

    /// CTX-012: single production constructor path for BlogObservationsSquashed from tool loop.
    let commitSquash
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (squash: BloggerSquashRequestContext)
        (squashText: string)
        : Task<CycleCommitOutcome> =
        task {
            let projections = AgentJournal.snapshot journal

            // CommitUnknown reconcile via unified receipt.
            let alreadyReceipt =
                projections.AgentProjections.Sessions
                |> Map.tryFind mainSessionId
                |> Option.bind (fun s -> s.BloggerCycles)
                |> Option.bind (fun cycles -> BloggerCycleProjection.tryReceipt providerRun cycles)

            match alreadyReceipt with
            | Some _ -> return CycleCommitOutcome.KnownCommitted
            | None ->
                match projections.AgentProjections.Sessions |> Map.tryFind mainSessionId with
                | None ->
                    return
                        CycleCommitOutcome.KnownNotCommitted
                            "BlogObservationsSquashed requires an existing work session projection"
                | Some session ->
                    match session.Companion |> Option.bind (fun c -> c.BloggerSessionId) with
                    | Some linked when linked = bloggerSessionId ->
                        let blog = session.Blog |> Option.defaultValue BlogProjection.empty
                        let k = squash.CoveredFrameCount

                        if k < 1 || k > List.length blog.Frames then
                            return
                                CycleCommitOutcome.KnownNotCommitted(
                                    sprintf
                                        "BlogObservationsSquashed covers %d frames but %d exist"
                                        k
                                        (List.length blog.Frames)
                                )
                        elif blog.FrameEpochId <> squash.FrameEpochId then
                            return CycleCommitOutcome.KnownNotCommitted "BlogObservationsSquashed frame epoch mismatch"
                        else
                            let selected = List.truncate k (BlogProjection.frames blog)
                            let digests = selected |> List.map (fun f -> f.Digest)

                            if digests <> squash.FrameDigests then
                                return
                                    CycleCommitOutcome.KnownNotCommitted
                                        "BlogObservationsSquashed frame digests mismatch"
                            else
                                match! journal.WriteBlob squashText with
                                | Error error -> return CycleCommitOutcome.KnownNotCommitted error
                                | Ok blob ->
                                    let fact =
                                        ContextFact.BlogObservationsSquashed
                                            {| SessionId = mainSessionId
                                               BloggerSessionId = bloggerSessionId
                                               RequestId = squash.RequestId
                                               PreviousFrameEpochId = blog.FrameEpochId
                                               NextFrameEpochId = FrameEpochId.next blog.FrameEpochId
                                               CoveredFrameCount = k
                                               TextRef = blob.BlobRef
                                               TextDigest = blob.BlobDigest
                                               ProviderRun = providerRun |}

                                    match!
                                        AgentJournal.appendAgent
                                            (StreamId.Session mainSessionId)
                                            (Some providerRun)
                                            fact
                                            journal
                                    with
                                    | Error failure -> return classifyAppendFailure failure
                                    | Ok _ -> return CycleCommitOutcome.KnownCommitted
                    | Some _ ->
                        return
                            CycleCommitOutcome.KnownNotCommitted
                                "Squash completion belongs to a different Blogger session"
                    | None ->
                        return
                            CycleCommitOutcome.KnownNotCommitted
                                "BlogObservationsSquashed requires a durably linked Blogger session"
        }
