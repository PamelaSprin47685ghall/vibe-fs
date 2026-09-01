namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

type MessageVisibilityHub =
    new: timerPort: ITimerPort -> MessageVisibilityHub
    member Notify: sessionId: SessionId -> unit
    member AwaitChange: sessionId: SessionId -> budgetMilliseconds: int -> Task<unit>
    member PendingCount: sessionId: SessionId -> int

[<RequireQualifiedAccess>]
module MessageVisibilitySignal =
    val observeEvent: hub: MessageVisibilityHub -> rawInput: obj -> unit
