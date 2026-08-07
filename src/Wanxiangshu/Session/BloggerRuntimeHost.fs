namespace Wanxiangshu.Session

open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// Shared Host-side side effects around Blogger physical slots (flight + drain).
/// Pure routing stays in BloggerRuntime; material CE stays in BloggerCoordinator;
/// continuation CE stays in EnforcerHost. Seal/block recipes are not duplicated.
module BloggerRuntimeHost =

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
        (scope: IParkedTransformHost)
        (bloggerKey: string)
        : bool =
        BloggerRuntime.blocksNewRequest
            (durableSealed journal mainSessionId)
            (scope.HasFlight bloggerKey)
            (scope.IsDrainOpen bloggerKey)

    /// Close drain + clear CurrentRequest/PendingOffer + cancel park waiter.
    let forceSealRuntime (scope: IParkedTransformHost) (bloggerKey: string) : unit =
        scope.SetDrainWindow(bloggerKey, DrainWindow.Closed)
        scope.ClearCurrentRequest bloggerKey
        scope.TryTakePendingOffer bloggerKey |> ignore
        scope.CancelParked bloggerKey

    /// Close drain + drop pending offer (keep CurrentRequest until caller clears).
    let forceSealCellDropOffer (scope: IParkedTransformHost) (bloggerKey: string) : unit =
        scope.SetDrainWindow(bloggerKey, DrainWindow.Closed)
        scope.TryTakePendingOffer bloggerKey |> ignore

    /// New Authority Root: reopen the drain window for next material. The root
    /// identity is recorded on the window so a stale reactivation (an older root
    /// arriving after a newer seal) cannot reopen a window it no longer owns.
    let reactivateAfterNewRoot
        (scope: IParkedTransformHost)
        (bloggerSessionId: SessionId)
        (root: AuthorityRootUserMessageId)
        : unit =
        let key = SessionId.value bloggerSessionId
        scope.SetDrainWindow(key, BloggerRuntime.openDrain root)
