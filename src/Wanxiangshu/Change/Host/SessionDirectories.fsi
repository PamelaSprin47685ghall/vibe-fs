namespace Wanxiangshu.Change.Host

open System.Collections.Generic
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Review

module OrchestratorSessionDirectories =
    val registerRestored:
        snapshot: ProjectionSet ->
        orchestratorId: SessionId ->
        worktrees: Dictionary<string, string> ->
        register: (SessionId -> string -> unit) ->
        registerReviewerTree: (string -> GitTreePort -> unit) ->
            unit
