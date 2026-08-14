namespace Wanxiangshu.Execution.Session.Recovery

open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Execution.Session.Recovery.SessionRecovery
open Wanxiangshu.Foundation.Identity

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
