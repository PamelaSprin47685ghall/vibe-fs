namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Host
open Wanxiangshu.Infrastructure.Resources
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

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

    /// ENFORCER-160: the parking lifetime for a continuation transform.
    /// Owned by the Enforcer domain (GOV-003: no proposal-constant dependency
    /// in the production graph; the proposal may reference this, never vice
    /// versa).
    let ParkedTransformLifetime = TimeSpan.FromMinutes 10.0

    /// C4: commit-path UTF-8 safety bounds.
    let MaxBlogTextBytes = 512 * 1024
    let MaxEvidenceBytes = 128 * 1024
    /// ENFORCER-042: defensive multi-call cap (protocol violation still merged).
    let MaxMergedToolCalls = 32

    /// Prefer non-empty preferred; else fallback. Never invent a blank list when
    /// either side has content. Both empty is an invariant break: blanking Host
    /// transcript yields provider 400 (messages cannot be empty).
    let private ensureNonEmpty (preferred: obj list) (fallback: obj list) : obj list =
        if not (List.isEmpty preferred) then
            preferred
        elif not (List.isEmpty fallback) then
            fallback
        else
            Diagnostic.fatal
                "enforcer-empty-projection"
                [ "result", "ensureNonEmpty: both preferred and fallback are empty" ]

            preferred

    let private projectMessages (messages: obj list) (fallback: obj list) : EnforcerContinuation.ContinuationOutcome =
        EnforcerContinuation.ContinuationOutcome.ProjectMessages(ensureNonEmpty messages fallback)

    let private stopPhysicalRun
        (messages: obj list)
        (fallback: obj list)
        (reason: string)
        : EnforcerContinuation.ContinuationOutcome =
        EnforcerContinuation.ContinuationOutcome.StopPhysicalRun(ensureNonEmpty messages fallback, reason)



    /// Commit one cycle: blobs first, then the single BlogObservationCommitted
    /// append (PERSIST-009 shape: durable effect → fact). The fold refuses a
    /// duplicate ProviderRun, so replay of an already-committed step is a no-op
    /// at the caller's idempotency check (ENFORCER-154).
    ///
    /// ENFORCER-045: coverage advance is ONLY the staged typed context. Re-deriving
    /// from XTrace head is forbidden — that path freezes PrefixCoverage at 0 and
    /// leaves CoveredPrefixDigest empty, so CTX-011 probes never arm.



    let private isEmptyTextCycleFailure (reason: string) : bool =
        reason.IndexOf("merged text is empty", StringComparison.Ordinal) >= 0

    /// Rebuild provider-semantic turns from durable XTrace (AABB refresh source).
    /// Current reanchor generation only: Host turn indices restart after HOST-006,
    /// so mixing generations under groupBy Turn glues voided labels to live ones.
    let private projectionFromXTrace
        (journal: AgentJournal)
        (xTrace: XTraceProjectionState)
        : ProviderProjection.ProviderSemanticProjection =
        let byTurn =
            XTraceProjection.currentGenerationParts xTrace.Parts
            |> List.groupBy (fun part -> part.Turn)
            |> List.sortBy fst

        let messages =
            byTurn
            |> List.choose (fun (_turn, parts) ->
                let ordered = parts |> List.sortBy (fun p -> p.PartIndex)

                let role =
                    ordered
                    |> List.tryHead
                    |> Option.map (fun p -> p.Role)
                    |> Option.defaultValue "user"

                let semanticParts =
                    ordered
                    |> List.choose (fun part ->
                        match journal.Writer.BlobWriter.Read part.TextRef with
                        | Error _ -> None
                        | Ok body ->
                            match part.Kind with
                            | "text" -> Some(ProviderProjection.SemanticText body)
                            | "reasoning" -> Some(ProviderProjection.SemanticReasoning body)
                            | "tool_call" ->
                                part.ToolName
                                |> Option.map (fun name -> ProviderProjection.SemanticToolCall(name, body))
                            | "tool_result" -> Some(ProviderProjection.SemanticToolResult body)
                            | "media_omitted" ->
                                let mediaType = if String.IsNullOrWhiteSpace body then None else Some body

                                Some(ProviderProjection.SemanticMedia(mediaType, ""))
                            | _ -> None)

                if List.isEmpty semanticParts then
                    None
                else
                    Some
                        { ProviderProjection.SemanticMessage.Role = role
                          ProviderProjection.SemanticMessage.Parts = semanticParts })

        { ProviderId = None
          ModelId = None
          Variant = None
          Tools = []
          System = []
          Messages = messages }

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
        match EnforcerFrameRecovery.lastCoveredSequence xTrace chunk.NextCursor with
        | None -> None
        | Some nextSeq when nextSeq <= blog.Coverage.IngestedThroughSequence -> None
        | Some nextSeq ->
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
                              string blog.Coverage.IngestedThroughSequence
                              string nextSeq ]
                    )
                )

            Some(
                BloggerRequestContext.Main
                    { RequestId = requestId
                      MainSessionId = mainSessionId
                      BloggerSessionId = bloggerSessionId
                      Toml = chunk.Toml
                      PreviousIngestedThroughSequence = blog.Coverage.IngestedThroughSequence
                      NextIngestedThroughSequence = nextSeq
                      PreviousCoverableTurnCutoffExclusive = blog.Coverage.CoverableTurnCutoffExclusive
                      NextCoverableTurnCutoffExclusive = chunk.NextCoverableTurnCutoffExclusive
                      NextCoveredPrefixDigest = nextDigest
                      FrameEpochId = blog.FrameEpochId
                      DeltaDigest = deltaDigest
                      ObservedPrefixEpochId = observedEpoch }
            )

    /// AABB: re-chunk from current IngestedThrough against latest XTrace.
    /// Returns None when sealed or no material.
    let tryRefreshMainContextFromJournal
        (scope: IParkedTransformHost)
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        : BloggerRequestContext option =
        let key = SessionId.value bloggerSessionId

        if BloggerRuntimeHost.blocksNew (Some journal) mainSessionId scope key then
            None
        else
            let session =
                AgentProjection.tryFind mainSessionId (AgentJournal.snapshot journal).AgentProjections
                |> Option.defaultValue AgentProjection.emptySession

            let blog = session.Blog |> Option.defaultValue BlogProjection.empty
            let xTrace = session.XTrace |> Option.defaultValue XTraceProjection.empty

            let epoch =
                session.PrefixEpoch
                |> Option.map (fun e -> e.EpochId)
                |> Option.defaultValue PrefixEpochId.initial

            let projection = projectionFromXTrace journal xTrace

            let ingestCursor =
                XTraceProjection.semanticCursorFor blog.Coverage.IngestedThroughSequence xTrace

            match
                BloggerDelta.nextChunk
                    BloggerDelta.DeltaLimitBytes
                    ingestCursor
                    blog.Coverage.CoverableTurnCutoffExclusive
                    projection.Messages
            with
            | None -> None
            | Some chunk -> mainContextFromChunk mainSessionId bloggerSessionId epoch blog xTrace projection chunk

    /// The Blogger continuation-transform handler.
    ///
    /// ENFORCER-044 steps 1-7: read the step, merge, commit atomically, then
    /// park or inject. Returns the (possibly modified) message list.
    ///
    /// The FIRST transform of a Blogger turn (the prompt_async origin,
    /// ENFORCER-051) has no assistant message yet — it must never park; the
    /// request has to go out. Only a continuation (assistant step present)
    /// parks.
    ///
    /// Thin dispatcher over EnforcerContinuation's three branches
    /// (emptyCallsBranch / commitBranch / firstRequestBranch): it only derives
    /// the closed branch context and forwards. Branch logic lives in
    /// EnforcerContinuation (module compiled before this one).
    ///
    /// rabbit §13.1 / S9.1: `confirmedFailure` is the injected FALLBACK-003 writer
    /// adapter (ConfirmedFailurePort). EnforcerHost must not call
    /// FallbackController.recordConfirmedFailure directly — journal + budget are
    /// closed at the wiring site (SpikePlugin / test harness).
    let handleContinuation
        (scope: IParkedTransformHost)
        (journal: AgentJournal option)
        (repairNudge: InteractionRepairNudge option)
        (confirmedFailure: ConfirmedFailurePort option)
        (recoveryProbe: AgentJournal -> SessionId -> obj list -> EnforcerContinuation.RecoveryStageProbe)
        (bloggerSessionId: SessionId)
        (rawMessages: obj list)
        : Task<EnforcerContinuation.ContinuationOutcome> =
        task {
            // Never blank the Host transcript. Project = continue provider view;
            // Stop = project non-empty messages then plugin AbortSession.
            let project (msgs: obj list) = projectMessages msgs rawMessages

            let stop (reason: string) =
                stopPhysicalRun rawMessages rawMessages reason

            // ENFORCER-010: the execute gate proves the Blogger association;
            // the commit side re-proves it. An unprovable owner is fail-closed:
            // no cycle is committed under a guessed session (a fallback to the
            // blogger's own id would write to the wrong stream and escape the
            // per-session exactly-once index).
            let mainSessionId =
                journal
                |> Option.bind (fun j ->
                    SessionAssociationProjection.tryMainSessionOf
                        bloggerSessionId
                        (AgentJournal.snapshot j).AgentProjections.Associations)

            let mkCtx (durable: AgentJournal) (owner: SessionId) : EnforcerContinuation.Context =
                { Scope = scope
                  Journal = journal
                  Durable = durable
                  Owner = owner
                  BloggerSessionId = bloggerSessionId
                  RawMessages = rawMessages
                  RepairNudge = repairNudge
                  ConfirmedFailure = confirmedFailure
                  RecoveryProbe = recoveryProbe
                  Project = project
                  Stop = stop
                  RefreshMainContext = tryRefreshMainContextFromJournal scope durable
                  IsEmptyTextCycleFailure = isEmptyTextCycleFailure
                  ParkedTransformLifetime = ParkedTransformLifetime }

            match journal, mainSessionId, EnforcerCycleDecode.extractCalls rawMessages with
            | Some durable, Some owner, Some(_messageId, calls, assistantCompleted) when List.isEmpty calls ->
                return! EnforcerContinuation.emptyCallsBranch (mkCtx durable owner) assistantCompleted
            | Some durable, Some owner, Some(messageId, calls, assistantCompleted) ->
                return! EnforcerContinuation.commitBranch (mkCtx durable owner) messageId calls assistantCompleted
            | _ -> return! EnforcerContinuation.firstRequestBranch scope journal bloggerSessionId rawMessages project
        }
