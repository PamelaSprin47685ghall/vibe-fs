namespace Wanxiangshu.Mission.Obligation.Todo

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoProjection

type MagicTodoProjectionHandle

[<RequireQualifiedAccess>]
module MagicTodoProjectionSurface =
    val internal lifeView: state: MagicTodoProjectionState -> lifeId: ManagerLifeId -> obj
    val create: unit -> MagicTodoProjectionHandle
    val fold: handle: MagicTodoProjectionHandle -> eventId: string -> factJson: string -> obj
    val view: handle: MagicTodoProjectionHandle -> lifeId: string -> obj
