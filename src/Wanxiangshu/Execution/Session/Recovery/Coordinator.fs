namespace Wanxiangshu.Execution.Session.Recovery

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Execution.Session.Recovery.SessionRecovery
open Wanxiangshu.Foundation.Identity

/// Physical single-flight decorator for family recovery (rabbit §14.3).
/// Owns only root → Task sharing; does not know how recovery is performed.
module FamilyRecoveryCoordinator =

    let private gate = obj ()
    // DSL-MUTABLE: single-flight — in-flight recovery task by root key
    let private inflight = Dictionary<string, Task<FamilyRecovery>>()

    let private startRecovery (recover: SessionId -> Task<FamilyRecovery>) (root: SessionId) key =
        task {
            try
                return! recover root
            finally
                lock gate (fun () -> inflight.Remove key |> ignore)
        }

    /// Run `recover` at most once per root while in flight; concurrent callers share the task.
    let runOnce (recover: SessionId -> Task<FamilyRecovery>) (root: SessionId) : Task<FamilyRecovery> =
        let key = SessionId.value root

        lock gate (fun () ->
            match inflight.TryGetValue key with
            | true, existing -> existing
            | false, _ ->
                let started = startRecovery recover root key
                inflight.[key] <- started
                started)
