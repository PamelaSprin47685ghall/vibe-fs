namespace Wanxiangshu.Mission.Obligation.Todo

open Wanxiangshu.OpenCode

/// Pure provider-input materialization owner. Snapshot/resource operations are
/// intentionally absent; those belong to MagicTodoMembraneSurface.
[<RequireQualifiedAccess>]
module MagicTodoLocalitySurface =

    val materializeInput: callId: string -> inputCanonical: string -> state: obj -> expectedCanonical: string -> obj
