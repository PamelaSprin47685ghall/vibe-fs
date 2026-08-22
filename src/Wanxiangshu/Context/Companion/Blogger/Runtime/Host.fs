namespace Wanxiangshu.Context.Companion.Blogger.Runtime

open System.Threading
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
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

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// Shared Host-side side effects around Blogger physical slots (flight + drain).
/// Pure routing stays in BloggerRuntime; material CE stays in BloggerCoordinator;
/// continuation CE stays in EnforcerHost. Seal/block recipes are not duplicated.
module BloggerRuntimeHost =

    [<RequireQualifiedAccess>]
    type ProducerSettlement =
        | NoOpenProducer
        | Committed
        | Abandoned
        | Cancelled

    let private bloggerOfMain (snapshot: ProjectionSet) (mainSessionId: SessionId) =
        SessionAssociationProjection.tryBloggerOf mainSessionId snapshot.AgentProjections.Associations

    let private cycleStateOfMain (snapshot: ProjectionSet) (mainSessionId: SessionId) =
        AgentProjection.tryFind mainSessionId snapshot.AgentProjections
        |> Option.bind (fun session -> session.BloggerCycles)

    let private tryOpenProducer
        (snapshot: ProjectionSet)
        (mainSessionId: SessionId)
        : (SessionId * BloggerRequestId) option =
        bloggerOfMain snapshot mainSessionId
        |> Option.bind (fun bloggerSessionId ->
            cycleStateOfMain snapshot mainSessionId
            |> Option.bind (BloggerCycleProjection.tryOpenByBlogger bloggerSessionId)
            |> Option.map (fun request -> bloggerSessionId, request.RequestId))

    let private producerSettlement
        (snapshot: ProjectionSet)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        (requestId: BloggerRequestId)
        : ProducerSettlement option =
        match cycleStateOfMain snapshot mainSessionId with
        | Some cycles when Map.containsKey requestId cycles.ProviderRunByRequestId -> Some ProducerSettlement.Committed
        | Some cycles when
            BloggerCycleProjection.tryOpenByBlogger bloggerSessionId cycles
            |> Option.exists (fun request -> request.RequestId = requestId)
            ->
            None
        | _ -> Some ProducerSettlement.Abandoned

    let private awaitProducerChange
        (cancellation: CancellationToken)
        (journal: AgentJournal)
        revision
        (continueWaiting: unit -> System.Threading.Tasks.Task<ProducerSettlement>)
        : System.Threading.Tasks.Task<ProducerSettlement> =
        task {
            match! AgentJournal.awaitChangeFromOrCancel revision cancellation journal with
            | None -> return ProducerSettlement.Cancelled
            | Some _ -> return! continueWaiting ()
        }

    let rec private awaitProducerSettlement
        (cancellation: CancellationToken)
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        (requestId: BloggerRequestId)
        : System.Threading.Tasks.Task<ProducerSettlement> =
        task {
            let snapshot, revision = AgentJournal.snapshotWithRevision journal

            match producerSettlement snapshot mainSessionId bloggerSessionId requestId with
            | Some settlement -> return settlement
            | None ->
                return!
                    awaitProducerChange cancellation journal revision (fun () ->
                        awaitProducerSettlement cancellation journal mainSessionId bloggerSessionId requestId)
        }

    /// Await only a producer that is already durable-open at the observation
    /// point. The request id is frozen before waiting, so a later Blogger request
    /// cannot accidentally satisfy this barrier. Journal revision is the sole
    /// wake source; no clock, polling, flight map, or pending-offer state proves
    /// settlement.
    let awaitOpenProducerSettlement
        (cancellation: CancellationToken)
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        : System.Threading.Tasks.Task<ProducerSettlement> =
        let snapshot = AgentJournal.snapshot journal

        match tryOpenProducer snapshot mainSessionId with
        | None -> System.Threading.Tasks.Task.FromResult ProducerSettlement.NoOpenProducer
        | Some(bloggerSessionId, requestId) ->
            awaitProducerSettlement cancellation journal mainSessionId bloggerSessionId requestId

    let claimCurrentRequest
        (scope: IBloggerRuntimeHost)
        (bloggerKey: string)
        (context: BloggerRequestContext)
        : Result<unit, string> =
        match scope.ClaimCurrentRequest(bloggerKey, context) with
        | BloggerFlightClaim.Claimed
        | BloggerFlightClaim.Refreshed -> Ok()
        | BloggerFlightClaim.Conflict existing ->
            Error(
                sprintf
                    "Blogger flight %s already belongs to request %s; cannot claim request %s"
                    bloggerKey
                    (BloggerRequestId.value existing)
                    (BloggerRequestId.value (BloggerRequestContext.requestId context))
            )

    let requireCurrentRequest
        (scope: IBloggerRuntimeHost)
        (bloggerKey: string)
        (context: BloggerRequestContext)
        : unit =
        match claimCurrentRequest scope bloggerKey context with
        | Ok() -> ()
        | Error reason -> FatalProcess.trip "blogger-flight-claim-conflict" reason

    let releaseCurrentRequest
        (scope: IBloggerRuntimeHost)
        (bloggerKey: string)
        (context: BloggerRequestContext)
        : Result<unit, string> =
        let requestId = BloggerRequestContext.requestId context

        match scope.ReleaseCurrentRequest(bloggerKey, requestId) with
        | BloggerFlightRelease.Released
        | BloggerFlightRelease.Missing -> Ok()
        | BloggerFlightRelease.Conflict existing ->
            Error(
                sprintf
                    "Blogger flight %s belongs to request %s; cannot release as request %s"
                    bloggerKey
                    (BloggerRequestId.value existing)
                    (BloggerRequestId.value requestId)
            )

    let requireReleaseCurrentRequest
        (scope: IBloggerRuntimeHost)
        (bloggerKey: string)
        (context: BloggerRequestContext)
        : unit =
        match releaseCurrentRequest scope bloggerKey context with
        | Ok() -> ()
        | Error reason -> FatalProcess.trip "blogger-flight-release-conflict" reason

    let requireReleaseObservedCurrentRequest (scope: IBloggerRuntimeHost) (bloggerKey: string) : unit =
        match scope.TryPeekCurrentRequest bloggerKey with
        | None -> ()
        | Some context -> requireReleaseCurrentRequest scope bloggerKey context

    let durableSealed (journal: AgentJournal option) (mainSessionId: SessionId) : bool =
        match journal with
        | None -> false
        | Some j -> AgentProjection.mainSealedForBlogger mainSessionId (AgentJournal.snapshot j).AgentProjections

    /// Durable handle sealed + drain closed → block new Y work.
    /// Busy uses physical flight ownership (`HasFlight`).
    /// Drain uses the physical drain slot (`IsDrainOpen`).
    let blocksNew
        (journal: AgentJournal option)
        (mainSessionId: SessionId)
        (scope: IBloggerRuntimeHost)
        (bloggerKey: string)
        : bool =
        BloggerRuntime.blocksNewRequest
            (durableSealed journal mainSessionId)
            (scope.HasFlight bloggerKey)
            (scope.IsDrainOpen bloggerKey)

    /// Close drain + drop PendingOffer + cancel park waiter.
    /// Existing physical flight ownership survives until its terminal owner clears it.
    let forceSealRuntime (scope: IBloggerRuntimeHost) (bloggerKey: string) : unit =
        scope.SetDrainWindow(bloggerKey, DrainWindow.Closed)
        scope.TryTakePendingOffer bloggerKey |> ignore
        scope.CancelParked bloggerKey

    /// Close drain + drop pending offer (keep CurrentRequest until caller clears).
    let forceSealCellDropOffer (scope: IBloggerRuntimeHost) (bloggerKey: string) : unit =
        scope.SetDrainWindow(bloggerKey, DrainWindow.Closed)
        scope.TryTakePendingOffer bloggerKey |> ignore

    /// New Authority Root: reopen the drain window for next material. The root
    /// identity is recorded on the window so a stale reactivation (an older root
    /// arriving after a newer seal) cannot reopen a window it no longer owns.
    let reactivateAfterNewRoot
        (scope: IBloggerRuntimeHost)
        (bloggerSessionId: SessionId)
        (root: AuthorityRootUserMessageId)
        : unit =
        let key = SessionId.value bloggerSessionId
        scope.SetDrainWindow(key, BloggerRuntime.openDrain root)
