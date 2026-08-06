namespace Wanxiangshu.Session

open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// Shared Host-side side effects around BloggerRuntime cells (P1-1).
/// Pure transitions stay in BloggerRuntime; material CE stays in BloggerCoordinator;
/// continuation CE stays in EnforcerHost. Seal/block recipes are not duplicated.
module BloggerRuntimeHost =

    let durableSealed (journal: AgentJournal option) (mainSessionId: SessionId) : bool =
        match journal with
        | None -> false
        | Some j -> AgentProjection.mainSealedForBlogger mainSessionId (AgentJournal.snapshot j).AgentProjections

    /// Durable handle sealed + cell not in drain window → block new Y work.
    let blocksNew
        (journal: AgentJournal option)
        (mainSessionId: SessionId)
        (scope: IParkedTransformHost)
        (bloggerKey: string)
        : bool =
        let cell = scope.GetBloggerRuntime bloggerKey
        BloggerRuntime.blocksNewRequest (durableSealed journal mainSessionId) cell

    /// Force sealed cell + clear CurrentRequest/PendingOffer + cancel park waiter.
    let forceSealRuntime (scope: IParkedTransformHost) (bloggerKey: string) : unit =
        scope.SetBloggerRuntime(bloggerKey, BloggerRuntime.forceSeal (scope.GetBloggerRuntime bloggerKey))
        scope.ClearCurrentRequest bloggerKey
        scope.TryTakePendingOffer bloggerKey |> ignore
        scope.CancelParked bloggerKey

    /// Seal only the cell + drop pending offer (keep CurrentRequest until caller clears).
    let forceSealCellDropOffer (scope: IParkedTransformHost) (bloggerKey: string) : unit =
        scope.SetBloggerRuntime(bloggerKey, BloggerRuntime.forceSeal (scope.GetBloggerRuntime bloggerKey))
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
        let cell = scope.GetBloggerRuntime key

        scope.SetBloggerRuntime(key, BloggerRuntime.onReactivate cell root)
