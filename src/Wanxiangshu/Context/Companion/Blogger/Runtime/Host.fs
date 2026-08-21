namespace Wanxiangshu.Context.Companion.Blogger.Runtime

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
