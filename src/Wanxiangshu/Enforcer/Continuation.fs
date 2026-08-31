namespace Wanxiangshu.Enforcer

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open FsToolkit.ErrorHandling
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Persistence.Journal

/// Session termination capability — same signature as PluginTransforms.fs private type.
type SessionTermination = SessionId -> string -> Task<Result<unit, string>>

/// The three continuation branches of EnforcerHost.handleContinuation (the
/// Blogger continuation-transform host), extracted so EnforcerHost stays a thin
/// dispatcher (ENFORCER-044).
///
/// Branch 1 (emptyCallsBranch): pending/running blog, abort cleanup, pure prose
/// terminal, and AABB repair — nothing is committed here. The first physical
/// protocol nudge is idle-owned; transform must never queue one behind a live
/// Host tool loop.
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
    /// StopPhysicalRun asks the plugin to interrupt only the current physical attempt after projecting messages.
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
        { Scope: IBloggerRuntimeHost
          Journal: AgentJournal option
          Durable: AgentJournal
          Owner: SessionId
          BloggerSessionId: SessionId
          RawMessages: obj list
          RecoveryProbe: AgentJournal -> SessionId -> obj list -> RecoveryStageProbe
          Project: obj list -> ContinuationOutcome
          Stop: string -> ContinuationOutcome
          RefreshMainContext: SessionId -> SessionId -> Task<BloggerRequestContext option>
          IsEmptyTextCycleFailure: string -> bool }

    let key (ctx: Context) = SessionId.value ctx.BloggerSessionId

    /// Evidence → Decision: last assistant messageId → provider run identity.
    let private providerRunFromLastAssistant (rawMessages: obj list) (fallbackId: string) =
        match EnforcerCycleDecode.lastAssistantStep rawMessages with
        | Some(messageId, _, _) when not (String.IsNullOrWhiteSpace messageId) -> ProviderRunIdentity.create messageId
        | _ -> ProviderRunIdentity.create fallbackId

    let private mainSealedNow (ctx: Context) (sessionKey: string) =
        AgentProjection.mainSealedForBlogger ctx.Owner (AgentJournal.snapshot ctx.Durable).AgentProjections
        && not (ctx.Scope.IsDrainOpen sessionKey)

    let private rebuildFromOption (ctx: Context) (currentCtx: BloggerRequestContext option) : Task<obj list> =
        task {
            match currentCtx with
            | Some c ->
                let! rebuilt = EnforcerFrameRecovery.tryRebuildFromContext ctx.Durable ctx.BloggerSessionId c
                return rebuilt |> Option.defaultValue ctx.RawMessages
            | None -> return ctx.RawMessages
        }

    let private peekOrResolveCycleContext (ctx: Context) (sessionKey: string) =
        match ctx.Scope.TryPeekCurrentRequest sessionKey with
        | Some c -> Task.FromResult(Some c)
        | None -> EnforcerFrameRecovery.resolveCycleContext ctx.Scope ctx.Durable ctx.Owner ctx.BloggerSessionId

    let private fatalProjectRaw
        (ctx: Context)
        (sessionKey: string)
        (currentCtx: BloggerRequestContext option)
        (reason: string)
        : Task<ContinuationOutcome> =
        task {
            Diagnostic.fatal "enforcer-cycle-failed" [ "session_id", sessionKey; "result", reason ]
            do! BloggerAbandon.openRequest ctx.Durable ctx.Owner ctx.BloggerSessionId currentCtx reason
            BloggerRuntimeHost.requireReleaseObservedCurrentRequest ctx.Scope sessionKey
            return ctx.Project ctx.RawMessages
        }

    /// Evidence → Decision: sealed main → force-seal project; else inject repair.
    let private projectRepairInstruction
        (ctx: Context)
        (sessionKey: string)
        (live: BloggerRequestContext)
        (reason: string)
        : Task<ContinuationOutcome> =
        task {
            if mainSealedNow ctx sessionKey then
                BloggerRuntimeHost.forceSealRuntime ctx.Scope sessionKey
                return ctx.Project ctx.RawMessages
            else
                let! refreshed = ctx.RefreshMainContext ctx.Owner ctx.BloggerSessionId
                let fresh = refreshed |> Option.defaultValue live
                BloggerRuntimeHost.requireCurrentRequest ctx.Scope sessionKey fresh
                Diagnostic.emit "enforcer-cycle-repair" [ "session_id", sessionKey; "result", reason ]
                let requestKey = BloggerRequestId.value (BloggerRequestContext.requestId fresh)
                let terminalRun = providerRunFromLastAssistant ctx.RawMessages "unknown-repair-run"
                let! rebuilt = EnforcerFrameRecovery.tryRebuildFromContext ctx.Durable ctx.BloggerSessionId fresh

                return
                    ctx.Project(
                        EnforcerRepair.withRepairInstruction
                            (rebuilt |> Option.defaultValue ctx.RawMessages)
                            requestKey
                            terminalRun
                    )
        }

    let private firstAabbOrExhaust
        (ctx: Context)
        (sessionKey: string)
        (currentCtx: BloggerRequestContext option)
        (live: BloggerRequestContext)
        (guaranteedFirstAabb: bool)
        (reason: string)
        : Task<ContinuationOutcome> =
        if guaranteedFirstAabb then
            projectRepairInstruction ctx sessionKey live reason
        else
            fatalProjectRaw ctx sessionKey currentCtx "blog aabb exhausted; auto-recovery budget spent"

    /// Evidence → Decision: sealed main → seal; else request-scoped repair/exhaust.
    let private aabbRepair
        (ctx: Context)
        (sessionKey: string)
        (currentCtx: BloggerRequestContext option)
        (live: BloggerRequestContext)
        (guaranteedFirstAabb: bool)
        (reason: string)
        : Task<ContinuationOutcome> =
        task {
            if mainSealedNow ctx sessionKey then
                BloggerRuntimeHost.forceSealRuntime ctx.Scope sessionKey
                return ctx.Project ctx.RawMessages
            else
                return! firstAabbOrExhaust ctx sessionKey currentCtx live guaranteedFirstAabb reason
        }

    let private sameAabbTerminalReentry
        (ctx: Context)
        (sessionKey: string)
        (issuedRun: ProviderRunIdentity)
        (terminalRun: ProviderRunIdentity)
        : Task<ContinuationOutcome> =
        Diagnostic.emit
            "enforcer-cycle-aabb-pending"
            [ "session_id", sessionKey
              "result", sprintf "same terminal re-entry after AABB (%s)" (ProviderRunIdentity.value issuedRun) ]

        Task.FromResult(ctx.Project ctx.RawMessages)

    /// Evidence → Decision: interrupted-blog recovery stage → fatal or inject repair.
    let private decideInterruptedRecovery
        (ctx: Context)
        (sessionKey: string)
        (currentCtx: BloggerRequestContext option)
        (live: BloggerRequestContext)
        : Task<ContinuationOutcome> =
        let terminalRun =
            providerRunFromLastAssistant ctx.RawMessages "unknown-interrupted-run"

        match ctx.RecoveryProbe ctx.Durable ctx.BloggerSessionId ctx.RawMessages live with
        | BloggerToolRecovery.AabbRepairIssued issuedRun when issuedRun = terminalRun ->
            sameAabbTerminalReentry ctx sessionKey issuedRun terminalRun
        | BloggerToolRecovery.AabbRepairIssued _ ->
            aabbRepair ctx sessionKey currentCtx live false "blog tool interrupted after AABB"
        | _ -> projectRepairInstruction ctx sessionKey live "blog tool interrupted without completed call"

    /// Evidence → Decision: live cycle for interrupted blog → recovery or stop.
    let private decideInterruptedBlog
        (ctx: Context)
        (sessionKey: string)
        (currentCtx: BloggerRequestContext option)
        (liveCtx: BloggerRequestContext option)
        : Task<ContinuationOutcome> =
        match liveCtx with
        | Some live -> decideInterruptedRecovery ctx sessionKey currentCtx live
        | None -> Task.FromResult(ctx.Stop "unowned-interrupted-blog-without-CurrentRequest")

    let private decideProtocolRecovery
        (ctx: Context)
        (sessionKey: string)
        (currentCtx: BloggerRequestContext option)
        (live: BloggerRequestContext)
        (terminalRun: ProviderRunIdentity)
        (reason: string)
        : Task<ContinuationOutcome> =
        match ctx.RecoveryProbe ctx.Durable ctx.BloggerSessionId ctx.RawMessages live with
        | BloggerToolRecovery.NoRecovery ->
            // The transform hook runs inside the Host provider/tool-loop. Sending
            // a session nudge here can only race that loop and become a queued
            // user message. Leave the terminal untouched; a true no-tool terminal
            // is repaired from reconciled SessionIdle after quiescence instead.
            Diagnostic.emit "enforcer-cycle-nudge-deferred-to-idle" [ "session_id", sessionKey; "result", reason ]

            Task.FromResult(ctx.Project ctx.RawMessages)
        | BloggerToolRecovery.InteractionNudgeIssued issuedRun when issuedRun = terminalRun ->
            Diagnostic.emit
                "enforcer-cycle-nudge-pending"
                [ "session_id", sessionKey
                  "result", "same terminal re-entry while nudge in flight" ]

            Task.FromResult(ctx.Project ctx.RawMessages)
        | BloggerToolRecovery.InteractionNudgeIssued _ ->
            aabbRepair ctx sessionKey currentCtx live true ("nudge semantic failure; " + reason)
        | BloggerToolRecovery.AabbRepairIssued issuedRun when issuedRun = terminalRun ->
            sameAabbTerminalReentry ctx sessionKey issuedRun terminalRun
        | BloggerToolRecovery.AabbRepairIssued _ ->
            aabbRepair ctx sessionKey currentCtx live false ("invalid terminal after AABB; " + reason)

    let private decideInvalidTerminal
        (ctx: Context)
        (sessionKey: string)
        (currentCtx: BloggerRequestContext option)
        (liveCtx: BloggerRequestContext option)
        (reason: string)
        : Task<ContinuationOutcome> =
        let terminalRun = providerRunFromLastAssistant ctx.RawMessages "unknown-prose-run"

        match liveCtx with
        | None -> Task.FromResult(ctx.Stop "unowned-invalid-blog-cycle-without-CurrentRequest")
        | Some live -> decideProtocolRecovery ctx sessionKey currentCtx live terminalRun reason

    let private terminalWasSuperseded
        (ctx: Context)
        (providerRun: ProviderRunIdentity)
        (liveCtx: BloggerRequestContext option)
        =
        liveCtx
        |> Option.exists (fun live ->
            let ownership =
                BloggerRecoveryProbe.terminalRequestOwnershipForProviderRun
                    ctx.Durable
                    ctx.BloggerSessionId
                    live
                    providerRun
                    ctx.RawMessages

            ownership = BloggerTerminalRequestOwnership.Superseded)

    let private projectSupersededTerminal (ctx: Context) (sessionKey: string) (providerRun: ProviderRunIdentity) =
        Diagnostic.emit
            "enforcer-cycle-superseded"
            [ "session_id", sessionKey
              "result",
              "terminal belongs to an older Blogger request: "
              + ProviderRunIdentity.value providerRun ]

        Task.FromResult(ctx.Project ctx.RawMessages)

    let private decideCompletedInvalidCardinality
        (ctx: Context)
        (sessionKey: string)
        (currentCtx: BloggerRequestContext option)
        (providerRun: ProviderRunIdentity)
        (callCount: int)
        : Task<ContinuationOutcome> =
        let liveCtx =
            EnforcerFrameRecovery.tryLiveCycleContext ctx.Scope ctx.BloggerSessionId

        if terminalWasSuperseded ctx providerRun liveCtx then
            projectSupersededTerminal ctx sessionKey providerRun
        else
            decideInvalidTerminal
                ctx
                sessionKey
                currentCtx
                liveCtx
                (sprintf "chronicle call count = %d; expected exactly one (ENFORCER-042)" callCount)

    let invalidCardinalityBranch
        (ctx: Context)
        (messageId: string)
        (callCount: int)
        (assistantCompleted: bool)
        : Task<ContinuationOutcome> =
        task {
            let sessionKey = key ctx
            let! currentCtx = peekOrResolveCycleContext ctx sessionKey

            if not assistantCompleted then
                let! rebuilt = rebuildFromOption ctx currentCtx
                return ctx.Project rebuilt
            elif String.IsNullOrWhiteSpace messageId then
                return!
                    fatalProjectRaw ctx sessionKey currentCtx "blog cycle has no provable provider run (ENFORCER-043)"
            else
                let providerRun = ProviderRunIdentity.create messageId
                return! decideCompletedInvalidCardinality ctx sessionKey currentCtx providerRun callCount
        }

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
            let sessionKey = key ctx
            let! currentCtx = peekOrResolveCycleContext ctx sessionKey

            let liveCtx =
                EnforcerFrameRecovery.tryLiveCycleContext ctx.Scope ctx.BloggerSessionId

            let terminalRun = providerRunFromLastAssistant ctx.RawMessages "unknown-prose-run"

            if terminalWasSuperseded ctx terminalRun liveCtx then
                return! projectSupersededTerminal ctx sessionKey terminalRun
            elif EnforcerRepair.hasIncompleteBlogTool ctx.RawMessages then
                return ctx.Project ctx.RawMessages
            elif EnforcerRepair.hasAbortedBlogAttempt ctx.RawMessages then
                return! decideInterruptedBlog ctx sessionKey currentCtx liveCtx
            elif EnforcerRepair.hasErroredBlogAttempt ctx.RawMessages then
                // A real chronicle call that failed schema/tool execution still
                // has a Host-owned tool-result continuation. The Blogger gets the
                // error and may correct its own hint on the next provider step.
                // Repair here would race that continuation and surface as queued.
                return ctx.Project ctx.RawMessages
            elif EnforcerRepair.hasCompletedBlogTool ctx.RawMessages then
                return!
                    decideInvalidTerminal
                        ctx
                        sessionKey
                        currentCtx
                        liveCtx
                        "completed chronicle call did not produce one valid cycle (ENFORCER-060)"
            elif EnforcerRepair.hasAnyBlogToolPart ctx.RawMessages then
                let! rebuilt = rebuildFromOption ctx currentCtx
                return ctx.Project rebuilt
            elif not assistantCompleted then
                let! rebuilt = rebuildFromOption ctx currentCtx
                return ctx.Project rebuilt
            else
                return!
                    decideInvalidTerminal ctx sessionKey currentCtx liveCtx "no completed chronicle call (ENFORCER-060)"
        }

    let private resumeWithContext (ctx: Context) (live: BloggerRequestContext) =
        task {
            let! rebuilt = EnforcerFrameRecovery.tryRebuildFromContext ctx.Durable ctx.BloggerSessionId live
            return rebuilt |> Option.defaultValue ctx.RawMessages
        }

    let private mainBlocks (ctx: Context) (mainSessionId: SessionId) (sessionKey: string) =
        BloggerRuntimeHost.blocksNew (Some ctx.Durable) mainSessionId ctx.Scope sessionKey

    let private materializeCatchUp (ctx: Context) (live: BloggerRequestContext) : Task<Result<unit, string>> =
        taskResult {
            let! promptKey =
                ProviderWireCapture.lastUserPromptKey ctx.RawMessages
                |> Result.requireSome "next Blogger provider step has no physical PromptKey"

            do!
                BloggerCoordinator.stageContinuationContext ctx.Scope ctx.Durable live
                |> TaskResult.mapError (fun reason -> "Blogger context materialize failed: " + reason)

            do!
                BloggerCoordinator.bindContinuationContext ctx.Scope ctx.Durable live promptKey
                |> TaskResult.mapError (fun reason -> "Blogger PromptKey bind failed: " + reason)
        }

    let private resumeCatchUpWithLive
        (ctx: Context)
        (sessionKey: string)
        (live: BloggerRequestContext)
        : Task<ContinuationOutcome> =
        task {
            match! materializeCatchUp ctx live with
            | Error reason -> return! fatalProjectRaw ctx sessionKey (Some live) reason
            | Ok() ->
                let! rebuilt = resumeWithContext ctx live
                return ctx.Project rebuilt
        }

    let private refreshGapAfterPark
        (ctx: Context)
        (mainSessionId: SessionId)
        (sessionKey: string)
        (caughtUpReason: string)
        : Task<ContinuationOutcome> =
        task {
            match! ctx.RefreshMainContext mainSessionId ctx.BloggerSessionId with
            | Some live -> return! resumeCatchUpWithLive ctx sessionKey live
            | None -> return ctx.Stop caughtUpReason
        }

    let private afterParkNotResumed
        (ctx: Context)
        (mainSessionId: SessionId)
        (sessionKey: string)
        (caughtUpReason: string)
        : Task<ContinuationOutcome> =
        task {
            if mainBlocks ctx mainSessionId sessionKey then
                BloggerRuntimeHost.forceSealRuntime ctx.Scope sessionKey
                return ctx.Stop "park-ended-main-sealed"
            else
                return! refreshGapAfterPark ctx mainSessionId sessionKey caughtUpReason
        }

    let private projectAfterParkWake
        (ctx: Context)
        (mainSessionId: SessionId)
        (sessionKey: string)
        (live: BloggerRequestContext)
        : Task<ContinuationOutcome> =
        task {
            if mainBlocks ctx mainSessionId sessionKey then
                BloggerRuntimeHost.forceSealRuntime ctx.Scope sessionKey
                return ctx.Stop "park-resumed-main-sealed"
            elif ctx.Scope.HasFlight sessionKey then
                let! rebuilt = resumeWithContext ctx live
                return ctx.Project rebuilt
            else
                BloggerRuntimeHost.requireCurrentRequest ctx.Scope sessionKey live
                let! rebuilt = resumeWithContext ctx live
                return ctx.Project rebuilt
        }

    let private afterParkResumed
        (ctx: Context)
        (mainSessionId: SessionId)
        (sessionKey: string)
        (offered: BloggerRequestContext)
        : Task<ContinuationOutcome> =
        task {
            match! ctx.RefreshMainContext mainSessionId ctx.BloggerSessionId with
            | Some live -> return! projectAfterParkWake ctx mainSessionId sessionKey live
            | None -> return! projectAfterParkWake ctx mainSessionId sessionKey offered
        }

    let private parkAfterCatchUpClear
        (ctx: Context)
        (mainSessionId: SessionId)
        (sessionKey: string)
        (caughtUpReason: string)
        : Task<ContinuationOutcome> =
        task {
            match! ctx.Scope.ParkTransform sessionKey with
            | ParkWake.MaterialAvailable offered -> return! afterParkResumed ctx mainSessionId sessionKey offered
            | ParkWake.Cancelled -> return! afterParkNotResumed ctx mainSessionId sessionKey caughtUpReason
        }

    /// Evidence → Decision: catch-up refresh None → seal or park until a physical boundary.
    let private resumeCatchUpWhenNone
        (ctx: Context)
        (mainSessionId: SessionId)
        (sessionKey: string)
        (caughtUpReason: string)
        : Task<ContinuationOutcome> =
        task {
            if
                AgentProjection.mainSealedForBlogger mainSessionId (AgentJournal.snapshot ctx.Durable).AgentProjections
            then
                BloggerRuntimeHost.forceSealCellDropOffer ctx.Scope sessionKey
                BloggerRuntimeHost.requireReleaseObservedCurrentRequest ctx.Scope sessionKey
                return ctx.Stop caughtUpReason
            else
                BloggerRuntimeHost.requireReleaseObservedCurrentRequest ctx.Scope sessionKey
                return! parkAfterCatchUpClear ctx mainSessionId sessionKey caughtUpReason
        }

    /// Evidence → Decision: refresh after unblock → live project or caught-up stop.
    let private resumeCatchUpAfterUnblocked
        (ctx: Context)
        (mainSessionId: SessionId)
        (sessionKey: string)
        (caughtUpReason: string)
        : Task<ContinuationOutcome> =
        task {
            ctx.Scope.TryTakePendingOffer sessionKey |> ignore

            match! ctx.RefreshMainContext mainSessionId ctx.BloggerSessionId with
            | Some live -> return! resumeCatchUpWithLive ctx sessionKey live
            | None -> return! resumeCatchUpWhenNone ctx mainSessionId sessionKey caughtUpReason
        }

    /// Evidence → Decision: main blocks → seal stop; else catch-up drain.
    let private resumeCatchUp
        (ctx: Context)
        (mainSessionId: SessionId)
        (sessionKey: string)
        (caughtUpReason: string)
        : Task<ContinuationOutcome> =
        task {
            if mainBlocks ctx mainSessionId sessionKey then
                BloggerRuntimeHost.forceSealRuntime ctx.Scope sessionKey
                return ctx.Stop "main-sealed-blocks-request"
            else
                return! resumeCatchUpAfterUnblocked ctx mainSessionId sessionKey caughtUpReason
        }

    /// Evidence → Decision: open-by-blogger presence → missing-request vs no-authority fatal.
    let private projectUnownedLiveAuthority
        (ctx: Context)
        (mainSessionId: SessionId)
        (sessionKey: string)
        : ContinuationOutcome =
        match EnforcerRepair.tryOpenByBlogger ctx.Durable mainSessionId ctx.BloggerSessionId with
        | Some _ ->
            Diagnostic.fatal "enforcer-cycle-failed" [ "session_id", sessionKey; "result", "missing CurrentRequest" ]

            ctx.Project ctx.RawMessages
        | None ->
            Diagnostic.fatal
                "enforcer-cycle-failed"
                [ "session_id", sessionKey; "result", "live blog without cycle authority" ]

            ctx.Project ctx.RawMessages

    /// Evidence → Decision: assistant completed → stop; else unowned live fatal project.
    let private unownedLiveBlogOutcome
        (ctx: Context)
        (mainSessionId: SessionId)
        (sessionKey: string)
        (assistantCompleted: bool)
        : ContinuationOutcome =
        if assistantCompleted then
            ctx.Stop "unowned-completed-blog-without-CurrentRequest"
        else
            projectUnownedLiveAuthority ctx mainSessionId sessionKey

    let private classifyAabbForTerminal (terminalRun: ProviderRunIdentity) (recovery: BloggerToolRecovery) =
        recovery
        |> box
        |> Option.ofObj
        |> Option.map unbox<BloggerToolRecovery>
        |> Option.bind (function
            | BloggerToolRecovery.AabbRepairIssued issuedRun -> Some(issuedRun = terminalRun)
            | _ -> None)

    let private aabbIssuedForTerminal
        (ctx: Context)
        (liveCtx: BloggerRequestContext option)
        (terminalRun: ProviderRunIdentity)
        : bool option =
        liveCtx
        |> Option.bind (fun live ->
            ctx.RecoveryProbe ctx.Durable ctx.BloggerSessionId ctx.RawMessages live
            |> classifyAabbForTerminal terminalRun)

    /// Evidence → Decision: AABB refresh Option → inject disposition or fatal reason.
    let private dispositionAfterEmptyTextRefresh
        (ctx: Context)
        (sessionKey: string)
        (liveCtx: BloggerRequestContext option)
        (refreshed: BloggerRequestContext option)
        (reason: string)
        : Task<CycleDisposition> =
        task {
            let fresh = refreshed |> Option.orElse liveCtx

            match fresh with
            | None ->
                Diagnostic.fatal
                    "enforcer-cycle-failed"
                    [ "session_id", sessionKey; "result", reason + "; aabb-refresh-empty" ]

                do!
                    BloggerAbandon.openRequest
                        ctx.Durable
                        ctx.Owner
                        ctx.BloggerSessionId
                        liveCtx
                        (reason + "; aabb-refresh-empty")

                BloggerRuntimeHost.requireReleaseObservedCurrentRequest ctx.Scope sessionKey
                return CycleDisposition.Working
            | Some freshCtx ->
                BloggerRuntimeHost.requireCurrentRequest ctx.Scope sessionKey freshCtx
                Diagnostic.emit "enforcer-cycle-repair" [ "session_id", sessionKey; "result", reason ]
                return CycleDisposition.InjectRepair freshCtx
        }

    let private fatalClearWorking
        (ctx: Context)
        (mainSessionId: SessionId)
        (sessionKey: string)
        (liveCtx: BloggerRequestContext option)
        (reason: string)
        : Task<CycleDisposition> =
        task {
            Diagnostic.fatal "enforcer-cycle-failed" [ "session_id", sessionKey; "result", reason ]
            do! BloggerAbandon.openRequest ctx.Durable mainSessionId ctx.BloggerSessionId liveCtx reason
            BloggerRuntimeHost.requireReleaseObservedCurrentRequest ctx.Scope sessionKey
            return CycleDisposition.Working
        }

    let private abandonStaleDisposition
        (ctx: Context)
        (mainSessionId: SessionId)
        (sessionKey: string)
        (liveCtx: BloggerRequestContext option)
        (reason: string)
        : Task<CycleDisposition> =
        task {
            Diagnostic.emit "enforcer-cycle-stale" [ "session_id", sessionKey; "result", reason ]
            do! BloggerAbandon.openRequest ctx.Durable mainSessionId ctx.BloggerSessionId liveCtx reason
            BloggerRuntimeHost.requireReleaseObservedCurrentRequest ctx.Scope sessionKey
            return CycleDisposition.AbandonThenCatchUp
        }

    /// Evidence → Decision: squash commit outcome → disposition.
    let private dispositionAfterSquashCommit
        (ctx: Context)
        (mainSessionId: SessionId)
        (sessionKey: string)
        (liveCtx: BloggerRequestContext option)
        (providerRun: ProviderRunIdentity)
        (squash: BloggerSquashRequestContext)
        (mergedText: string)
        : Task<CycleDisposition> =
        task {
            match!
                EnforcerCycleCommit.commitSquash
                    ctx.Durable
                    mainSessionId
                    ctx.BloggerSessionId
                    providerRun
                    squash
                    mergedText
            with
            | EnforcerCycleCommit.CycleCommitOutcome.KnownCommitted ->
                BloggerRuntimeHost.requireReleaseObservedCurrentRequest ctx.Scope sessionKey
                return CycleDisposition.Committed None
            | EnforcerCycleCommit.CycleCommitOutcome.KnownNotCommitted reason ->
                return! abandonStaleDisposition ctx mainSessionId sessionKey liveCtx reason
            | EnforcerCycleCommit.CycleCommitOutcome.CommitUnknown reason ->
                Diagnostic.fatal "enforcer-cycle-commit-unknown" [ "session_id", sessionKey; "result", reason ]
                return CycleDisposition.CommitUnknown
        }

    let private sealIfMainSealedAfterCommit (ctx: Context) (mainSessionId: SessionId) (sessionKey: string) =
        if
            AgentProjection.mainSealedForBlogger mainSessionId (AgentJournal.snapshot ctx.Durable).AgentProjections
            && not (ctx.Scope.IsDrainOpen sessionKey)
        then
            BloggerRuntimeHost.forceSealCellDropOffer ctx.Scope sessionKey

    /// Evidence → Decision: main commit outcome → disposition.
    let private dispositionAfterMainCommit
        (ctx: Context)
        (mainSessionId: SessionId)
        (sessionKey: string)
        (liveCtx: BloggerRequestContext option)
        (providerRun: ProviderRunIdentity)
        (toolCallIds: ToolCallId list)
        (merged: EnforcerCycle.CanonicalCycle)
        (main: BloggerMainRequestContext)
        : Task<CycleDisposition> =
        task {
            match!
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
                sealIfMainSealedAfterCommit ctx mainSessionId sessionKey
                BloggerRuntimeHost.requireReleaseObservedCurrentRequest ctx.Scope sessionKey
                return CycleDisposition.Committed None
            | EnforcerCycleCommit.CycleCommitOutcome.KnownNotCommitted reason ->
                return! abandonStaleDisposition ctx mainSessionId sessionKey liveCtx reason
            | EnforcerCycleCommit.CycleCommitOutcome.CommitUnknown reason ->
                Diagnostic.fatal "enforcer-cycle-commit-unknown" [ "session_id", sessionKey; "result", reason ]
                return CycleDisposition.CommitUnknown
        }

    /// Evidence → Decision: main coverage/digest/open prerequisites → commit or fatal.
    let private dispositionForMainCycle
        (ctx: Context)
        (mainSessionId: SessionId)
        (sessionKey: string)
        (liveCtx: BloggerRequestContext option)
        (providerRun: ProviderRunIdentity)
        (toolCallIds: ToolCallId list)
        (merged: EnforcerCycle.CanonicalCycle)
        (main: BloggerMainRequestContext)
        : Task<CycleDisposition> =
        let tomlDigest = BlobDigest.create (HostDigest.sha256Hex main.Toml)

        let openUnbound =
            EnforcerRepair.tryOpenByBlogger ctx.Durable mainSessionId ctx.BloggerSessionId
            |> Option.exists (fun openReq -> openReq.RequestId = main.RequestId && openReq.PromptKey.IsNone)

        if tomlDigest <> main.DeltaDigest then
            fatalClearWorking ctx mainSessionId sessionKey liveCtx "delta digest mismatch"
        elif main.NextIngestedThroughSequence <= main.PreviousIngestedThroughSequence then
            fatalClearWorking ctx mainSessionId sessionKey liveCtx "coverage did not advance"
        elif openUnbound then
            fatalClearWorking ctx mainSessionId sessionKey liveCtx "open request has no PromptKey binding"
        else
            dispositionAfterMainCommit ctx mainSessionId sessionKey liveCtx providerRun toolCallIds merged main

    /// Evidence → Decision: live request context kind → squash/main commit path.
    let private dispositionForValidatedCycle
        (ctx: Context)
        (mainSessionId: SessionId)
        (sessionKey: string)
        (liveCtx: BloggerRequestContext option)
        (providerRun: ProviderRunIdentity)
        (merged: EnforcerCycle.CanonicalCycle)
        (toolCallIds: ToolCallId list)
        : Task<CycleDisposition> =
        match liveCtx with
        | Some(BloggerRequestContext.Squash squash) ->
            dispositionAfterSquashCommit ctx mainSessionId sessionKey liveCtx providerRun squash merged.MergedText
        | Some(BloggerRequestContext.Main main) ->
            dispositionForMainCycle ctx mainSessionId sessionKey liveCtx providerRun toolCallIds merged main
        | None -> fatalClearWorking ctx mainSessionId sessionKey liveCtx "missing CurrentRequest"

    /// Evidence → Decision: validateCycle Error/Ok → inject / fatal / commit disposition.
    let private runOwnedCycleBody
        (ctx: Context)
        (mainSessionId: SessionId)
        (sessionKey: string)
        (messageId: string)
        (calls: (int * ToolCallId * EnforcerCodec.CanonicalBlogCall) list)
        (liveCtx: BloggerRequestContext option)
        (providerRun: ProviderRunIdentity)
        : Task<CycleDisposition> =
        task {
            let aabbForTerminal = aabbIssuedForTerminal ctx liveCtx providerRun
            let validation = EnforcerCycleDecode.validateCycle messageId calls

            match validation, aabbForTerminal with
            | Error reason, None when
                ctx.IsEmptyTextCycleFailure reason
                && not (EnforcerRepair.hasIncompleteBlogTool ctx.RawMessages)
                ->
                let! refreshed = ctx.RefreshMainContext mainSessionId ctx.BloggerSessionId
                return! dispositionAfterEmptyTextRefresh ctx sessionKey liveCtx refreshed reason
            | Error reason, Some true when ctx.IsEmptyTextCycleFailure reason ->
                Diagnostic.emit
                    "enforcer-cycle-aabb-pending"
                    [ "session_id", sessionKey
                      "result", "same empty-text terminal re-entry after AABB" ]

                return CycleDisposition.Working
            | Error reason, Some false when ctx.IsEmptyTextCycleFailure reason ->
                return! fatalClearWorking ctx mainSessionId sessionKey liveCtx "protocol-repair-exhausted"
            | Error reason, _ -> return! fatalClearWorking ctx mainSessionId sessionKey liveCtx reason
            | Ok(merged, toolCallIds), _ ->
                return! dispositionForValidatedCycle ctx mainSessionId sessionKey liveCtx providerRun merged toolCallIds
        }

    /// Evidence → Decision: the request-scoped AABB projection owns its single repair.
    let private finishInjectRepair
        (ctx: Context)
        (messageId: string)
        (live: BloggerRequestContext)
        : Task<ContinuationOutcome> =
        task {
            let requestKey = BloggerRequestId.value (BloggerRequestContext.requestId live)
            let terminalRun = ProviderRunIdentity.create messageId
            let! rebuilt = resumeWithContext ctx live
            return ctx.Project(EnforcerRepair.withRepairInstruction rebuilt requestKey terminalRun)
        }

    let private finishWorking (ctx: Context) (liveCtx: BloggerRequestContext option) : Task<ContinuationOutcome> =
        task {
            match liveCtx with
            | Some live ->
                let! rebuilt = resumeWithContext ctx live
                return ctx.Project rebuilt
            | None -> return ctx.Project ctx.RawMessages
        }

    /// Evidence → Decision: durable seal after catch-up → stop; else park wait.
    let private finishCaughtUpAfterCommit
        (ctx: Context)
        (mainSessionId: SessionId)
        (sessionKey: string)
        : Task<ContinuationOutcome> =
        task {
            if
                AgentProjection.mainSealedForBlogger mainSessionId (AgentJournal.snapshot ctx.Durable).AgentProjections
            then
                BloggerRuntimeHost.forceSealCellDropOffer ctx.Scope sessionKey
                BloggerRuntimeHost.requireReleaseObservedCurrentRequest ctx.Scope sessionKey
                return ctx.Stop "main-sealed-caught-up"
            else
                BloggerRuntimeHost.requireReleaseObservedCurrentRequest ctx.Scope sessionKey
                return! parkAfterCatchUpClear ctx mainSessionId sessionKey "park-ended-catch-up-complete"
        }

    /// Evidence → Decision: post-commit refresh × afterSquashMain → drain or catch-up.
    let private drainAfterCommitMaterial
        (ctx: Context)
        (mainSessionId: SessionId)
        (sessionKey: string)
        (refreshed: BloggerRequestContext option)
        (afterSquashMain: BloggerRequestContext option)
        : Task<ContinuationOutcome> =
        task {
            match refreshed, afterSquashMain with
            | Some live, _
            | None, Some live ->
                let! refreshedAgain = ctx.RefreshMainContext mainSessionId ctx.BloggerSessionId
                let live = refreshedAgain |> Option.defaultValue live
                return! resumeCatchUpWithLive ctx sessionKey live
            | None, None -> return! finishCaughtUpAfterCommit ctx mainSessionId sessionKey
        }

    /// Evidence → Decision: main sealed after commit → stop; else drain next window.
    let private finishCommitted
        (ctx: Context)
        (mainSessionId: SessionId)
        (sessionKey: string)
        (afterSquashMain: BloggerRequestContext option)
        : Task<ContinuationOutcome> =
        task {
            if mainBlocks ctx mainSessionId sessionKey then
                BloggerRuntimeHost.forceSealRuntime ctx.Scope sessionKey
                return ctx.Stop "main-sealed-after-commit"
            else
                ctx.Scope.TryTakePendingOffer sessionKey |> ignore
                let! refreshed = ctx.RefreshMainContext mainSessionId ctx.BloggerSessionId
                return! drainAfterCommitMaterial ctx mainSessionId sessionKey refreshed afterSquashMain
        }

    /// Evidence → Decision: cycle disposition → inject / catch-up / project / drain.
    let private finishOwnedDisposition
        (ctx: Context)
        (mainSessionId: SessionId)
        (sessionKey: string)
        (messageId: string)
        (liveCtx: BloggerRequestContext option)
        (disposition: CycleDisposition)
        : Task<ContinuationOutcome> =
        match disposition with
        | CycleDisposition.InjectRepair live -> finishInjectRepair ctx messageId live
        | CycleDisposition.CommitUnknown -> Task.FromResult(ctx.Project ctx.RawMessages)
        | CycleDisposition.AbandonThenCatchUp ->
            resumeCatchUp ctx mainSessionId sessionKey "stale-cycle-catch-up-complete"
        | CycleDisposition.Working -> finishWorking ctx liveCtx
        | CycleDisposition.Committed afterSquashMain -> finishCommitted ctx mainSessionId sessionKey afterSquashMain

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
            let sessionKey = key ctx

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

            if alreadyEntry || alreadyReceipt then
                return! resumeCatchUp ctx mainSessionId sessionKey "idempotent-receipt-catch-up-complete"
            elif liveCtx.IsNone then
                return unownedLiveBlogOutcome ctx mainSessionId sessionKey assistantCompleted
            elif terminalWasSuperseded ctx providerRun liveCtx then
                return! projectSupersededTerminal ctx sessionKey providerRun
            else
                let! disposition = runOwnedCycleBody ctx mainSessionId sessionKey messageId calls liveCtx providerRun

                return! finishOwnedDisposition ctx mainSessionId sessionKey messageId liveCtx disposition
        }

    /// Branch 3 — COMPANION-005 first request / non-tool step: rebuild only from
    /// durable frames + typed CurrentRequest. Never extract TOML from raw user
    /// messages (C2).
    let firstRequestBranch
        (scope: IBloggerRuntimeHost)
        (journal: AgentJournal option)
        (bloggerSessionId: SessionId)
        (rawMessages: obj list)
        (project: obj list -> ContinuationOutcome)
        : Task<ContinuationOutcome> =
        task {
            match journal, scope.TryPeekCurrentRequest(SessionId.value bloggerSessionId) with
            | Some durable, Some ctx ->
                let! rebuilt = EnforcerFrameRecovery.tryRebuildFromContext durable bloggerSessionId ctx
                return project (rebuilt |> Option.defaultValue rawMessages)
            | _ -> return project rawMessages
        }


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

    let private isEmptyTextCycleFailure (reason: string) : bool =
        reason = EnforcerCycleDecode.EmptyTextError

    /// The Blogger continuation-transform handler (moved from EnforcerHost to
    /// break the Host.fs ↔ Continuation.fs compile-order cycle).
    ///
    /// Thin dispatcher over the three branches (emptyCallsBranch / commitBranch /
    /// firstRequestBranch): it only derives the closed branch context and forwards.
    let handleContinuation
        (scope: IBloggerRuntimeHost)
        (journal: AgentJournal option)
        (recoveryProbe: AgentJournal -> SessionId -> obj list -> RecoveryStageProbe)
        (bloggerSessionId: SessionId)
        (rawMessages: obj list)
        : Task<ContinuationOutcome> =
        task {
            let project (msgs: obj list) = projectMessages msgs rawMessages

            let stop (reason: string) =
                stopPhysicalRun rawMessages rawMessages reason

            let mainSessionId =
                journal
                |> Option.bind (fun j ->
                    SessionAssociationProjection.tryMainSessionOf
                        bloggerSessionId
                        (AgentJournal.snapshot j).AgentProjections.Associations)

            let mkCtx (durable: AgentJournal) (owner: SessionId) : Context =
                { Scope = scope
                  Journal = journal
                  Durable = durable
                  Owner = owner
                  BloggerSessionId = bloggerSessionId
                  RawMessages = rawMessages
                  RecoveryProbe = recoveryProbe
                  Project = project
                  Stop = stop
                  RefreshMainContext = BloggerMainContext.fromJournal scope durable
                  IsEmptyTextCycleFailure = isEmptyTextCycleFailure }

            let chronicleCallCount = EnforcerRepair.chronicleCallCount rawMessages

            match journal, mainSessionId, EnforcerCycleDecode.extractCalls rawMessages with
            | Some durable, Some owner, Some(messageId, _, assistantCompleted) when chronicleCallCount > 1 ->
                return! invalidCardinalityBranch (mkCtx durable owner) messageId chronicleCallCount assistantCompleted
            | Some durable, Some owner, Some(_messageId, calls, assistantCompleted) when List.isEmpty calls ->
                return! emptyCallsBranch (mkCtx durable owner) assistantCompleted
            | Some durable, Some owner, Some(messageId, calls, assistantCompleted) ->
                return! commitBranch (mkCtx durable owner) messageId calls assistantCompleted
            | _ -> return! firstRequestBranch scope journal bloggerSessionId rawMessages project
        }



    let private projectOrKeepRaw (sessionId: string) (bloggerMessages: obj list) (messages: obj list) : obj list =
        if List.isEmpty messages then
            Diagnostic.emit
                "enforcer-empty-project"
                [ "session_id", sessionId
                  "result", "ProjectMessages empty; keep raw transcript" ]

            bloggerMessages
        else
            messages

    let private messagesOrRaw (bloggerMessages: obj list) (messages: obj list) : obj list =
        if List.isEmpty messages then bloggerMessages else messages

    let private terminalRunFromMessages (rawMessages: obj list) : ProviderRunIdentity =
        match EnforcerCycleDecode.lastAssistantStep rawMessages with
        | Some(messageId, _, _) when not (String.IsNullOrWhiteSpace messageId) -> ProviderRunIdentity.create messageId
        | _ -> ProviderRunIdentity.create "unknown-prose-run"

    let private makeRecoveryProbe
        (durable: AgentJournal)
        (sid: SessionId)
        (rawMessages: obj list)
        : RecoveryStageProbe =
        fun ctx ->
            let terminalRun = terminalRunFromMessages rawMessages
            let requestKey = BloggerRequestId.value (BloggerRequestContext.requestId ctx)
            BloggerRecoveryProbe.repairState durable sid requestKey terminalRun rawMessages

    let private handlePhysicalStopResult
        (sid: SessionId)
        (sessionId: string)
        (physicalUserMessageId: PhysicalUserMessageId option)
        result
        =
        match result with
        | Ok() ->
            physicalUserMessageId
            |> Option.iter (fun physical ->
                ModelRouting.suppressProviderStep sid physical
                ModelRouting.releasePhysicalExecution sid physical |> ignore)
        | Error error ->
            Diagnostic.emit "enforcer-stop-physical-run" [ "session_id", sessionId; "result", "abort-error: " + error ]

    let private requestPhysicalStop
        (terminateSession: SessionTermination)
        (sid: SessionId)
        (sessionId: string)
        (physicalUserMessageId: PhysicalUserMessageId option)
        (reason: string)
        =
        task {
            try
                let! result = terminateSession sid reason
                handlePhysicalStopResult sid sessionId physicalUserMessageId result
            with ex ->
                Diagnostic.emit
                    "enforcer-stop-physical-run"
                    [ "session_id", sessionId; "result", "abort-exception: " + ex.Message ]
        }
        |> ignore

    let private applyContinuationOutcome
        (terminateSession: SessionTermination)
        (sid: SessionId)
        (sessionId: string)
        (bloggerMessages: obj list)
        (outObj: obj)
        (outcome: ContinuationOutcome)
        : Task =
        task {
            match outcome with
            | ContinuationOutcome.ProjectMessages messages ->
                HostMessageProjection.replaceMessagesInPlace
                    outObj
                    (projectOrKeepRaw sessionId bloggerMessages messages)
            | ContinuationOutcome.StopPhysicalRun(messages, reason) ->
                HostMessageProjection.replaceMessagesInPlace outObj (messagesOrRaw bloggerMessages messages)

                Diagnostic.emit "enforcer-stop-physical-run" [ "session_id", sessionId; "result", reason ]

                requestPhysicalStop
                    terminateSession
                    sid
                    sessionId
                    (ProviderWireCapture.lastUserMessageId bloggerMessages)
                    reason
        }

    let private runEnforcerWhenFamilyReady
        (scope: PluginRuntimeScope)
        (journal: AgentJournal option)
        (durable: AgentJournal)
        (terminateSession: SessionTermination)
        (sid: SessionId)
        (sessionId: string)
        (outObj: obj)
        : Task =
        task {
            let bloggerMessages = unbox<obj array> outObj?messages |> Array.toList

            let! outcome = handleContinuation scope.BloggerRuntimeHost journal makeRecoveryProbe sid bloggerMessages

            do! applyContinuationOutcome terminateSession sid sessionId bloggerMessages outObj outcome
        }

    let private runEnforcerAfterRecovery
        (scope: PluginRuntimeScope)
        (journal: AgentJournal option)
        (durable: AgentJournal)
        (terminateSession: SessionTermination)
        (sid: SessionId)
        (sessionId: string)
        (outObj: obj)
        (recovery: SessionRecovery.FamilyRecovery)
        : Task =
        match recovery with
        | SessionRecovery.FamilyRecovery.FamilyBlocked _ -> Task.FromResult()
        | SessionRecovery.FamilyRecovery.FamilyWaiting _
        | SessionRecovery.FamilyRecovery.FamilyReady _ ->
            runEnforcerWhenFamilyReady scope journal durable terminateSession sid sessionId outObj

    let private runEnforcerForMainSession
        (scope: PluginRuntimeScope)
        (journal: AgentJournal option)
        (durable: AgentJournal)
        (terminateSession: SessionTermination)
        (sid: SessionId)
        (sessionId: string)
        (outObj: obj)
        : Task =
        task {
            let! recovery = scope.EnsureRecoveryDone sid
            do! runEnforcerAfterRecovery scope journal durable terminateSession sid sessionId outObj recovery
        }

    let private runEnforcerIfMainAssociated
        (scope: PluginRuntimeScope)
        (journal: AgentJournal option)
        (durable: AgentJournal)
        (terminateSession: SessionTermination)
        (sid: SessionId)
        (sessionId: string)
        (outObj: obj)
        : Task =
        let associations = (AgentJournal.snapshot durable).AgentProjections.Associations

        match SessionAssociationProjection.tryMainSessionOf sid associations with
        | Some _ -> runEnforcerForMainSession scope journal durable terminateSession sid sessionId outObj
        | None -> Task.FromResult()

    let applyContinuation
        (scope: PluginRuntimeScope)
        (journal: AgentJournal option)
        (terminateSession: SessionTermination)
        (projectionSessionIdOpt: string option)
        (outObj: obj)
        : Task =
        match projectionSessionIdOpt, journal with
        | Some sessionId, Some durable ->
            let sid = SessionId.create sessionId
            runEnforcerIfMainAssociated scope journal durable terminateSession sid sessionId outObj
        | _ -> Task.FromResult()
