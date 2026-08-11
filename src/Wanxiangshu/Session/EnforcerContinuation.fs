namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Host
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// The three continuation branches of EnforcerHost.handleContinuation (the
/// Blogger continuation-transform host), extracted so EnforcerHost stays a thin
/// dispatcher (ENFORCER-044).
///
/// Branch 1 (emptyCallsBranch): pending/running blog, abort cleanup, pure prose
/// terminal, repair injection (AABB / nudge) — nothing is committed here.
/// Branch 2 (commitBranch): ENFORCER-044 merge/commit on completed blog tool
/// parts, then drain / park / inject-repair disposition.
/// Branch 3 (firstRequestBranch): COMPANION-005 first request / non-tool step —
/// rebuild only from durable frames + typed CurrentRequest.
module EnforcerContinuation =

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

    /// ENFORCER-153 / DSL-003: the recovery stage probe, injected by the caller
    /// (Application layer owns the derivation; Session cannot reference it by
    /// compile order). Derived from the durable repair claim + provider-visible
    /// transcript on every read — recovery is never stored on a runtime cell
    /// mirror, and this module must never grow one.
    type RecoveryStageProbe = BloggerRequestContext -> BloggerToolRecovery

    /// Closed context shared by the branches: EnforcerHost (the thin dispatcher)
    /// injects every dependency the branch bodies touch, so the branches are pure
    /// transforms with no ambient module state.
    type Context =
        { Scope: IParkedTransformHost
          Journal: AgentJournal option
          Durable: AgentJournal
          Owner: SessionId
          BloggerSessionId: SessionId
          RawMessages: obj list
          RepairNudge: InteractionRepairNudge option
          ConfirmedFailure: ConfirmedFailurePort option
          RecoveryProbe: AgentJournal -> SessionId -> obj list -> RecoveryStageProbe
          Project: obj list -> ContinuationOutcome
          Stop: string -> ContinuationOutcome
          RefreshMainContext: SessionId -> SessionId -> BloggerRequestContext option
          IsEmptyTextCycleFailure: string -> bool
          ParkedTransformLifetime: System.TimeSpan }

    let key (ctx: Context) = SessionId.value ctx.BloggerSessionId

    /// Branch 1 — empty completed-blog list. Host transform msgs do NOT include
    /// the newly created outbound assistant (prompt.ts: updateMessage then
    /// trigger transform on prior msgs). lastAssistant = historical tail. An
    /// empty completed-blog list means:
    /// 1) pending/running blog — Host re-enters after tool completion
    /// 2) abort cleanup: blog status=error+interrupted, assistant completed
    ///    → NOT pure prose; fail closed if still InFlight
    /// 3) outbound after prior success is non-empty EnforcerCycleDecode.extractCalls (other arm)
    /// 4) pure prose terminal (no blog parts at all) — ENFORCER-060 once when live
    /// 5) no live request + interrupted/prose terminal → stop, never invent repair
    let emptyCallsBranch (ctx: Context) (assistantCompleted: bool) : Task<ContinuationOutcome> =
        task {
            let key = key ctx

            let currentCtx =
                match ctx.Scope.TryPeekCurrentRequest key with
                | Some c -> Some c
                | None -> EnforcerFrameRecovery.resolveCycleContext ctx.Scope ctx.Durable ctx.Owner ctx.BloggerSessionId

            // Repair injection requires LIVE InFlight authority only.
            // Durable-re-derived currentCtx is for rebuild/fatal/abandon — never for aabbRepair.
            // Abort residue (stop → Host interrupted blog) has no live cycle to repair.
            let liveCtx =
                EnforcerFrameRecovery.tryLiveCycleContext ctx.Scope ctx.BloggerSessionId

            let rebuild () =
                match currentCtx with
                | Some c ->
                    EnforcerFrameRecovery.tryRebuildFromContext ctx.Durable ctx.BloggerSessionId c
                    |> Option.defaultValue ctx.RawMessages
                | None -> ctx.RawMessages

            let fatalEnd (reason: string) =
                Diagnostic.fatal "enforcer-cycle-failed" [ "session_id", key; "result", reason ]

                BloggerAbandon.openRequest ctx.Durable ctx.Owner ctx.BloggerSessionId currentCtx reason

                ctx.Scope.ClearCurrentRequest key
                ctx.Project ctx.RawMessages

            /// A sealed main run has nowhere to deliver a repaired cycle; the runtime
            /// is force-sealed instead of repaired. Shared by both repair entries so
            /// the seal decision cannot drift between them.
            let mainSealedNow () =
                AgentProjection.mainSealedForBlogger ctx.Owner (AgentJournal.snapshot ctx.Durable).AgentProjections
                && not (ctx.Scope.IsDrainOpen key)

            /// The repair projection alone: refresh the transcript from durable frames
            /// and inject the protocol-repair instruction. The injected synthetic
            /// message IS the consumed marker (ENFORCER-153 derivation), so this call
            /// is what bounds the repair budget — not the fallback cursor.
            ///
            /// No cursor movement here. Whether the observed terminal is a confirmed
            /// model failure (ENFORCER-065/068) or Host abort residue (LOOP-006) is the
            /// caller's evidence to read, and only the former may spend AABB.
            let projectRepairInstruction (live: BloggerRequestContext) (reason: string) =
                if mainSealedNow () then
                    BloggerRuntimeHost.forceSealRuntime ctx.Scope key
                    ctx.Project ctx.RawMessages
                else
                    let fresh =
                        ctx.RefreshMainContext ctx.Owner ctx.BloggerSessionId
                        |> Option.defaultValue live

                    ctx.Scope.SetCurrentRequest(key, fresh)

                    Diagnostic.emit "enforcer-cycle-repair" [ "session_id", key; "result", reason ]
                    let requestKey = BloggerRequestId.value (BloggerRequestContext.requestId fresh)

                    let rebuilt =
                        EnforcerFrameRecovery.tryRebuildFromContext ctx.Durable ctx.BloggerSessionId fresh
                        |> Option.defaultValue ctx.RawMessages

                    ctx.Project(EnforcerRepair.withRepairInstruction rebuilt requestKey)

            /// ENFORCER-068 AABB: record the confirmed failure on the primary cursor,
            /// then inject the repair. Used for: nudge hard-fail, second pure prose,
            /// ENFORCER-061 empty text, ENFORCER-065 ToolExecutionError.
            ///
            /// NOT used for Host abort residue — an interrupted tool call is a cleanup
            /// abort, and LOOP-006 forbids cleanup aborts from spending AABB.
            let aabbRepair (live: BloggerRequestContext) (reason: string) =
                if mainSealedNow () then
                    BloggerRuntimeHost.forceSealRuntime ctx.Scope key
                    ctx.Project ctx.RawMessages
                else
                    // ENFORCER-062/067/068 bridge via ConfirmedFailurePort (rabbit §13.1):
                    // injected adapter advances the primary cursor through the ONE
                    // writer. RecoveryExhausted forbids the next automatic attempt —
                    // the repair projection is then NOT injected. ContinueRecovery
                    // covers Advanced / AlreadyRecorded / NoActiveRun (FALLBACK-001).
                    let providerRun =
                        match EnforcerCycleDecode.lastAssistantStep ctx.RawMessages with
                        | Some(messageId, _, _) when not (String.IsNullOrWhiteSpace messageId) ->
                            ProviderRunIdentity.create messageId
                        | _ -> ProviderRunIdentity.create "unknown-prose-run"

                    let admission =
                        match ctx.ConfirmedFailure with
                        | None ->
                            Diagnostic.emit
                                "enforcer-aabb-bridge"
                                [ "session_id", key; "result", "no confirmed failure port; " + reason ]

                            None
                        | Some record ->
                            match record ctx.Owner providerRun reason with
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
                    | _ -> projectRepairInstruction live reason

            /// ENFORCER-066: durable InteractionRepair. No AABB transcript refresh.
            /// repairNudge is injected from HostSessionNudge (compile-order port).
            let interactionNudge
                (live: BloggerRequestContext)
                (terminalRun: ProviderRunIdentity)
                (reason: string)
                : Task<ContinuationOutcome> =
                task {
                    match ctx.RepairNudge with
                    | None ->
                        Diagnostic.emit
                            "enforcer-cycle-nudge-fail"
                            [ "session_id", key; "result", "no repair nudge port; " + reason ]

                        return aabbRepair live ("nudge-no-port: " + reason)
                    | Some send ->
                        let! sent =
                            send
                                ctx.BloggerSessionId
                                EnforcerRepair.RepairInstruction
                                None
                                ctx.Journal
                                terminalRun
                                "blogger-missing-tool"

                        match sent with
                        | Ok _ ->
                            // The durable claim written by the send is the nudge
                            // marker (ENFORCER-153); nothing mirrors it in memory.
                            Diagnostic.emit "enforcer-cycle-nudge" [ "session_id", key; "result", reason ]

                            // Nudge is a durable prompt_async; transform projects current view only.
                            return ctx.Project ctx.RawMessages
                        | Error err when err.IndexOf("already claimed", StringComparison.OrdinalIgnoreCase) >= 0 ->
                            // ENFORCER-067: claim exists / pending — not failure; no AABB.
                            // The existing durable claim already identifies this nudge.
                            Diagnostic.emit "enforcer-cycle-nudge-pending" [ "session_id", key; "result", err ]

                            return ctx.Project ctx.RawMessages
                        | Error err ->
                            // ENFORCER-067 immediate failure → AABB.
                            Diagnostic.emit "enforcer-cycle-nudge-fail" [ "session_id", key; "result", err ]

                            return aabbRepair live ("nudge-failed: " + err)
                }

            if EnforcerRepair.hasIncompleteBlogTool ctx.RawMessages then
                return ctx.Project ctx.RawMessages
            elif EnforcerRepair.hasAbortedBlogAttempt ctx.RawMessages then
                // LOOP-006: an interrupted tool call is Host cleanup after abort, so it
                // is not a confirmed model failure and must not consume the owner's
                // A/A/B/B offset or budget — otherwise one owner provider failure is
                // charged twice (once here, once by its own provider-failure path) and
                // FALLBACK-002's provider-visible A/A/B/B order becomes a race.
                // The repair is still injected; ENFORCER-153's transcript marker bounds
                // it to one, and the second interrupt is terminal.
                match liveCtx with
                | Some live ->
                    match ctx.RecoveryProbe ctx.Durable ctx.BloggerSessionId ctx.RawMessages live with
                    | BloggerToolRecovery.AabbRepairConsumed ->
                        return fatalEnd "blog tool interrupted; repair already consumed"
                    | _ -> return projectRepairInstruction live "blog tool interrupted without completed call"
                | None ->
                    // No live cycle: interrupted blog without authority is stop/abort residue,
                    // not a repair opportunity. Stop, never inject # Protocol repair.
                    return ctx.Stop "unowned-interrupted-blog-without-CurrentRequest"
            elif EnforcerRepair.hasErroredBlogAttempt ctx.RawMessages then
                // ENFORCER-065 ToolExecutionError: a genuine failed cycle → unified
                // Fallback (ENFORCER-062), one AABB, then exhaust.
                match liveCtx with
                | Some live ->
                    match ctx.RecoveryProbe ctx.Durable ctx.BloggerSessionId ctx.RawMessages live with
                    | BloggerToolRecovery.AabbRepairConsumed -> return fatalEnd "blog tool error; aabb exhausted"
                    | _ -> return aabbRepair live "blog tool error without completed call"
                | None -> return ctx.Stop "unowned-errored-blog-without-CurrentRequest"
            elif EnforcerRepair.hasAnyBlogToolPart ctx.RawMessages then
                return ctx.Project(rebuild ())
            elif not assistantCompleted then
                return ctx.Project(rebuild ())
            else
                // ENFORCER-060/064..068: completed assistant, zero blog parts → pure prose.
                let terminalRun =
                    match EnforcerCycleDecode.lastAssistantStep ctx.RawMessages with
                    | Some(messageId, _, _) when not (String.IsNullOrWhiteSpace messageId) ->
                        ProviderRunIdentity.create messageId
                    | _ -> ProviderRunIdentity.create "unknown-prose-run"

                match liveCtx with
                | None -> return ctx.Stop "unowned-completed-prose-without-CurrentRequest"
                | Some live ->
                    match ctx.RecoveryProbe ctx.Durable ctx.BloggerSessionId ctx.RawMessages live with
                    | BloggerToolRecovery.NoRecovery ->
                        return! interactionNudge live terminalRun "no completed blog calls (ENFORCER-060)"
                    | BloggerToolRecovery.InteractionNudgeIssued issuedRun when issuedRun = terminalRun ->
                        // ENFORCER-067: same terminal re-entry / transform re-fire — not failure.
                        // Do not AABB until a *new* pure-prose terminal arrives.
                        Diagnostic.emit
                            "enforcer-cycle-nudge-pending"
                            [ "session_id", key; "result", "same terminal re-entry while nudge in flight" ]

                        return ctx.Project ctx.RawMessages
                    | BloggerToolRecovery.InteractionNudgeIssued _ ->
                        // Semantic failure: nudge accepted, new terminal still pure prose → AABB.
                        return aabbRepair live "nudge semantic failure; pure prose again (ENFORCER-067)"
                    | BloggerToolRecovery.AabbRepairConsumed ->
                        return fatalEnd "protocol-repair-exhausted (ENFORCER-060)"
        }

    /// Branch 2 — ENFORCER-044: merge/commit on completed blog tool parts when
    /// this plugin owns the cycle (live CurrentRequest).
    ///
    /// Host prompt.ts: transform msgs do NOT include the newly created
    /// outbound assistant — lastAssistant is always the previous one.
    /// processor.cleanup sets time.completed AFTER tools finish and BEFORE
    /// the next loop iteration reloads msgs and re-triggers transform.
    /// So the only Host trajectory that shows blog tool status=completed
    /// also has assistant.time.completed. Skipping commit on that flag
    /// freezes RecordCoverage: every later delta restarts at the origin
    /// 200 KiB window with no fatal (silent stall).
    ///
    /// ENFORCER-154 alreadyEntry/alreadyReceipt still refuse re-commit.
    /// liveCtx=None means we do not own this step — never invent authority.
    let commitBranch
        (ctx: Context)
        (messageId: string)
        (calls: (int * ToolCallId * EnforcerCodec.CanonicalBlogCall) list)
        (assistantCompleted: bool)
        : Task<ContinuationOutcome> =
        task {
            let mainSessionId = ctx.Owner
            let providerRun = ProviderRunIdentity.create messageId
            let key = key ctx
            // Peek only — never heal InFlight from open on this arm.
            let liveCtx =
                EnforcerFrameRecovery.tryLiveCycleContext ctx.Scope ctx.BloggerSessionId

            let snapshot = AgentJournal.snapshot ctx.Durable

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

            let resumeWithContext live =
                EnforcerFrameRecovery.tryRebuildFromContext ctx.Durable ctx.BloggerSessionId live
                |> Option.defaultValue ctx.RawMessages

            let mainBlocks () =
                BloggerRuntimeHost.blocksNew (Some ctx.Durable) mainSessionId ctx.Scope key

            /// Catch-up drain: one ≤200 KiB window from durable coverage; None = caught up.
            /// Stale PendingOffer is discarded — context must recompute from coverage (COMPANION-008).
            /// Caught-up / sealed → StopPhysicalRun so Host does not loop on tool calls.
            let resumeCatchUp (fallback: obj list) (caughtUpReason: string) : ContinuationOutcome =
                if mainBlocks () then
                    BloggerRuntimeHost.forceSealRuntime ctx.Scope key
                    ctx.Stop "main-sealed-blocks-request"
                else
                    ctx.Scope.TryTakePendingOffer key |> ignore

                    match ctx.RefreshMainContext mainSessionId ctx.BloggerSessionId with
                    | Some live ->
                        if ctx.Scope.HasFlight key then
                            ()
                        else
                            ctx.Scope.SetCurrentRequest(key, live)

                        ctx.Project(resumeWithContext live)
                    | None ->
                        // Caught up. Durable seal ends reactivation permanently.
                        if
                            AgentProjection.mainSealedForBlogger
                                mainSessionId
                                (AgentJournal.snapshot ctx.Durable).AgentProjections
                        then
                            BloggerRuntimeHost.forceSealCellDropOffer ctx.Scope key
                        else
                            ctx.Scope.ClearCurrentRequest key

                        ctx.Stop caughtUpReason

            if alreadyEntry || alreadyReceipt then
                // ENFORCER-154: same provider run already committed — drain remaining gap.
                return resumeCatchUp ctx.RawMessages "idempotent-receipt-catch-up-complete"
            elif liveCtx.IsNone then
                // No owned cycle. Unowned completed blog is protocol stop (not silent
                // project): returning rawMessages alone lets Host tool-loop forever.
                // Live unowned (assistant not completed) remains Diagnostic.fatal.
                if assistantCompleted then
                    return ctx.Stop "unowned-completed-blog-without-CurrentRequest"
                else
                    match EnforcerRepair.tryOpenByBlogger ctx.Durable mainSessionId ctx.BloggerSessionId with
                    | Some _ ->
                        Diagnostic.fatal
                            "enforcer-cycle-failed"
                            [ "session_id", key; "result", "missing CurrentRequest" ]

                        return ctx.Project ctx.RawMessages
                    | None ->
                        Diagnostic.fatal
                            "enforcer-cycle-failed"
                            [ "session_id", key; "result", "live blog without cycle authority" ]

                        return ctx.Project ctx.RawMessages
            else
                // PERSIST-010 precheck / concurrent coverage advance: abandon stale
                // staged cycle then rebuild from live journal coverage. Must NOT
                // resumeWithContext(liveCtx) — that freezes PreviousIngestedThrough
                // at the pre-crash cursor and loops KnownNotCommitted forever.
                // DSL-MUTABLE: algorithm-scratch — mutable cycle-disposition accumulator
                let mutable disposition = CycleDisposition.Working

                let fatalEnd (reason: string) =
                    Diagnostic.fatal "enforcer-cycle-failed" [ "session_id", key; "result", reason ]

                    BloggerAbandon.openRequest ctx.Durable mainSessionId ctx.BloggerSessionId liveCtx reason

                    ctx.Scope.ClearCurrentRequest key

                let unexpectedEnd (reason: string) = fatalEnd reason

                /// KnownNotCommitted is recoverable: abandon open + Idle, then
                /// resumeCatchUp re-chunks from projection.IngestedThroughSequence.
                /// Must NOT Diagnostic.fatal — that SIGKILLs before catch-up runs.
                let abandonStaleCycle (reason: string) =
                    Diagnostic.emit "enforcer-cycle-stale" [ "session_id", key; "result", reason ]

                    BloggerAbandon.openRequest ctx.Durable mainSessionId ctx.BloggerSessionId liveCtx reason

                    ctx.Scope.ClearCurrentRequest key
                    disposition <- CycleDisposition.AbandonThenCatchUp

                // ENFORCER-153: the AABB budget is derived from the transcript
                // (the injected repair message for the live request IS the spent
                // marker), never from a runtime mirror.
                let aabbConsumed () =
                    match liveCtx with
                    | Some live ->
                        (ctx.RecoveryProbe ctx.Durable ctx.BloggerSessionId ctx.RawMessages live) = BloggerToolRecovery.AabbRepairConsumed
                    | None -> false

                match EnforcerCycleDecode.validateCycle messageId calls with
                | Error reason when
                    ctx.IsEmptyTextCycleFailure reason
                    && not (aabbConsumed ())
                    && not (EnforcerRepair.hasIncompleteBlogTool ctx.RawMessages)
                    ->
                    // ENFORCER-061: empty text keeps one AABB repair budget (not pure-prose nudge).
                    let fresh =
                        ctx.RefreshMainContext mainSessionId ctx.BloggerSessionId
                        |> Option.orElse liveCtx

                    match fresh with
                    | None -> unexpectedEnd (reason + "; aabb-refresh-empty")
                    | Some freshCtx ->
                        disposition <- CycleDisposition.InjectRepair freshCtx
                        ctx.Scope.SetCurrentRequest(key, freshCtx)

                        Diagnostic.emit "enforcer-cycle-repair" [ "session_id", key; "result", reason ]
                | Error reason ->
                    if ctx.IsEmptyTextCycleFailure reason && aabbConsumed () then
                        unexpectedEnd "protocol-repair-exhausted"
                    else
                        unexpectedEnd reason
                | Ok(merged, toolCallIds) ->
                    match liveCtx with
                    | Some(BloggerRequestContext.Squash squash) ->
                        match
                            EnforcerCycleCommit.commitSquash
                                ctx.Durable
                                mainSessionId
                                ctx.BloggerSessionId
                                providerRun
                                squash
                                merged.MergedText
                        with
                        | EnforcerCycleCommit.CycleCommitOutcome.KnownCommitted ->
                            disposition <- CycleDisposition.Committed None
                            ctx.Scope.ClearCurrentRequest key
                        | EnforcerCycleCommit.CycleCommitOutcome.KnownNotCommitted reason -> abandonStaleCycle reason
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
                            EnforcerRepair.tryOpenByBlogger ctx.Durable mainSessionId ctx.BloggerSessionId
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
                                    ctx.Durable
                                    mainSessionId
                                    ctx.BloggerSessionId
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
                                        (AgentJournal.snapshot ctx.Durable).AgentProjections
                                    && not (ctx.Scope.IsDrainOpen key)
                                then
                                    BloggerRuntimeHost.forceSealCellDropOffer ctx.Scope key

                                ctx.Scope.ClearCurrentRequest key
                            | EnforcerCycleCommit.CycleCommitOutcome.KnownNotCommitted reason ->
                                abandonStaleCycle reason
                            | EnforcerCycleCommit.CycleCommitOutcome.CommitUnknown reason ->
                                disposition <- CycleDisposition.CommitUnknown

                                Diagnostic.fatal "enforcer-cycle-commit-unknown" [ "session_id", key; "result", reason ]
                    | None -> unexpectedEnd "missing CurrentRequest"

                match disposition with
                | CycleDisposition.InjectRepair live ->
                    // ENFORCER-062/067/068 bridge (empty text) via ConfirmedFailurePort
                    // (rabbit §13.1). RecoveryExhausted forbids automatic repair;
                    // ContinueRecovery covers Advanced / AlreadyRecorded / NoActiveRun.
                    let emptyReason = "blog empty text (ENFORCER-061)"

                    let admission =
                        match ctx.ConfirmedFailure with
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
                        let requestKey = BloggerRequestId.value (BloggerRequestContext.requestId live)
                        return ctx.Project(EnforcerRepair.withRepairInstruction (resumeWithContext live) requestKey)
                | CycleDisposition.CommitUnknown -> return ctx.Project ctx.RawMessages
                | CycleDisposition.AbandonThenCatchUp ->
                    // Stale staged coverage abandoned: rebuild next window from live
                    // IngestedThroughSequence. resumeCatchUp sets CurrentRequest +
                    // InFlight when material remains; None = true catch-up stop.
                    return resumeCatchUp ctx.RawMessages "stale-cycle-catch-up-complete"
                | CycleDisposition.Working ->
                    match liveCtx with
                    | Some live -> return ctx.Project(resumeWithContext live)
                    | None -> return ctx.Project ctx.RawMessages
                | CycleDisposition.Committed afterSquashMain ->
                    if mainBlocks () then
                        BloggerRuntimeHost.forceSealRuntime ctx.Scope key
                        return ctx.Stop "main-sealed-after-commit"
                    else
                        // Drain contract: after commit, immediately take next ≤200 KiB window
                        // from durable coverage until catch-up. PendingOffer is a wake signal
                        // only — never prefer stale frozen context over re-chunk.
                        ctx.Scope.TryTakePendingOffer key |> ignore

                        match ctx.RefreshMainContext mainSessionId ctx.BloggerSessionId, afterSquashMain with
                        | Some live, _
                        | None, Some live ->
                            let live =
                                ctx.RefreshMainContext mainSessionId ctx.BloggerSessionId
                                |> Option.defaultValue live

                            if ctx.Scope.HasFlight key then
                                ()
                            else
                                ctx.Scope.SetCurrentRequest(key, live)

                            return ctx.Project(resumeWithContext live)
                        | None, None ->
                            // Caught up now. Durable seal closes DrainWindow permanently.
                            if
                                AgentProjection.mainSealedForBlogger
                                    mainSessionId
                                    (AgentJournal.snapshot ctx.Durable).AgentProjections
                            then
                                BloggerRuntimeHost.forceSealCellDropOffer ctx.Scope key
                                ctx.Scope.ClearCurrentRequest key
                                return ctx.Stop "main-sealed-caught-up"
                            else
                                ctx.Scope.ClearCurrentRequest key

                                let! resumed = ctx.Scope.ParkTransform(key, ctx.ParkedTransformLifetime)

                                if not resumed then
                                    if mainBlocks () then
                                        BloggerRuntimeHost.forceSealRuntime ctx.Scope key
                                        return ctx.Stop "park-ended-main-sealed"
                                    else
                                        // Re-check gap: flight wake may have arrived after last refresh.
                                        match ctx.RefreshMainContext mainSessionId ctx.BloggerSessionId with
                                        | Some live ->
                                            if ctx.Scope.HasFlight key then
                                                ()
                                            else
                                                ctx.Scope.SetCurrentRequest(key, live)

                                            return ctx.Project(resumeWithContext live)
                                        | None ->
                                            // True catch-up after park lifetime: quiet stop (not fatal).
                                            // Never return [] — Host would blank messages → provider 400.
                                            return ctx.Stop "park-ended-catch-up-complete"
                                else
                                    ctx.Scope.TryTakePendingOffer key |> ignore

                                    match ctx.RefreshMainContext mainSessionId ctx.BloggerSessionId with
                                    | Some live ->
                                        if mainBlocks () then
                                            BloggerRuntimeHost.forceSealRuntime ctx.Scope key
                                            return ctx.Stop "park-resumed-main-sealed"
                                        else if ctx.Scope.HasFlight key then
                                            return ctx.Project(resumeWithContext live)
                                        else
                                            ctx.Scope.SetCurrentRequest(key, live)
                                            return ctx.Project(resumeWithContext live)
                                    | None -> return ctx.Project ctx.RawMessages
        }

    /// Branch 3 — COMPANION-005 first request / non-tool step: rebuild only from
    /// durable frames + typed CurrentRequest. Never extract TOML from raw user
    /// messages (C2).
    let firstRequestBranch
        (scope: IParkedTransformHost)
        (journal: AgentJournal option)
        (bloggerSessionId: SessionId)
        (rawMessages: obj list)
        (project: obj list -> ContinuationOutcome)
        : Task<ContinuationOutcome> =
        task {
            match journal, scope.TryPeekCurrentRequest(SessionId.value bloggerSessionId) with
            | Some durable, Some ctx ->
                return
                    project (
                        EnforcerFrameRecovery.tryRebuildFromContext durable bloggerSessionId ctx
                        |> Option.defaultValue rawMessages
                    )
            | _ -> return project rawMessages
        }
