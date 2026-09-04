namespace Wanxiangshu.Change.Host

open System.Collections.Generic
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Foundation.Identity

module OrchestratorSessionDirectories =
    val registerRestored:
        snapshot: ProjectionSet ->
        orchestratorId: SessionId ->
        worktrees: Dictionary<string, string> ->
        register: (SessionId -> string -> unit) ->
            unit
