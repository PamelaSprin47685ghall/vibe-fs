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
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Persistence.Journal

/// Session termination capability — same signature as PluginTransforms.fs private type.
type SessionTermination = SessionId -> string -> Task<Result<unit, string>>

/// The Blogger continuation transform (ENFORCER-044).
///
/// A provider step answers the latest physical user message (Host
/// `MessageV2.latest`), and the transform input never holds the step being
/// built. The step position decides what the continuation may do.
///
/// First step of a physical message: nothing answers it yet, and the history
/// tail answers an older physical message, so nothing is judged. The step only
/// receives the live request's canonical view (COMPANION-005), followed by the
/// physical repair prompt when that prompt is a repair scoped to the request.
///
/// Later step of the same Host loop: the previous step answers this physical
/// message. Only when its durable landing proves the physical message belongs
/// to the live request (context-compression-024) may the continuation commit,
/// hand the step to the single repair owner, or stop the physical run.
/// Superseded and unproven steps pass through untouched.
module EnforcerContinuation =

    /// Local outcome of one owned commit (no program-counter bools).
    [<RequireQualifiedAccess>]
    type CycleDisposition =
        | Working
        | Committed of afterSquashMain: BloggerRequestContext option
        | CommitUnknown
        | AbandonThenCatchUp

    /// Continuation transform result. Empty message lists are forbidden: Host
    /// forwards them as provider `messages` and rejects with 400.
    /// StopPhysicalRun asks the plugin to interrupt only the current physical attempt after projecting messages.
    [<RequireQualifiedAccess>]
    type ContinuationOutcome =
        | ProjectMessages of obj list
        | StopPhysicalRun of messages: obj list * reason: string

    /// One owned step: the live request its physical message is proven to
    /// belong to, and the previous step of that Host loop.
    type Context =
        { Scope: IBloggerRuntimeHost
          Durable: AgentJournal
          Owner: SessionId
          BloggerSessionId: SessionId
          Live: BloggerRequestContext
          Previous: EnforcerCycleDecode.AssistantStep
          RawMessages: obj list
          Project: obj list -> ContinuationOutcome
          Stop: string -> ContinuationOutcome
          RefreshMainContext: SessionId -> SessionId -> Task<BloggerRequestContext option>
          IsEmptyTextCycleFailure: string -> bool }

    let private key (ctx: Context) = SessionId.value ctx.BloggerSessionId

    let private previousRun (ctx: Context) =
        ProviderRunIdentity.create ctx.Previous.MessageId

    /// Durable seal fact only: sealing cancels the physical parked waiter and
    /// relies on the durable handle seal. There is no reopenable drain latch.
    let private durableSealedFor (ctx: Context) (mainSessionId: SessionId) =
        BloggerRuntimeHost.durableSealed (Some ctx.Durable) mainSessionId

    /// Release exactly the known request. Same identity is idempotent
    /// (Released/Missing); a foreign identity fails closed.
    let private releaseExact (ctx: Context) (sessionKey: string) (exact: BloggerRequestContext) =
        match BloggerRuntimeHost.releaseCurrentRequest ctx.Scope sessionKey exact with
        | Ok() -> ()
        | Error reason ->
            // Exact-owner release conflict: the flight slot moved to a foreign
            // request between our claim and this release — the same race a
            // superseded repair episode produces. Killing the process here
            // would turn a routine supersedure into a crash; the released slot
            // is never touched, so decline and keep the evidence path retraced.
            Diagnostic.emit "blogger-flight-release-conflict" [ "session_id", sessionKey; "result", reason ]

    /// Release whatever exact request is currently observed, if any. Only
    /// used where the exact context is not already in hand; the peeked
    /// context itself is the released identity, so a foreign owner trips.
    let private releaseObservedExact (ctx: Context) (sessionKey: string) =
        match ctx.Scope.TryPeekCurrentRequest sessionKey with
        | Some exact -> releaseExact ctx sessionKey exact
        | None -> ()

    /// Canonical provider view of a request: durable frames + typed request
    /// context. A rebuild miss keeps the Host transcript.
    let private resumeWithContext (ctx: Context) (live: BloggerRequestContext) =
        task {
            let! rebuilt = EnforcerFrameRecovery.tryRebuildFromContext ctx.Durable ctx.BloggerSessionId live
            return rebuilt |> Option.defaultValue ctx.RawMessages
        }

    let private abandonThenStop
        (ctx: Context)
        (sessionKey: string)
        (request: BloggerRequestContext)
        (reason: string)
        : Task<ContinuationOutcome> =
        task {
            do! BloggerAbandon.openRequest ctx.Durable ctx.Owner ctx.BloggerSessionId (Some request) reason
            releaseExact ctx sessionKey request
            return ctx.Stop reason
        }

    /// Coordinator-admitted in-loop repair: the live canonical view followed by
    /// the request-scoped repair instruction for the previous step.
    let private finishInjectRepair (ctx: Context) : Task<ContinuationOutcome> =
        task {
            let requestKey = BloggerRequestId.value (BloggerRequestContext.requestId ctx.Live)
            let! view = resumeWithContext ctx ctx.Live
            return ctx.Project(EnforcerRepair.withRepairInstruction view requestKey (previousRun ctx))
        }

    /// Evidence → Decision: an owned invalid step goes to the single repair
    /// owner (BloggerCoordinator.observeTransformRepair). Transform never sends
    /// a physical protocol nudge behind a live Host tool loop: a nudge that
    /// must come from idle stops this owned run so idle can deliver it. A
    /// verdict that the coordinator does not own this step means ownership
    /// moved underneath; the Host loop is then left untouched.
    let private repairStep (ctx: Context) (sessionKey: string) (reason: string) : Task<ContinuationOutcome> =
        task {
            match!
                BloggerCoordinator.observeTransformRepair
                    ctx.Scope
                    (Some ctx.Durable)
                    ctx.Live
                    (previousRun ctx)
                    ctx.RawMessages
            with
            | BloggerRepairOutcome.RepairInjected _ -> return! finishInjectRepair ctx
            | BloggerRepairOutcome.PendingRepairWait ->
                Diagnostic.emit "enforcer-cycle-nudge-deferred-to-idle" [ "session_id", sessionKey; "result", reason ]
                return ctx.Stop "enforcer-cycle-nudge-deferred-to-idle"
            | BloggerRepairOutcome.AbandonedExhausted -> return ctx.Stop "blogger-protocol-repair-exhausted"
            | BloggerRepairOutcome.NudgeSent _
            | BloggerRepairOutcome.AabbSent _
            | BloggerRepairOutcome.Completed ->
                // A transform observation is a read-only rendezvous post: only
                // idle may be answered with a send, and nothing answers with
                // completion. This reply breaches the rendezvous contract.
                Diagnostic.fatal "enforcer-repair-outcome-mismatch" [ "session_id", sessionKey; "result", reason ]
                return ctx.Stop "enforcer-repair-outcome-mismatch"
            | BloggerRepairOutcome.SupersededIgnored
            | BloggerRepairOutcome.UnprovenIgnored
            | BloggerRepairOutcome.UnownedIdleIgnored ->
                Diagnostic.emit "enforcer-cycle-not-owned" [ "session_id", sessionKey; "result", reason ]
                return ctx.Project ctx.RawMessages
        }

    /// More than one chronicle call on the owned previous step (ENFORCER-042).
    let private invalidCardinalityStep (ctx: Context) (sessionKey: string) (callCount: int) =
        task {
            if ctx.Previous.Completed then
                return!
                    repairStep
                        ctx
                        sessionKey
                        (sprintf "chronicle call count = %d; expected exactly one (ENFORCER-042)" callCount)
            else
                let! view = resumeWithContext ctx ctx.Live
                return ctx.Project view
        }

    /// The owned previous step carries no valid chronicle call.
    /// 1) pending/running blog — Host re-enters after tool completion
    /// 2) interrupted blog (status=error + interrupted) — the run was cut
    /// 3) chronicle tool error — Host-owned tool-result continuation
    /// 4) completed chronicle that decodes to no valid cycle — protocol repair
    /// 5) no chronicle part on a completed step — ENFORCER-060 protocol repair
    let private emptyCallsStep (ctx: Context) (sessionKey: string) : Task<ContinuationOutcome> =
        task {
            let previous = ctx.Previous

            if EnforcerRepair.hasIncompleteBlogTool previous then
                return ctx.Project ctx.RawMessages
            elif EnforcerRepair.hasAbortedBlogAttempt previous then
                return! repairStep ctx sessionKey "chronicle call was interrupted"
            elif EnforcerRepair.hasErroredBlogAttempt previous then
                // A real chronicle call that failed schema/tool execution still
                // has a Host-owned tool-result continuation. The Blogger gets the
                // error and may correct its own hint on the next provider step.
                // Repair here would race that continuation and surface as queued.
                return ctx.Project ctx.RawMessages
            elif EnforcerRepair.hasCompletedBlogTool previous then
                return!
                    repairStep ctx sessionKey "completed chronicle call did not produce one valid cycle (ENFORCER-060)"
            elif EnforcerRepair.hasAnyBlogToolPart previous || not previous.Completed then
                let! view = resumeWithContext ctx ctx.Live
                return ctx.Project view
            else
                return! repairStep ctx sessionKey "no completed chronicle call (ENFORCER-060)"
        }

    let private materializeCatchUp (ctx: Context) (live: BloggerRequestContext) : Task<Result<unit, string>> =
        taskResult {
            let! promptKey =
                ProviderWireCapture.lastUserPromptKey ctx.RawMessages
                |> Result.requireSome "next Blogger provider step has no physical PromptKey"

            let existingOpen =
                (AgentJournal.snapshot ctx.Durable).AgentProjections.Sessions
                |> Map.tryFind (BloggerRequestContext.mainSessionId live)
                |> Option.bind (fun s -> s.BloggerCycles)
                |> Option.bind (fun cycles -> Map.tryFind (BloggerRequestContext.requestId live) cycles.OpenByRequestId)

            match existingOpen with
            | Some openReq when openReq.PromptKey = Some promptKey -> ()
            | _ ->
                do!
                    BloggerCoordinator.materializeContinuationContext ctx.Scope ctx.Durable live
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
            | Error reason -> return! abandonThenStop ctx sessionKey live reason
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
            if durableSealedFor ctx mainSessionId then
                ctx.Scope.CancelParked sessionKey
                return ctx.Stop "park-ended-main-sealed"
            else
                return! refreshGapAfterPark ctx mainSessionId sessionKey caughtUpReason
        }

    /// Parked-resume adjudication: claim succeeded → continue building
    /// context; claim conflict → emit + ctx.Stop so the foreign owner stays
    /// the only writer of this frame, never a fatal trip over a routine
    /// supersede.
    let private resumeAfterClaim
        (ctx: Context)
        (sessionKey: string)
        (live: BloggerRequestContext)
        : Task<ContinuationOutcome> =
        task {
            match BloggerRuntimeHost.claimCurrentRequest ctx.Scope sessionKey live with
            | Ok() ->
                let! rebuilt = resumeWithContext ctx live
                return ctx.Project rebuilt
            | Error reason ->
                Diagnostic.emit "blogger-flight-claim-conflict" [ "session_id", sessionKey; "result", reason ]
                return ctx.Stop "park-resumed-foreign-flight"
        }

    let private projectAfterParkWake
        (ctx: Context)
        (mainSessionId: SessionId)
        (sessionKey: string)
        (live: BloggerRequestContext)
        : Task<ContinuationOutcome> =
        task {
            if durableSealedFor ctx mainSessionId then
                ctx.Scope.CancelParked sessionKey
                return ctx.Stop "park-resumed-main-sealed"
            else
                // Exact claim: already-ours refreshes, empty claims, foreign
                // declines. A foreign identity here means this parked resume
                // arrived after the parked context was superseded — the correct
                // outcome is ctx.Stop (committed evidence preserved), not a
                // fatal trip over a routine supersede.
                return! resumeAfterClaim ctx sessionKey live
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

    /// Evidence → Decision: durable seal after refresh-None → cancel waiter and stop; else park.
    let private resumeCatchUpWhenNone
        (ctx: Context)
        (mainSessionId: SessionId)
        (sessionKey: string)
        (caughtUpReason: string)
        : Task<ContinuationOutcome> =
        task {
            if durableSealedFor ctx mainSessionId then
                ctx.Scope.CancelParked sessionKey
                releaseObservedExact ctx sessionKey
                return ctx.Stop caughtUpReason
            else
                releaseObservedExact ctx sessionKey
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
            match! ctx.RefreshMainContext mainSessionId ctx.BloggerSessionId with
            | Some live -> return! resumeCatchUpWithLive ctx sessionKey live
            | None -> return! resumeCatchUpWhenNone ctx mainSessionId sessionKey caughtUpReason
        }

    /// Evidence → Decision: durable sealed → cancel waiter and stop; else catch-up park.
    let private resumeCatchUp
        (ctx: Context)
        (mainSessionId: SessionId)
        (sessionKey: string)
        (caughtUpReason: string)
        : Task<ContinuationOutcome> =
        task {
            if durableSealedFor ctx mainSessionId then
                ctx.Scope.CancelParked sessionKey
                return ctx.Stop "main-sealed-blocks-request"
            else
                return! resumeCatchUpAfterUnblocked ctx mainSessionId sessionKey caughtUpReason
        }

    let private fatalClearWorking
        (ctx: Context)
        (mainSessionId: SessionId)
        (sessionKey: string)
        (reason: string)
        : Task<CycleDisposition> =
        task {
            // Settlement first, fuse last: the durable abandon and the exact flight
            // release are this cycle's closing acts. The process dies on fuse
            // either way, so abandonment evidence is attempted and reported before
            // the fatal record, never scheduled after it.
            try
                do! BloggerAbandon.openRequest ctx.Durable mainSessionId ctx.BloggerSessionId (Some ctx.Live) reason
            with ex ->
                Diagnostic.emit
                    "enforcer-cycle-settlement-failed"
                    [ "session_id", sessionKey
                      "result", "abandonment evidence failed: " + ex.Message ]

            releaseExact ctx sessionKey ctx.Live
            Diagnostic.fatal "enforcer-cycle-failed" [ "session_id", sessionKey; "result", reason ]
            return CycleDisposition.Working
        }

    let private abandonStaleDisposition
        (ctx: Context)
        (mainSessionId: SessionId)
        (sessionKey: string)
        (reason: string)
        : Task<CycleDisposition> =
        task {
            Diagnostic.emit "enforcer-cycle-stale" [ "session_id", sessionKey; "result", reason ]
            do! BloggerAbandon.openRequest ctx.Durable mainSessionId ctx.BloggerSessionId (Some ctx.Live) reason
            releaseExact ctx sessionKey ctx.Live
            return CycleDisposition.AbandonThenCatchUp
        }

    /// Evidence → Decision: squash commit outcome → disposition.
    let private dispositionAfterSquashCommit
        (ctx: Context)
        (mainSessionId: SessionId)
        (sessionKey: string)
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
                releaseExact ctx sessionKey ctx.Live
                return CycleDisposition.Committed None
            | EnforcerCycleCommit.CycleCommitOutcome.KnownNotCommitted reason ->
                return! abandonStaleDisposition ctx mainSessionId sessionKey reason
            | EnforcerCycleCommit.CycleCommitOutcome.CommitUnknown reason ->
                // The durable commit evidence is indeterminate — keep the exact
                // request in-flight. The caller keeps the pending marker and
                // projects again; the write is not lost to a premature process
                // exit masking durable evidence.
                Diagnostic.emit "enforcer-cycle-commit-unknown" [ "session_id", sessionKey; "result", reason ]
                return CycleDisposition.CommitUnknown
        }

    /// Durable seal after commit only cancels the physical parked waiter; the
    /// durable seal fact itself blocks new work. Never reopens durable work.
    let private sealIfMainSealedAfterCommit (ctx: Context) (mainSessionId: SessionId) (sessionKey: string) =
        if BloggerRuntimeHost.durableSealed (Some ctx.Durable) mainSessionId then
            ctx.Scope.CancelParked sessionKey

    /// Evidence → Decision: main commit outcome → disposition.
    let private dispositionAfterMainCommit
        (ctx: Context)
        (mainSessionId: SessionId)
        (sessionKey: string)
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
                releaseExact ctx sessionKey ctx.Live
                return CycleDisposition.Committed None
            | EnforcerCycleCommit.CycleCommitOutcome.KnownNotCommitted reason ->
                return! abandonStaleDisposition ctx mainSessionId sessionKey reason
            | EnforcerCycleCommit.CycleCommitOutcome.CommitUnknown reason ->
                Diagnostic.emit "enforcer-cycle-commit-unknown" [ "session_id", sessionKey; "result", reason ]
                return CycleDisposition.CommitUnknown
        }

    /// Evidence → Decision: main coverage/digest/open prerequisites → commit or fatal.
    let private dispositionForMainCycle
        (ctx: Context)
        (mainSessionId: SessionId)
        (sessionKey: string)
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
            fatalClearWorking ctx mainSessionId sessionKey "delta digest mismatch"
        elif main.NextIngestedThroughSequence <= main.PreviousIngestedThroughSequence then
            fatalClearWorking ctx mainSessionId sessionKey "coverage did not advance"
        elif openUnbound then
            fatalClearWorking ctx mainSessionId sessionKey "open request has no PromptKey binding"
        else
            dispositionAfterMainCommit ctx mainSessionId sessionKey providerRun toolCallIds merged main

    /// Evidence → Decision: live request context kind → squash/main commit path.
    let private dispositionForValidatedCycle
        (ctx: Context)
        (mainSessionId: SessionId)
        (sessionKey: string)
        (providerRun: ProviderRunIdentity)
        (merged: EnforcerCycle.CanonicalCycle)
        (toolCallIds: ToolCallId list)
        : Task<CycleDisposition> =
        match ctx.Live with
        | BloggerRequestContext.Squash squash ->
            dispositionAfterSquashCommit ctx mainSessionId sessionKey providerRun squash merged.MergedText
        | BloggerRequestContext.Main main ->
            dispositionForMainCycle ctx mainSessionId sessionKey providerRun toolCallIds merged main

    let private finishWorking (ctx: Context) : Task<ContinuationOutcome> =
        task {
            let! rebuilt = resumeWithContext ctx ctx.Live
            return ctx.Project rebuilt
        }

    /// Evidence → Decision: durable seal after catch-up → cancel waiter and stop; else park wait.
    let private finishCaughtUpAfterCommit
        (ctx: Context)
        (mainSessionId: SessionId)
        (sessionKey: string)
        : Task<ContinuationOutcome> =
        task {
            if durableSealedFor ctx mainSessionId then
                ctx.Scope.CancelParked sessionKey
                releaseObservedExact ctx sessionKey
                return ctx.Stop "main-sealed-caught-up"
            else
                releaseObservedExact ctx sessionKey
                return! parkAfterCatchUpClear ctx mainSessionId sessionKey "park-ended-catch-up-complete"
        }

    /// Evidence → Decision: post-commit refresh × afterSquashMain → resume or catch-up stop.
    let private catchUpAfterCommitMaterial
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

    /// Evidence → Decision: main sealed after commit → cancel waiter and stop; else park next material.
    let private finishCommitted
        (ctx: Context)
        (mainSessionId: SessionId)
        (sessionKey: string)
        (afterSquashMain: BloggerRequestContext option)
        : Task<ContinuationOutcome> =
        task {
            if durableSealedFor ctx mainSessionId then
                ctx.Scope.CancelParked sessionKey
                return ctx.Stop "main-sealed-after-commit"
            else
                let! refreshed = ctx.RefreshMainContext mainSessionId ctx.BloggerSessionId
                return! catchUpAfterCommitMaterial ctx mainSessionId sessionKey refreshed afterSquashMain
        }

    /// Evidence → Decision: commit disposition → catch-up / project / park.
    let private finishOwnedDisposition
        (ctx: Context)
        (mainSessionId: SessionId)
        (sessionKey: string)
        (disposition: CycleDisposition)
        : Task<ContinuationOutcome> =
        match disposition with
        | CycleDisposition.CommitUnknown -> Task.FromResult(ctx.Project ctx.RawMessages)
        | CycleDisposition.AbandonThenCatchUp ->
            resumeCatchUp ctx mainSessionId sessionKey "stale-cycle-catch-up-complete"
        | CycleDisposition.Working -> finishWorking ctx
        | CycleDisposition.Committed afterSquashMain -> finishCommitted ctx mainSessionId sessionKey afterSquashMain

    /// Evidence → Decision: cycle validation → coordinator repair / fatal / commit.
    let private commitValidated
        (ctx: Context)
        (mainSessionId: SessionId)
        (sessionKey: string)
        (providerRun: ProviderRunIdentity)
        (validation: Result<EnforcerCycle.CanonicalCycle * ToolCallId list, string>)
        : Task<ContinuationOutcome> =
        match validation with
        | Error reason when
            ctx.IsEmptyTextCycleFailure reason
            && not (EnforcerRepair.hasIncompleteBlogTool ctx.Previous)
            ->
            repairStep ctx sessionKey reason
        | Error reason ->
            task {
                let! disposition = fatalClearWorking ctx mainSessionId sessionKey reason
                return! finishOwnedDisposition ctx mainSessionId sessionKey disposition
            }
        | Ok(merged, toolCallIds) ->
            task {
                let! disposition =
                    dispositionForValidatedCycle ctx mainSessionId sessionKey providerRun merged toolCallIds

                return! finishOwnedDisposition ctx mainSessionId sessionKey disposition
            }

    /// ENFORCER-044: commit the owned previous step's chronicle call.
    ///
    /// Host prompt.ts: processor.cleanup sets time.completed AFTER tools finish
    /// and BEFORE the next loop iteration reloads msgs and re-triggers
    /// transform, so a completed chronicle part always comes with a completed
    /// assistant. Skipping commit on that flag would freeze RecordCoverage.
    /// ENFORCER-154 alreadyEntry/alreadyReceipt still refuse re-commit.
    let commitBranch
        (ctx: Context)
        (calls: (int * ToolCallId * EnforcerCodec.CanonicalBlogCall) list)
        : Task<ContinuationOutcome> =
        let mainSessionId = ctx.Owner
        let providerRun = previousRun ctx
        let sessionKey = key ctx
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
            resumeCatchUp ctx mainSessionId sessionKey "idempotent-receipt-catch-up-complete"
        else
            EnforcerCycleDecode.validateCycle ctx.Previous.MessageId calls
            |> commitValidated ctx mainSessionId sessionKey providerRun

    /// An owned later step: judge the previous step of the live Host loop. A
    /// completed step without provider identity only closes the exact request
    /// (context-compression-025); no provider run is invented for it.
    let private ownedStep (ctx: Context) : Task<ContinuationOutcome> =
        let calls = EnforcerCycleDecode.callsOf Diagnostic.emit ctx.Previous
        let callCount = EnforcerRepair.chronicleCallCount ctx.Previous

        if ctx.Previous.Completed && String.IsNullOrWhiteSpace ctx.Previous.MessageId then
            abandonThenStop ctx (key ctx) ctx.Live "blog cycle has no provable provider run (ENFORCER-043)"
        elif callCount > 1 then
            invalidCardinalityStep ctx (key ctx) callCount
        elif List.isEmpty calls then
            emptyCallsStep ctx (key ctx)
        else
            commitBranch ctx calls

    let private ownershipLabel (ownership: BloggerTerminalRequestOwnership) =
        match ownership with
        | BloggerTerminalRequestOwnership.Current -> "current"
        | BloggerTerminalRequestOwnership.Superseded -> "superseded"
        | BloggerTerminalRequestOwnership.Unproven -> "unproven"

    /// A later step whose physical message is not proven to belong to the live
    /// request: no commit, no repair budget, no stop. The Host loop keeps its
    /// own transcript.
    let private notOwnedStep (bloggerSessionId: SessionId) (physical: PhysicalUserMessageId) (why: string) =
        Diagnostic.emit
            "enforcer-cycle-not-owned"
            [ "session_id", SessionId.value bloggerSessionId
              "result", why + " step of " + PhysicalUserMessageId.value physical ]

    /// The physical user message itself, as the Host transcript carries it.
    let private physicalMessage (physical: PhysicalUserMessageId) (rawMessages: obj list) : obj list =
        rawMessages
        |> List.tryFind (fun raw -> ProviderWireDecode.hostMessageId raw = Some(PhysicalUserMessageId.value physical))
        |> Option.toList

    /// First step of a physical message: nothing is judged. The step gets the
    /// live request's canonical view; a physical repair prompt scoped to that
    /// request stays visible after it, since that prompt is what the step answers.
    let private firstStepView
        (durable: AgentJournal)
        (bloggerSessionId: SessionId)
        (live: BloggerRequestContext)
        (physical: PhysicalUserMessageId)
        (rawMessages: obj list)
        : Task<obj list> =
        task {
            match! EnforcerFrameRecovery.tryRebuildFromContext durable bloggerSessionId live with
            | None -> return rawMessages
            | Some view when BloggerRecoveryProbe.isRequestScopedRepairPrompt durable bloggerSessionId live physical ->
                return view @ physicalMessage physical rawMessages
            | Some view -> return view
        }

    let private projectMessages (messages: obj list) (fallback: obj list) : ContinuationOutcome =
        ContinuationOutcome.ProjectMessages(if List.isEmpty messages then fallback else messages)

    let private stopPhysicalRun (messages: obj list) (reason: string) : ContinuationOutcome =
        ContinuationOutcome.StopPhysicalRun(messages, reason)

    let private isEmptyTextCycleFailure (reason: string) : bool =
        reason = EnforcerCycleDecode.EmptyTextError

    let private linkedMain (journal: AgentJournal option) (bloggerSessionId: SessionId) =
        journal
        |> Option.bind (fun durable ->
            SessionAssociationProjection.tryMainSessionOf
                bloggerSessionId
                (AgentJournal.snapshot durable).AgentProjections.Associations
            |> Option.map (fun owner -> durable, owner))

    /// The Blogger continuation-transform handler: decode the step position,
    /// prove ownership, then dispatch. It only derives the closed step context
    /// and forwards; the branches own the decisions.
    let handleContinuation
        (scope: IBloggerRuntimeHost)
        (journal: AgentJournal option)
        (bloggerSessionId: SessionId)
        (rawMessages: obj list)
        : Task<ContinuationOutcome> =
        task {
            let project (msgs: obj list) = projectMessages msgs rawMessages

            let stop (reason: string) = stopPhysicalRun rawMessages reason

            let liveCtx = EnforcerFrameRecovery.tryLiveCycleContext scope bloggerSessionId

            let ownedContext durable owner live previous : Context =
                { Scope = scope
                  Durable = durable
                  Owner = owner
                  BloggerSessionId = bloggerSessionId
                  Live = live
                  Previous = previous
                  RawMessages = rawMessages
                  Project = project
                  Stop = stop
                  RefreshMainContext = BloggerMainContext.fromJournal scope durable
                  IsEmptyTextCycleFailure = isEmptyTextCycleFailure }

            let ownership durable live physical =
                BloggerRecoveryProbe.terminalRequestOwnershipForPhysicalMessage durable bloggerSessionId live physical

            match linkedMain journal bloggerSessionId, liveCtx, EnforcerCycleDecode.stepPosition rawMessages with
            | Some(durable, _), Some live, EnforcerCycleDecode.StepPosition.First physical ->
                let! view = firstStepView durable bloggerSessionId live physical rawMessages
                return project view
            | Some(durable, owner), Some live, EnforcerCycleDecode.StepPosition.After(physical, previous) when
                ownership durable live physical = BloggerTerminalRequestOwnership.Current
                ->
                return! ownedStep (ownedContext durable owner live previous)
            | Some(durable, _), Some live, EnforcerCycleDecode.StepPosition.After(physical, _) ->
                notOwnedStep bloggerSessionId physical (ownershipLabel (ownership durable live physical))
                return project rawMessages
            | Some _, None, EnforcerCycleDecode.StepPosition.After(physical, _) ->
                notOwnedStep bloggerSessionId physical "no live request"
                return project rawMessages
            | _ -> return project rawMessages
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

    /// The stop decision's binding act: retire the exact in-flight step and the
    /// physical execution custody BEFORE the physical abort resolves. The next
    /// provider admission for this execution is fenced out from this instant;
    /// a slow, rejected, or thrown abort can never reopen it.
    let internal barPhysicalProviderAdmission (sid: SessionId) (physicalUserMessageId: PhysicalUserMessageId) =
        ModelRouting.suppressProviderStep sid physicalUserMessageId
        ModelRouting.releasePhysicalExecution sid physicalUserMessageId |> ignore

    /// Detached Host-side abort requested only after the admission barrier is
    /// up. Its outcome is diagnostic evidence; it carries no authority over
    /// whether the execution may run again.
    /// The real awaited call — separate function so the outer `task` never
    /// carries a nested match under a `try` guard.
    let private awaitPhysicalStop
        (terminateSession: SessionTermination)
        (sid: SessionId)
        (sessionId: string)
        (reason: string)
        : Task =
        task {
            match! terminateSession sid reason with
            | Ok() -> ()
            | Error error ->
                Diagnostic.emit
                    "enforcer-stop-physical-run"
                    [ "session_id", sessionId; "result", "abort-error: " + error ]
        }

    let private requestPhysicalStop
        (terminateSession: SessionTermination)
        (sid: SessionId)
        (sessionId: string)
        (reason: string)
        =
        task {
            try
                return! awaitPhysicalStop terminateSession sid sessionId reason
            with ex ->
                Diagnostic.emit
                    "enforcer-stop-physical-run"
                    [ "session_id", sessionId; "result", "abort-exception: " + ex.Message ]
        }
        |> ignore

    /// Stop ordering: suppress + retire the exact physical execution first,
    /// then issue the physical abort as an independent observation.
    let internal applyPhysicalStop
        (terminateSession: SessionTermination)
        (sid: SessionId)
        (sessionId: string)
        (physicalUserMessageId: PhysicalUserMessageId option)
        (reason: string)
        =
        physicalUserMessageId |> Option.iter (barPhysicalProviderAdmission sid)
        requestPhysicalStop terminateSession sid sessionId reason

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

                applyPhysicalStop
                    terminateSession
                    sid
                    sessionId
                    (ProviderWireCapture.lastUserMessageId bloggerMessages)
                    reason
        }

    /// Enter the exact current-process Blogger continuation directly. The
    /// generic family recovery gate is deleted; Waiting is never treated as
    /// Ready.
    let private runEnforcerContinuation
        (scope: IBloggerRuntimeHost)
        (journal: AgentJournal option)
        (terminateSession: SessionTermination)
        (sid: SessionId)
        (sessionId: string)
        (outObj: obj)
        : Task =
        task {
            let bloggerMessages = unbox<obj array> outObj?messages |> Array.toList

            let! outcome = handleContinuation scope journal sid bloggerMessages

            do! applyContinuationOutcome terminateSession sid sessionId bloggerMessages outObj outcome
        }

    let private runEnforcerIfMainAssociated
        (scope: IBloggerRuntimeHost)
        (journal: AgentJournal option)
        (durable: AgentJournal)
        (terminateSession: SessionTermination)
        (sid: SessionId)
        (sessionId: string)
        (outObj: obj)
        : Task =
        let associations = (AgentJournal.snapshot durable).AgentProjections.Associations

        match SessionAssociationProjection.tryMainSessionOf sid associations with
        | Some _ -> runEnforcerContinuation scope journal terminateSession sid sessionId outObj
        | None -> Task.FromResult()

    let applyContinuation
        (scope: IBloggerRuntimeHost)
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
