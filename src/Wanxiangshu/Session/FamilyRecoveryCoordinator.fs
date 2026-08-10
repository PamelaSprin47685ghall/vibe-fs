namespace Wanxiangshu.Session

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Domain.SessionRecovery
open Wanxiangshu.Kernel.Identity

/// Physical single-flight decorator for family recovery (rabbit §14.3).
/// Owns only root → Task sharing; does not know how recovery is performed.
module FamilyRecoveryCoordinator =

    let private gate = obj ()
    let private inflight = Dictionary<string, Task<FamilyRecovery>>()

    /// Run `recover` at most once per root while in flight; concurrent callers share the task.
    let runOnce (recover: SessionId -> Task<FamilyRecovery>) (root: SessionId) : Task<FamilyRecovery> =
        let key = SessionId.value root

        lock gate (fun () ->
            match inflight.TryGetValue key with
            | true, existing -> existing
            | false, _ ->
                let started =
                    task {
                        try
                            return! recover root
                        finally
                            lock gate (fun () -> inflight.Remove key |> ignore)
                    }

                inflight.[key] <- started
                started)
