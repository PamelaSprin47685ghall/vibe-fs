namespace Wanxiangshu.Mission.Review

open Wanxiangshu.Mission.Obligation.Todo

/// Review-owned Magic Todo surface for integration tests.
[<RequireQualifiedAccess>]
module ReviewTodoSurface =

    val factJson: caseName: string -> payload: obj -> obj

    val ids: sha256: obj -> lifeId: string -> callId: string -> obj

    val newProjection: unit -> MagicTodoProjectionHandle

    val fold: projection: MagicTodoProjectionHandle -> eventId: string -> caseName: string -> payload: obj -> obj

    val view: projection: MagicTodoProjectionHandle -> lifeId: string -> obj
