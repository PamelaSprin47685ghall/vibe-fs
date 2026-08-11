namespace Wanxiangshu.Journal

open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity

/// Shared session-scoped projection-update algebra for the fold families
/// (formerly private helpers of `Fold`). `prefixOutcome` is shared by the
/// Context family and the MagicTodo envelope branch.
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

    let updateReviewGuard sessionId apply projection =
        updateSession
            sessionId
            (fun session ->
                { session with
                    ReviewGuard = Some(apply (Option.defaultValue ReviewProjection.empty session.ReviewGuard)) })
            projection

    let bindTerminalFrontier
        (sessionId: SessionId)
        (terminalRef: BlobRef)
        (terminalDigest: BlobDigest)
        (projection: AgentProjectionSet)
        =
        match AgentProjection.tryFind sessionId projection with
        | Some { ReviewGuard = Some _
                 XTrace = Some xTrace } ->
            updateReviewGuard
                sessionId
                (ReviewProjection.recordTerminalFrontier
                    terminalRef
                    terminalDigest
                    (XTraceProjection.headSequence xTrace + 1L))
                projection
        | _ -> projection

    let updateRequirements sessionId apply projection =
        updateSession
            sessionId
            (fun session ->
                { session with
                    ReviewRequirements =
                        Some(apply (Option.defaultValue ReviewRequirementProjection.empty session.ReviewRequirements)) })
            projection

    let updateOrchestrator apply (projection: AgentProjectionSet) =
        { projection with
            Orchestrator = apply projection.Orchestrator }

    /// PROMPT-005: dispatch facts all key on the same session and projection.
    let updateAuthority sessionId apply projection =
        updateSession
            sessionId
            (fun session ->
                { session with
                    PromptAuthority =
                        Some(apply (Option.defaultValue PromptAuthorityLedger.empty session.PromptAuthority)) })
            projection
