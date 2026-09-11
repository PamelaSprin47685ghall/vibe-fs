namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Enforcer.InstitutionalLearning
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Attention
open Wanxiangshu.Interaction.Concern
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode.Host.PairProgramming
open Wanxiangshu.OpenCode.Host.RequirementGrounding
open Wanxiangshu.Enforcer.Guidance

/// Shared session-scoped projection-update algebra for the fold families
/// (formerly private helpers of `Fold`). `prefixOutcome` is shared by the
/// Context family and the MagicTodo envelope branch; the `apply*` appliers at
/// the end assemble the single-field fact families whose decision lives in the
/// owning domain fold.
module ProjectionUpdate =

    let private reject = FoldRejection.reject

    /// PERSIST-010 prefix-epoch refusals.
    ///
    /// `StalePrefixEpoch` is absorbed here, unlike its frame counterpart. Every
    /// epoch-advancing line carries the epoch it expected, so a replayed rebase or
    /// reanchor — the crash-recovery path in CTX-012 deliberately re-attempts both
    /// — arrives stale and means "already applied". That is what makes recovery
    /// idempotent without a second dedupe mechanism.
    ///
    /// `CandidateNotNew` is absorbed for the same reason: CTX-011 already refuses
    /// to build such a probe, so a line carrying one is a replay.
    let prefixOutcome factName projection result =
        match result with
        | Ok updated -> Ok updated
        | Error(PrefixFoldRejection.StalePrefixEpoch _)
        | Error PrefixFoldRejection.CandidateNotNew
        // HOST-006: the same compaction observed twice. Absorbed rather than fatal —
        // the observation repeats on every reconcile because the compaction message
        // stays in the transcript, so this is the expected steady state, not corruption.
        | Error(PrefixFoldRejection.CompactionAlreadyReanchored _) -> Ok projection
        | Error PrefixFoldRejection.NonSequentialPrefixEpoch ->
            reject factName "prefix epoch is not the successor of the previous one (PERSIST-010)"
        | Error(PrefixFoldRejection.CutoffRetreated(committed, proposed)) ->
            reject factName (sprintf "promoted cutoff %d is earlier than the committed %d (CTX-011)" proposed committed)

    // ── session-scoped helpers ──────────────────────────────────────────────

    let updateSession sessionId apply projection =
        AgentProjection.update sessionId apply projection

    let updateCompanion sessionId apply projection =
        updateSession
            sessionId
            (fun session ->
                { session with
                    Companion = Some(apply (Option.defaultValue CompanionProjection.empty session.Companion)) })
            projection

    /// docs/what/context.md frame facts. `tryUpdate` rather than `update`: every one of them can
    /// be refused, and PERSIST-010 requires the refusal to reach the caller.
    let tryUpdateBlog sessionId apply projection =
        AgentProjection.tryUpdate
            sessionId
            (fun session ->
                apply (Option.defaultValue BlogProjection.empty session.Blog)
                |> Result.map (fun updated -> { session with Blog = Some updated }))
            projection

    let tryUpdatePrefix sessionId apply projection =
        AgentProjection.tryUpdate
            sessionId
            (fun session ->
                apply (Option.defaultValue PrefixEpochProjection.empty session.PrefixEpoch)
                |> Result.map (fun updated ->
                    { session with
                        PrefixEpoch = Some updated }))
            projection

    let retireAuxiliaryInjectionVisibility (session: SessionAgentProjection) =
        { session with
            TipDelivery = session.TipDelivery |> Option.map TipDeliveryProjection.applyReanchor
            Guidelines = session.Guidelines |> Option.map GuidelineProjection.applyReanchor
            RequirementGrounding =
                session.RequirementGrounding
                |> Option.map RequirementGroundingProjection.applyReanchor }

    /// PROMPT-005: dispatch facts all key on the same session and projection.
    let updateAuthority sessionId apply projection =
        updateSession
            sessionId
            (fun session ->
                { session with
                    PromptAuthority =
                        Some(apply (Option.defaultValue PromptAuthorityLedger.empty session.PromptAuthority)) })
            projection

    // ── single-field fact families ──────────────────────────────────────────

    let applyFission (projection: AgentProjectionSet) (fact: FissionFactCases) =
        FissionProjection.fold projection.Fission fact
        |> Result.map (fun updated -> { projection with Fission = updated })
        |> Result.mapError (fun reason ->
            { Fact = "Fission"
              Reason = sprintf "%A" reason })

    let applyConcern (projection: AgentProjectionSet) (fact: ConcernFactCases) =
        ConcernProjection.applyFact fact projection.Concern
        |> Result.map (fun updated -> { projection with Concern = updated })
        |> Result.mapError (fun reason -> { Fact = "Concern"; Reason = reason })

    let applyAttention
        (projection: AgentProjectionSet)
        (fact: AttentionFactCases)
        : Result<AgentProjectionSet, FoldRejection> =
        match fact with
        | AttentionFactCases.DeferredWorkRecorded payload ->
            Ok
                { projection with
                    Attention =
                        projection.Attention
                        |> AttentionProjection.record payload.SessionId payload.OccurrenceId payload.Text }

    let applyAttentionLearning
        (projection: AgentProjectionSet)
        (fact: InstitutionalLearningFactCases)
        : Result<AgentProjectionSet, FoldRejection> =
        match fact with
        | InstitutionalLearningFactCases.LearningDispositionCommitted payload ->
            Ok
                { projection with
                    Attention =
                        projection.Attention
                        |> AttentionProjection.resurface
                            payload.SessionId
                            payload.OccurrenceId
                            payload.ResurfacedDeferredWorkIds }

    let applyInstitutionalLearning
        (projection: AgentProjectionSet)
        (fact: InstitutionalLearningFactCases)
        : Result<AgentProjectionSet, FoldRejection> =
        Ok
            { projection with
                InstitutionalLearning = InstitutionalLearningProjection.apply fact projection.InstitutionalLearning }
