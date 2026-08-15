namespace Wanxiangshu.Enforcer.Cycle

open Wanxiangshu.Composition.Durable

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

    let private collapseOutcome (result: Result<CycleCommitOutcome, CycleCommitOutcome>) : CycleCommitOutcome =
        match result with
        | Ok outcome -> outcome
        | Error outcome -> outcome

    let private liveBlogOf (projections: ProjectionSet) (mainSessionId: SessionId) : BlogProjectionState =
        projections.AgentProjections.Sessions
        |> Map.tryFind mainSessionId
        |> Option.bind (fun session -> session.Blog)
        |> Option.defaultValue BlogProjection.empty

    let private precheckStagedCoverage
        (coverage: BloggerMainRequestContext)
        (liveBlog: BlogProjectionState)
        : CycleCommitOutcome option =
        let liveIngest = liveBlog.Coverage.IngestedThroughSequence
        let liveCutoff = liveBlog.Coverage.CoverableTurnCutoffExclusive
        let liveFrameEpoch = liveBlog.FrameEpochId

        if coverage.PreviousIngestedThroughSequence <> liveIngest then
            Some(
                CycleCommitOutcome.KnownNotCommitted(
                    sprintf
                        "staged previous ingest cursor %d disagrees with projection %d (PERSIST-010 precheck)"
                        coverage.PreviousIngestedThroughSequence
                        liveIngest
                )
            )
        elif coverage.PreviousCoverableTurnCutoffExclusive <> liveCutoff then
            Some(
                CycleCommitOutcome.KnownNotCommitted(
                    sprintf
                        "staged previous coverable cutoff %d disagrees with projection %d (PERSIST-010 precheck)"
                        coverage.PreviousCoverableTurnCutoffExclusive
                        liveCutoff
                )
            )
        elif coverage.FrameEpochId <> liveFrameEpoch then
            Some(
                CycleCommitOutcome.KnownNotCommitted(
                    sprintf
                        "staged frame epoch %d disagrees with projection %d (PERSIST-010 precheck)"
                        (FrameEpochId.value coverage.FrameEpochId)
                        (FrameEpochId.value liveFrameEpoch)
                )
            )
        elif coverage.NextIngestedThroughSequence <= coverage.PreviousIngestedThroughSequence then
            Some(CycleCommitOutcome.KnownNotCommitted "coverage did not advance")
        else
            None

    let private refuseStaleAfterBlob
        (coverage: BloggerMainRequestContext)
        (latestBlog: BlogProjectionState)
        : CycleCommitOutcome option =
        if
            coverage.PreviousIngestedThroughSequence
            <> latestBlog.Coverage.IngestedThroughSequence
            || coverage.PreviousCoverableTurnCutoffExclusive
               <> latestBlog.Coverage.CoverableTurnCutoffExclusive
            || coverage.FrameEpochId <> latestBlog.FrameEpochId
        then
            Some(
                CycleCommitOutcome.KnownNotCommitted(
                    sprintf
                        "staged previous ingest cursor %d disagrees with projection %d after blob write (PERSIST-010 precheck)"
                        coverage.PreviousIngestedThroughSequence
                        latestBlog.Coverage.IngestedThroughSequence
                )
            )
        else
            None

    let private requireClear (outcome: CycleCommitOutcome option) : Result<unit, CycleCommitOutcome> =
        match outcome with
        | Some refused -> Error refused
        | None -> Ok()

    let private writeBlobOrNotCommitted
        (journal: AgentJournal)
        (text: string)
        : Task<Result<BlobWriteReceipt, CycleCommitOutcome>> =
        task {
            match! journal.WriteBlob text with
            | Ok blob -> return Ok blob
            | Error error -> return Error(CycleCommitOutcome.KnownNotCommitted error)
        }

    let private writeOptionalEvidence
        (journal: AgentJournal)
        (evidence: string)
        : Task<Result<BlobWriteReceipt option, CycleCommitOutcome>> =
        if evidence = "" then
            Task.FromResult(Ok None)
        else
            taskResult {
                let! blob = writeBlobOrNotCommitted journal evidence
                return Some blob
            }

    let private appendOrClassify
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (fact: AgentFact)
        : Task<Result<unit, CycleCommitOutcome>> =
        task {
            match! AgentJournal.appendAgent (StreamId.Session mainSessionId) (Some providerRun) fact journal with
            | Ok _ -> return Ok()
            | Error failure -> return Error(classifyAppendFailure failure)
        }

    let private commitWithCoverage
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (toolCallIds: ToolCallId list)
        (merged: EnforcerCycle.MergedCycle)
        (coverage: BloggerMainRequestContext)
        : Task<CycleCommitOutcome> =
        task {
            let projections = AgentJournal.snapshot journal
            let liveBlog = liveBlogOf projections mainSessionId

            match precheckStagedCoverage coverage liveBlog with
            | Some outcome -> return outcome
            | None ->
                // C5: use epoch frozen at request materialization, never live PrefixEpoch.
                let epoch = coverage.ObservedPrefixEpochId

                let! result =
                    taskResult {
                        let! textBlob = writeBlobOrNotCommitted journal merged.MergedText
                        let! evidenceRef = writeOptionalEvidence journal merged.MergedEvidence
                        let latestBlog = liveBlogOf (AgentJournal.snapshot journal) mainSessionId
                        do! requireClear (refuseStaleAfterBlob coverage latestBlog)
                        let tip = merged.CanonicalTip

                        let fact =
                            ContextFact.BlogObservationCommitted
                                {| SessionId = mainSessionId
                                   BloggerSessionId = bloggerSessionId
                                   RequestId = coverage.RequestId
                                   FrameEpochId = coverage.FrameEpochId
                                   PreviousIngestedThroughSequence = coverage.PreviousIngestedThroughSequence
                                   NextIngestedThroughSequence = coverage.NextIngestedThroughSequence
                                   PreviousCoverableTurnCutoffExclusive = coverage.PreviousCoverableTurnCutoffExclusive
                                   NextCoverableTurnCutoffExclusive = coverage.NextCoverableTurnCutoffExclusive
                                   NextCoveredPrefixDigest = coverage.NextCoveredPrefixDigest
                                   TextRef = textBlob.BlobRef
                                   TextDigest = textBlob.BlobDigest
                                   ProviderRun = providerRun
                                   ToolCallIds = toolCallIds
                                   TipRuleId = tip.RuleId
                                   FieldNameAtCommit = Some tip.FieldName
                                   EvidenceRef = evidenceRef |> Option.map (fun blob -> blob.BlobRef)
                                   ObservedPrefixEpochId = epoch |}

                        do! appendOrClassify journal mainSessionId providerRun fact
                        return CycleCommitOutcome.KnownCommitted
                    }

                return collapseOutcome result
        }

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
            match already, declared with
            | Some _, _ -> return CycleCommitOutcome.KnownCommitted
            | None, None ->
                return CycleCommitOutcome.KnownNotCommitted "blog cycle has no staged coverage context (ENFORCER-045)"
            | None, Some coverage ->
                return!
                    commitWithCoverage journal mainSessionId bloggerSessionId providerRun toolCallIds merged coverage
        }

    [<RequireQualifiedAccess>]
    type private SquashAdmission =
        | AlreadyCommitted
        | Rejected of reason: string
        | Ready of blog: BlogProjectionState * coveredFrameCount: int

    let private validateSquashFrames
        (blog: BlogProjectionState)
        (squash: BloggerSquashRequestContext)
        : SquashAdmission =
        let k = squash.CoveredFrameCount
        let selected = List.truncate k (BlogProjection.frames blog)
        let digests = selected |> List.map (fun f -> f.Digest)

        if k < 1 || k > List.length blog.Frames then
            SquashAdmission.Rejected(
                sprintf "BlogObservationsSquashed covers %d frames but %d exist" k (List.length blog.Frames)
            )
        elif blog.FrameEpochId <> squash.FrameEpochId then
            SquashAdmission.Rejected "BlogObservationsSquashed frame epoch mismatch"
        elif digests <> squash.FrameDigests then
            SquashAdmission.Rejected "BlogObservationsSquashed frame digests mismatch"
        else
            SquashAdmission.Ready(blog, k)

    let private decideSquashLink
        (session: SessionAgentProjection)
        (bloggerSessionId: SessionId)
        (squash: BloggerSquashRequestContext)
        : SquashAdmission =
        match session.Companion |> Option.bind (fun c -> c.BloggerSessionId) with
        | Some linked when linked = bloggerSessionId ->
            validateSquashFrames (session.Blog |> Option.defaultValue BlogProjection.empty) squash
        | Some _ -> SquashAdmission.Rejected "Squash completion belongs to a different Blogger session"
        | None -> SquashAdmission.Rejected "BlogObservationsSquashed requires a durably linked Blogger session"

    let private decideSquashSession
        (projections: ProjectionSet)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        (squash: BloggerSquashRequestContext)
        : SquashAdmission =
        match projections.AgentProjections.Sessions |> Map.tryFind mainSessionId with
        | None -> SquashAdmission.Rejected "BlogObservationsSquashed requires an existing work session projection"
        | Some session -> decideSquashLink session bloggerSessionId squash

    let private admitSquash
        (projections: ProjectionSet)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (squash: BloggerSquashRequestContext)
        : SquashAdmission =
        let alreadyReceipt =
            projections.AgentProjections.Sessions
            |> Map.tryFind mainSessionId
            |> Option.bind (fun s -> s.BloggerCycles)
            |> Option.bind (fun cycles -> BloggerCycleProjection.tryReceipt providerRun cycles)

        match alreadyReceipt with
        | Some _ -> SquashAdmission.AlreadyCommitted
        | None -> decideSquashSession projections mainSessionId bloggerSessionId squash

    let private commitReadySquash
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (squash: BloggerSquashRequestContext)
        (squashText: string)
        (blog: BlogProjectionState)
        (k: int)
        : Task<CycleCommitOutcome> =
        task {
            let! result =
                taskResult {
                    let! blob = writeBlobOrNotCommitted journal squashText

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

                    do! appendOrClassify journal mainSessionId providerRun fact
                    return CycleCommitOutcome.KnownCommitted
                }

            return collapseOutcome result
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
            match admitSquash projections mainSessionId bloggerSessionId providerRun squash with
            | SquashAdmission.AlreadyCommitted -> return CycleCommitOutcome.KnownCommitted
            | SquashAdmission.Rejected reason -> return CycleCommitOutcome.KnownNotCommitted reason
            | SquashAdmission.Ready(blog, k) ->
                return! commitReadySquash journal mainSessionId bloggerSessionId providerRun squash squashText blog k
        }
