namespace Wanxiangshu.Change.Host

open System.Collections.Generic
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Foundation.Identity

module OrchestratorSessionDirectories =
    val registerRestored:
        handles: AgentLinkageProjection option ->
        worktrees: Dictionary<string, string> ->
        register: (SessionId -> string -> unit) ->
            unit
