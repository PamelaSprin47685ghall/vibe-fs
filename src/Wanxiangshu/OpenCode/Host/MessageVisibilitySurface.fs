namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// JS-native boundary for MessageVisibilityHub (HOST-BOUNDARY-008): tests drive
/// a duck-typed ITimerPort and observe wake / deadline-backstop / waiter-leak
/// semantics. The hub and the F# waiter registry never cross this edge.
module MessageVisibilitySurface =

    type private HubHandle(hub: MessageVisibilityHub) =
        member _.Hub = hub

    let create (timerPort: obj) : obj =
        HubHandle(MessageVisibilityHub(unbox<ITimerPort> timerPort)) :> obj

    let notify (handle: obj) (sessionId: string) : unit =
        (handle :?> HubHandle).Hub.Notify(SessionId.create sessionId)

    let awaitChange (handle: obj) (sessionId: string) (budgetMilliseconds: int) : Task<unit> =
        (handle :?> HubHandle).Hub.AwaitChange (SessionId.create sessionId) budgetMilliseconds

    let pendingCount (handle: obj) (sessionId: string) : int =
        (handle :?> HubHandle).Hub.PendingCount(SessionId.create sessionId)
