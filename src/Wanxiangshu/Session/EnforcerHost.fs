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


    /// Local outcome of one continuation cycle body (no program-counter bools).
    [<RequireQualifiedAccess>]
    type CycleDisposition =
        | Working
        | Committed of afterSquashMain: BloggerRequestContext option
        | InjectRepair of BloggerRequestContext
        | CommitUnknown
        | AbandonThenCatchUp

    /// Continuation transform result. Empty message lists are forbidden: Host
    /// forwards them as provider `messages` and rejects with 400.
    /// StopPhysicalRun asks the plugin to AbortSession after projecting messages.
    [<RequireQualifiedAccess>]
    type ContinuationOutcome =
        | ProjectMessages of obj list
        | StopPhysicalRun of messages: obj list * reason: string

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

    let private projectMessages (messages: obj list) (fallback: obj list) : ContinuationOutcome =
        ContinuationOutcome.ProjectMessages(ensureNonEmpty messages fallback)

    let private stopPhysicalRun (messages: obj list) (fallback: obj list) (reason: string) : ContinuationOutcome =
        ContinuationOutcome.StopPhysicalRun(ensureNonEmpty messages fallback, reason)



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
    /// ENFORCER-153 / DSL-003: the recovery stage probe, injected by the caller
    /// (Application layer owns the derivation; Session cannot reference it by
    /// compile order). Derived from the durable repair claim + provider-visible
    /// transcript on every read — recovery is never stored on a runtime cell
    /// mirror, and this module must never grow one.
    type RecoveryStageProbe = BloggerRequestContext -> BloggerToolRecovery

    /// rabbit §13.1 / S9.1: `confirmedFailure` is the injected FALLBACK-003 writer
    /// adapter (ConfirmedFailurePort). EnforcerHost must not call
    /// FallbackController.recordConfirmedFailure directly — journal + budget are
    /// closed at the wiring site (SpikePlugin / test harness).
    let handleContinuation
        (scope: IParkedTransformHost)
        (journal: AgentJournal option)
        (repairNudge: InteractionRepairNudge option)
        (confirmedFailure: ConfirmedFailurePort option)
        (recoveryProbe: AgentJournal -> SessionId -> obj list -> RecoveryStageProbe)
        (bloggerSessionId: SessionId)
        (rawMessages: obj list)
        : Task<ContinuationOutcome> =
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

            match journal, mainSessionId, EnforcerCycleDecode.extractCalls rawMessages with
            | Some durable, Some owner, Some(_messageId, calls, assistantCompleted) when List.isEmpty calls ->
                // Host transform msgs do NOT include the newly created outbound assistant
                // (prompt.ts: updateMessage then trigger transform on prior msgs).
                // lastAssistant = historical tail. Empty completed-blog list means:
                // 1) pending/running blog — Host re-enters after tool completion
                // 2) abort cleanup: blog status=error+interrupted, assistant completed
                //    → NOT pure prose; fail closed if still InFlight
                // 3) outbound after prior success is non-empty EnforcerCycleDecode.extractCalls (other arm)
                // 4) pure prose terminal (no blog parts at all) — ENFORCER-060 once when live
                // 5) no live request + interrupted/prose terminal → stop, never invent repair
                let key = SessionId.value bloggerSessionId

                let currentCtx =
                    match scope.TryPeekCurrentRequest key with
                    | Some c -> Some c
                    | None -> EnforcerFrameRecovery.resolveCycleContext scope durable owner bloggerSessionId

                // Repair injection requires LIVE InFlight authority only.
                // Durable-re-derived currentCtx is for rebuild/fatal/abandon — never for aabbRepair.
                // Abort residue (stop → Host interrupted blog) has no live cycle to repair.
                let liveCtx = EnforcerFrameRecovery.tryLiveCycleContext scope bloggerSessionId

                let rebuild () =
                    match currentCtx with
                    | Some c ->
                        EnforcerFrameRecovery.tryRebuildFromContext durable bloggerSessionId c
                        |> Option.defaultValue rawMessages
                    | None -> rawMessages

                let fatalEnd (reason: string) =
                    Diagnostic.fatal "enforcer-cycle-failed" [ "session_id", key; "result", reason ]

                    BloggerAbandon.openRequest durable owner bloggerSessionId currentCtx reason

                    scope.ClearCurrentRequest key
                    project rawMessages

                /// A sealed main run has nowhere to deliver a repaired cycle; the runtime
                /// is force-sealed instead of repaired. Shared by both repair entries so
                /// the seal decision cannot drift between them.
                let mainSealedNow () =
                    AgentProjection.mainSealedForBlogger owner (AgentJournal.snapshot durable).AgentProjections
                    && not (scope.IsDrainOpen key)

                /// The repair projection alone: refresh the transcript from durable frames
                /// and inject the protocol-repair instruction. The injected synthetic
                /// message IS the consumed marker (ENFORCER-153 derivation), so this call
                /// is what bounds the repair budget — not the fallback cursor.
                ///
                /// No cursor movement here. Whether the observed terminal is a confirmed
                /// model failure (ENFORCER-065/068) or Host abort residue (LOOP-006) is the
                /// caller's evidence to read, and only the former may spend AABB.
                let projectRepairInstruction (ctx: BloggerRequestContext) (reason: string) =
                    if mainSealedNow () then
                        BloggerRuntimeHost.forceSealRuntime scope key
                        project rawMessages
                    else
                        let fresh =
                            tryRefreshMainContextFromJournal scope durable owner bloggerSessionId
                            |> Option.defaultValue ctx

                        scope.SetCurrentRequest(key, fresh)

                        Diagnostic.emit "enforcer-cycle-repair" [ "session_id", key; "result", reason ]
                        let requestKey = BloggerRequestId.value (BloggerRequestContext.requestId fresh)

                        let rebuilt =
                            EnforcerFrameRecovery.tryRebuildFromContext durable bloggerSessionId fresh
                            |> Option.defaultValue rawMessages

                        project (EnforcerRepair.withRepairInstruction rebuilt requestKey)

                /// ENFORCER-068 AABB: record the confirmed failure on the primary cursor,
                /// then inject the repair. Used for: nudge hard-fail, second pure prose,
                /// ENFORCER-061 empty text, ENFORCER-065 ToolExecutionError.
                ///
                /// NOT used for Host abort residue — an interrupted tool call is a cleanup
                /// abort, and LOOP-006 forbids cleanup aborts from spending AABB.
                let aabbRepair (ctx: BloggerRequestContext) (reason: string) =
                    if mainSealedNow () then
                        BloggerRuntimeHost.forceSealRuntime scope key
                        project rawMessages
                    else
                        // ENFORCER-062/067/068 bridge via ConfirmedFailurePort (rabbit §13.1):
                        // injected adapter advances the primary cursor through the ONE
                        // writer. RecoveryExhausted forbids the next automatic attempt —
                        // the repair projection is then NOT injected. ContinueRecovery
                        // covers Advanced / AlreadyRecorded / NoActiveRun (FALLBACK-001).
                        let providerRun =
                            match EnforcerCycleDecode.lastAssistantStep rawMessages with
                            | Some(messageId, _, _) when not (String.IsNullOrWhiteSpace messageId) ->
                                ProviderRunIdentity.create messageId
                            | _ -> ProviderRunIdentity.create "unknown-prose-run"

                        let admission =
                            match confirmedFailure with
                            | None ->
                                Diagnostic.emit
                                    "enforcer-aabb-bridge"
                                    [ "session_id", key; "result", "no confirmed failure port; " + reason ]

                                None
                            | Some record ->
                                match record owner providerRun reason with
                                | Ok result -> Some result
                                | Error err ->
                                    Diagnostic.emit
                                        "enforcer-aabb-bridge"
                                        [ "session_id", key; "result", "confirmedFailure port rejected: " + err ]

                                    None

                        match admission with
                        | Some RecoveryAdmission.RecoveryExhausted ->
                            Diagnostic.emit "enforcer-aabb-exhausted" [ "session_id", key; "result", reason ]
                            fatalEnd "blog aabb exhausted; auto-recovery budget spent"
                        | _ -> projectRepairInstruction ctx reason

                /// ENFORCER-066: durable InteractionRepair. No AABB transcript refresh.
                /// repairNudge is injected from HostSessionNudge (compile-order port).
                let interactionNudge
                    (ctx: BloggerRequestContext)
                    (terminalRun: ProviderRunIdentity)
                    (reason: string)
                    : Task<ContinuationOutcome> =
                    task {
                        match repairNudge with
                        | None ->
                            Diagnostic.emit
                                "enforcer-cycle-nudge-fail"
                                [ "session_id", key; "result", "no repair nudge port; " + reason ]

                            return aabbRepair ctx ("nudge-no-port: " + reason)
                        | Some send ->
                            let! sent =
                                send
                                    bloggerSessionId
                                    EnforcerRepair.RepairInstruction
                                    None
                                    journal
                                    terminalRun
                                    "blogger-missing-tool"

                            match sent with
                            | Ok _ ->
                                // The durable claim written by the send is the nudge
                                // marker (ENFORCER-153); nothing mirrors it in memory.
                                Diagnostic.emit "enforcer-cycle-nudge" [ "session_id", key; "result", reason ]

                                // Nudge is a durable prompt_async; transform projects current view only.
                                return project rawMessages
                            | Error err when err.IndexOf("already claimed", StringComparison.OrdinalIgnoreCase) >= 0 ->
                                // ENFORCER-067: claim exists / pending — not failure; no AABB.
                                // The existing durable claim already identifies this nudge.
                                Diagnostic.emit "enforcer-cycle-nudge-pending" [ "session_id", key; "result", err ]

                                return project rawMessages
                            | Error err ->
                                // ENFORCER-067 immediate failure → AABB.
                                Diagnostic.emit "enforcer-cycle-nudge-fail" [ "session_id", key; "result", err ]

                                return aabbRepair ctx ("nudge-failed: " + err)
                    }

                if EnforcerRepair.hasIncompleteBlogTool rawMessages then
                    return project rawMessages
                elif EnforcerRepair.hasAbortedBlogAttempt rawMessages then
                    // LOOP-006: an interrupted tool call is Host cleanup after abort, so it
                    // is not a confirmed model failure and must not consume the owner's
                    // A/A/B/B offset or budget — otherwise one owner provider failure is
                    // charged twice (once here, once by its own provider-failure path) and
                    // FALLBACK-002's provider-visible A/A/B/B order becomes a race.
                    // The repair is still injected; ENFORCER-153's transcript marker bounds
                    // it to one, and the second interrupt is terminal.
                    match liveCtx with
                    | Some ctx ->
                        match recoveryProbe durable bloggerSessionId rawMessages ctx with
                        | BloggerToolRecovery.AabbRepairConsumed ->
                            return fatalEnd "blog tool interrupted; repair already consumed"
                        | _ -> return projectRepairInstruction ctx "blog tool interrupted without completed call"
                    | None ->
                        // No live cycle: interrupted blog without authority is stop/abort residue,
                        // not a repair opportunity. Stop, never inject # Protocol repair.
                        return stop "unowned-interrupted-blog-without-CurrentRequest"
                elif EnforcerRepair.hasErroredBlogAttempt rawMessages then
                    // ENFORCER-065 ToolExecutionError: a genuine failed cycle → unified
                    // Fallback (ENFORCER-062), one AABB, then exhaust.
                    match liveCtx with
                    | Some ctx ->
                        match recoveryProbe durable bloggerSessionId rawMessages ctx with
                        | BloggerToolRecovery.AabbRepairConsumed -> return fatalEnd "blog tool error; aabb exhausted"
                        | _ -> return aabbRepair ctx "blog tool error without completed call"
                    | None -> return stop "unowned-errored-blog-without-CurrentRequest"
                elif EnforcerRepair.hasAnyBlogToolPart rawMessages then
                    return project (rebuild ())
                elif not assistantCompleted then
                    return project (rebuild ())
                else
                    // ENFORCER-060/064..068: completed assistant, zero blog parts → pure prose.
                    let terminalRun =
                        match EnforcerCycleDecode.lastAssistantStep rawMessages with
                        | Some(messageId, _, _) when not (String.IsNullOrWhiteSpace messageId) ->
                            ProviderRunIdentity.create messageId
                        | _ -> ProviderRunIdentity.create "unknown-prose-run"

                    match liveCtx with
                    | None -> return stop "unowned-completed-prose-without-CurrentRequest"
                    | Some ctx ->
                        match recoveryProbe durable bloggerSessionId rawMessages ctx with
                        | BloggerToolRecovery.NoRecovery ->
                            return! interactionNudge ctx terminalRun "no completed blog calls (ENFORCER-060)"
                        | BloggerToolRecovery.InteractionNudgeIssued issuedRun when issuedRun = terminalRun ->
                            // ENFORCER-067: same terminal re-entry / transform re-fire — not failure.
                            // Do not AABB until a *new* pure-prose terminal arrives.
                            Diagnostic.emit
                                "enforcer-cycle-nudge-pending"
                                [ "session_id", key; "result", "same terminal re-entry while nudge in flight" ]

                            return project rawMessages
                        | BloggerToolRecovery.InteractionNudgeIssued _ ->
                            // Semantic failure: nudge accepted, new terminal still pure prose → AABB.
                            return aabbRepair ctx "nudge semantic failure; pure prose again (ENFORCER-067)"
                        | BloggerToolRecovery.AabbRepairConsumed ->
                            return fatalEnd "protocol-repair-exhausted (ENFORCER-060)"
            | Some durable, Some owner, Some(messageId, calls, assistantCompleted) when not (List.isEmpty calls) ->
                // ENFORCER-044: merge/commit on completed blog tool parts when this plugin
                // owns the cycle (live CurrentRequest).
                //
                // Host prompt.ts: transform msgs do NOT include the newly created
                // outbound assistant — lastAssistant is always the previous one.
                // processor.cleanup sets time.completed AFTER tools finish and BEFORE
                // the next loop iteration reloads msgs and re-triggers transform.
                // So the only Host trajectory that shows blog tool status=completed
                // also has assistant.time.completed. Skipping commit on that flag
                // freezes RecordCoverage: every later delta restarts at the origin
                // 200 KiB window with no fatal (silent stall).
                //
                // ENFORCER-154 alreadyEntry/alreadyReceipt still refuse re-commit.
                // liveCtx=None means we do not own this step — never invent authority.
                let mainSessionId = owner
                let providerRun = ProviderRunIdentity.create messageId
                let key = SessionId.value bloggerSessionId
                // Peek only — never heal InFlight from open on this arm.
                let liveCtx = EnforcerFrameRecovery.tryLiveCycleContext scope bloggerSessionId

                let snapshot = AgentJournal.snapshot durable

                let alreadyEntry =
                    snapshot.AgentProjections.Sessions
                    |> Map.tryFind mainSessionId
                    |> Option.bind (fun session -> session.Enforcement)
                    |> Option.map (fun state -> EnforcementProjection.tryFindByProviderRun providerRun state)
                    |> Option.flatten
                    |> Option.isSome

                let alreadyReceipt =
                    snapshot.AgentProjections.Sessions
                    |> Map.tryFind mainSessionId
                    |> Option.bind (fun session -> session.BloggerCycles)
                    |> Option.bind (fun cycles -> BloggerCycleProjection.tryReceipt providerRun cycles)
                    |> Option.isSome

                let resumeWithContext ctx =
                    EnforcerFrameRecovery.tryRebuildFromContext durable bloggerSessionId ctx
                    |> Option.defaultValue rawMessages

                let mainBlocks () =
                    BloggerRuntimeHost.blocksNew (Some durable) mainSessionId scope key

                /// Catch-up drain: one ≤200 KiB window from durable coverage; None = caught up.
                /// Stale PendingOffer is discarded — context must recompute from coverage (COMPANION-008).
                /// Caught-up / sealed → StopPhysicalRun so Host does not loop on tool calls.
                let resumeCatchUp (fallback: obj list) (caughtUpReason: string) : ContinuationOutcome =
                    if mainBlocks () then
                        BloggerRuntimeHost.forceSealRuntime scope key
                        stopPhysicalRun rawMessages fallback "main-sealed-blocks-request"
                    else
                        scope.TryTakePendingOffer key |> ignore

                        match tryRefreshMainContextFromJournal scope durable mainSessionId bloggerSessionId with
                        | Some ctx ->
                            if scope.HasFlight key then
                                ()
                            else
                                scope.SetCurrentRequest(key, ctx)

                            project (resumeWithContext ctx)
                        | None ->
                            // Caught up. Durable seal ends reactivation permanently.
                            if
                                AgentProjection.mainSealedForBlogger
                                    mainSessionId
                                    (AgentJournal.snapshot durable).AgentProjections
                            then
                                BloggerRuntimeHost.forceSealCellDropOffer scope key
                            else
                                scope.ClearCurrentRequest key

                            stopPhysicalRun rawMessages fallback caughtUpReason

                if alreadyEntry || alreadyReceipt then
                    // ENFORCER-154: same provider run already committed — drain remaining gap.
                    return resumeCatchUp rawMessages "idempotent-receipt-catch-up-complete"
                elif liveCtx.IsNone then
                    // No owned cycle. Unowned completed blog is protocol stop (not silent
                    // project): returning rawMessages alone lets Host tool-loop forever.
                    // Live unowned (assistant not completed) remains Diagnostic.fatal.
                    if assistantCompleted then
                        return stop "unowned-completed-blog-without-CurrentRequest"
                    else
                        match EnforcerRepair.tryOpenByBlogger durable mainSessionId bloggerSessionId with
                        | Some _ ->
                            Diagnostic.fatal
                                "enforcer-cycle-failed"
                                [ "session_id", key; "result", "missing CurrentRequest" ]

                            return project rawMessages
                        | None ->
                            Diagnostic.fatal
                                "enforcer-cycle-failed"
                                [ "session_id", key; "result", "live blog without cycle authority" ]

                            return project rawMessages
                else
                    // PERSIST-010 precheck / concurrent coverage advance: abandon stale
                    // staged cycle then rebuild from live journal coverage. Must NOT
                    // resumeWithContext(liveCtx) — that freezes PreviousIngestedThrough
                    // at the pre-crash cursor and loops KnownNotCommitted forever.
                    // DSL-MUTABLE: algorithm-scratch — mutable cycle-disposition accumulator
                    let mutable disposition = CycleDisposition.Working

                    let fatalEnd (reason: string) =
                        Diagnostic.fatal "enforcer-cycle-failed" [ "session_id", key; "result", reason ]

                        BloggerAbandon.openRequest durable mainSessionId bloggerSessionId liveCtx reason

                        scope.ClearCurrentRequest key

                    let unexpectedEnd (reason: string) = fatalEnd reason

                    /// KnownNotCommitted is recoverable: abandon open + Idle, then
                    /// resumeCatchUp re-chunks from projection.IngestedThroughSequence.
                    /// Must NOT Diagnostic.fatal — that SIGKILLs before catch-up runs.
                    let abandonStaleCycle (reason: string) =
                        Diagnostic.emit "enforcer-cycle-stale" [ "session_id", key; "result", reason ]

                        BloggerAbandon.openRequest durable mainSessionId bloggerSessionId liveCtx reason

                        scope.ClearCurrentRequest key
                        disposition <- CycleDisposition.AbandonThenCatchUp

                    // ENFORCER-153: the AABB budget is derived from the transcript
                    // (the injected repair message for the live request IS the spent
                    // marker), never from a runtime mirror.
                    let aabbConsumed () =
                        match liveCtx with
                        | Some ctx ->
                            (recoveryProbe durable bloggerSessionId rawMessages ctx) = BloggerToolRecovery.AabbRepairConsumed
                        | None -> false

                    match EnforcerCycleDecode.validateCycle messageId calls with
                    | Error reason when
                        isEmptyTextCycleFailure reason
                        && not (aabbConsumed ())
                        && not (EnforcerRepair.hasIncompleteBlogTool rawMessages)
                        ->
                        // ENFORCER-061: empty text keeps one AABB repair budget (not pure-prose nudge).
                        let fresh =
                            tryRefreshMainContextFromJournal scope durable mainSessionId bloggerSessionId
                            |> Option.orElse liveCtx

                        match fresh with
                        | None -> unexpectedEnd (reason + "; aabb-refresh-empty")
                        | Some freshCtx ->
                            disposition <- CycleDisposition.InjectRepair freshCtx
                            scope.SetCurrentRequest(key, freshCtx)

                            Diagnostic.emit "enforcer-cycle-repair" [ "session_id", key; "result", reason ]
                    | Error reason ->
                        if isEmptyTextCycleFailure reason && aabbConsumed () then
                            unexpectedEnd "protocol-repair-exhausted"
                        else
                            unexpectedEnd reason
                    | Ok(merged, toolCallIds) ->
                        match liveCtx with
                        | Some(BloggerRequestContext.Squash squash) ->
                            match
                                EnforcerCycleCommit.commitSquash
                                    durable
                                    mainSessionId
                                    bloggerSessionId
                                    providerRun
                                    squash
                                    merged.MergedText
                            with
                            | EnforcerCycleCommit.CycleCommitOutcome.KnownCommitted ->
                                disposition <- CycleDisposition.Committed None
                                scope.ClearCurrentRequest key
                            | EnforcerCycleCommit.CycleCommitOutcome.KnownNotCommitted reason ->
                                abandonStaleCycle reason
                            | EnforcerCycleCommit.CycleCommitOutcome.CommitUnknown reason ->
                                disposition <- CycleDisposition.CommitUnknown

                                Diagnostic.fatal "enforcer-cycle-commit-unknown" [ "session_id", key; "result", reason ]
                        | Some(BloggerRequestContext.Main main) ->
                            let tomlDigest = BlobDigest.create (HostDigest.sha256Hex main.Toml)

                            // First physical send materializes open with PromptKey after
                            // StartFromContext. Catch-up drain reuses live CurrentRequest
                            // without a new open slot — only the open that matches this
                            // RequestId must carry a PromptKey.
                            let openUnbound =
                                EnforcerRepair.tryOpenByBlogger durable mainSessionId bloggerSessionId
                                |> Option.exists (fun openReq ->
                                    openReq.RequestId = main.RequestId && openReq.PromptKey.IsNone)

                            if tomlDigest <> main.DeltaDigest then
                                unexpectedEnd "delta digest mismatch"
                            elif main.NextIngestedThroughSequence <= main.PreviousIngestedThroughSequence then
                                unexpectedEnd "coverage did not advance"
                            elif openUnbound then
                                unexpectedEnd "open request has no PromptKey binding"
                            else
                                match
                                    EnforcerCycleCommit.commitCycle
                                        durable
                                        mainSessionId
                                        bloggerSessionId
                                        providerRun
                                        toolCallIds
                                        merged
                                        (Some main)
                                with
                                | EnforcerCycleCommit.CycleCommitOutcome.KnownCommitted ->
                                    disposition <- CycleDisposition.Committed None

                                    // Handle may have sealed during the cycle.
                                    if
                                        AgentProjection.mainSealedForBlogger
                                            mainSessionId
                                            (AgentJournal.snapshot durable).AgentProjections
                                        && not (scope.IsDrainOpen key)
                                    then
                                        BloggerRuntimeHost.forceSealCellDropOffer scope key

                                    scope.ClearCurrentRequest key
                                | EnforcerCycleCommit.CycleCommitOutcome.KnownNotCommitted reason ->
                                    abandonStaleCycle reason
                                | EnforcerCycleCommit.CycleCommitOutcome.CommitUnknown reason ->
                                    disposition <- CycleDisposition.CommitUnknown

                                    Diagnostic.fatal
                                        "enforcer-cycle-commit-unknown"
                                        [ "session_id", key; "result", reason ]
                        | None -> unexpectedEnd "missing CurrentRequest"

                    match disposition with
                    | CycleDisposition.InjectRepair ctx ->
                        // ENFORCER-062/067/068 bridge (empty text) via ConfirmedFailurePort
                        // (rabbit §13.1). RecoveryExhausted forbids automatic repair;
                        // ContinueRecovery covers Advanced / AlreadyRecorded / NoActiveRun.
                        let emptyReason = "blog empty text (ENFORCER-061)"

                        let admission =
                            match confirmedFailure with
                            | None ->
                                Diagnostic.emit
                                    "enforcer-aabb-bridge"
                                    [ "session_id", key; "result", "no confirmed failure port; " + emptyReason ]

                                None
                            | Some record ->
                                match record mainSessionId (ProviderRunIdentity.create messageId) emptyReason with
                                | Ok result -> Some result
                                | Error err ->
                                    Diagnostic.emit
                                        "enforcer-aabb-bridge"
                                        [ "session_id", key; "result", "confirmedFailure port rejected: " + err ]

                                    None

                        match admission with
                        | Some RecoveryAdmission.RecoveryExhausted ->
                            Diagnostic.emit "enforcer-aabb-exhausted" [ "session_id", key; "result", emptyReason ]

                            fatalEnd "blog aabb exhausted; auto-recovery budget spent"
                            return failwith "unreachable: fatalEnd ends the cycle"
                        | _ ->
                            let requestKey = BloggerRequestId.value (BloggerRequestContext.requestId ctx)
                            return project (EnforcerRepair.withRepairInstruction (resumeWithContext ctx) requestKey)
                    | CycleDisposition.CommitUnknown -> return project rawMessages
                    | CycleDisposition.AbandonThenCatchUp ->
                        // Stale staged coverage abandoned: rebuild next window from live
                        // IngestedThroughSequence. resumeCatchUp sets CurrentRequest +
                        // InFlight when material remains; None = true catch-up stop.
                        return resumeCatchUp rawMessages "stale-cycle-catch-up-complete"
                    | CycleDisposition.Working ->
                        match liveCtx with
                        | Some ctx -> return project (resumeWithContext ctx)
                        | None -> return project rawMessages
                    | CycleDisposition.Committed afterSquashMain ->
                        if mainBlocks () then
                            BloggerRuntimeHost.forceSealRuntime scope key
                            return stop "main-sealed-after-commit"
                        else
                            // Drain contract: after commit, immediately take next ≤200 KiB window
                            // from durable coverage until catch-up. PendingOffer is a wake signal
                            // only — never prefer stale frozen context over re-chunk.
                            scope.TryTakePendingOffer key |> ignore

                            match
                                tryRefreshMainContextFromJournal scope durable mainSessionId bloggerSessionId,
                                afterSquashMain
                            with
                            | Some ctx, _
                            | None, Some ctx ->
                                let ctx =
                                    tryRefreshMainContextFromJournal scope durable mainSessionId bloggerSessionId
                                    |> Option.defaultValue ctx

                                if scope.HasFlight key then
                                    ()
                                else
                                    scope.SetCurrentRequest(key, ctx)

                                return project (resumeWithContext ctx)
                            | None, None ->
                                // Caught up now. Durable seal closes DrainWindow permanently.
                                if
                                    AgentProjection.mainSealedForBlogger
                                        mainSessionId
                                        (AgentJournal.snapshot durable).AgentProjections
                                then
                                    BloggerRuntimeHost.forceSealCellDropOffer scope key
                                    scope.ClearCurrentRequest key
                                    return stop "main-sealed-caught-up"
                                else
                                    scope.ClearCurrentRequest key

                                    let! resumed = scope.ParkTransform(key, ParkedTransformLifetime)

                                    if not resumed then
                                        if mainBlocks () then
                                            BloggerRuntimeHost.forceSealRuntime scope key
                                            return stop "park-ended-main-sealed"
                                        else
                                            // Re-check gap: flight wake may have arrived after last refresh.
                                            match
                                                tryRefreshMainContextFromJournal
                                                    scope
                                                    durable
                                                    mainSessionId
                                                    bloggerSessionId
                                            with
                                            | Some ctx ->
                                                if scope.HasFlight key then
                                                    ()
                                                else
                                                    scope.SetCurrentRequest(key, ctx)

                                                return project (resumeWithContext ctx)
                                            | None ->
                                                // True catch-up after park lifetime: quiet stop (not fatal).
                                                // Never return [] — Host would blank messages → provider 400.
                                                return stop "park-ended-catch-up-complete"
                                    else
                                        scope.TryTakePendingOffer key |> ignore

                                        match
                                            tryRefreshMainContextFromJournal
                                                scope
                                                durable
                                                mainSessionId
                                                bloggerSessionId
                                        with
                                        | Some ctx ->
                                            if mainBlocks () then
                                                BloggerRuntimeHost.forceSealRuntime scope key
                                                return stop "park-resumed-main-sealed"
                                            else if scope.HasFlight key then
                                                return project (resumeWithContext ctx)
                                            else
                                                scope.SetCurrentRequest(key, ctx)
                                                return project (resumeWithContext ctx)
                                        | None -> return project rawMessages
            | _ ->
                // COMPANION-005 first request / non-tool step: rebuild only from
                // durable frames + typed CurrentRequest. Never extract TOML from
                // raw user messages (C2).
                match journal, scope.TryPeekCurrentRequest(SessionId.value bloggerSessionId) with
                | Some durable, Some ctx ->
                    return
                        project (
                            EnforcerFrameRecovery.tryRebuildFromContext durable bloggerSessionId ctx
                            |> Option.defaultValue rawMessages
                        )
                | _ -> return project rawMessages
        }
