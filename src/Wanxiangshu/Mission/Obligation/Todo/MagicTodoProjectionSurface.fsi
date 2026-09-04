namespace Wanxiangshu.Mission.Obligation.Todo

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoProjection
open Wanxiangshu.Mission.Relay

type MagicTodoProjectionHandle

[<RequireQualifiedAccess>]
module MagicTodoProjectionSurface =
    val internal incumbencyView: state: MagicTodoProjectionState -> incumbencyId: IncumbencyId -> obj
    val create: unit -> MagicTodoProjectionHandle
    val fold: handle: MagicTodoProjectionHandle -> eventId: string -> factJson: string -> obj
    val view: handle: MagicTodoProjectionHandle -> incumbencyId: string -> obj
