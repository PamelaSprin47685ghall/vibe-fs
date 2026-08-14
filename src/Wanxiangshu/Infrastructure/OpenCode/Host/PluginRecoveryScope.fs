namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Execution.Session
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Domain.SessionRecovery
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// Family recovery coordination (PROMPT-011 + C5 + RECOVERY-FAMILY) and
/// attempt planning state for one plugin instance: recovery ports attachment,
/// per-session arming and per-provider-run attempt plans.
type PluginRecoveryScope() =
    /// Family recovery coordinator ports (PROMPT-011 + C5 + RECOVERY-FAMILY).
    ///
    /// Attached after `createHost`. First business entry point runs
    /// SessionRecoveryWorkflow.recoverFamilyDirect under FamilyRecoveryCoordinator
    /// single-flight; later callers await the same task.
    // DSL-MUTABLE: resource — family recovery ports attachment slot
    let mutable familyRecoveryPorts: SessionRecoveryWorkflow.Ports option = None
    let recoveryGateLock = obj ()

    member val RecoveryArming = Dictionary<string, SlotArming>()
    member val AttemptPlans = Dictionary<string, AttemptPlan>()

    member this.AttachFamilyRecoveryPorts(ports: SessionRecoveryWorkflow.Ports) =
        lock recoveryGateLock (fun () -> familyRecoveryPorts <- Some ports)

    /// RECOVERY-FAMILY: obtain FamilyRecovery for a parent before business work.
    /// Missing ports → FamilyBlocked (fail closed). Never synthetic FamilyReady.
    member this.RequireFamilyRecovery(root: SessionId) : Task<FamilyRecovery> =
        task {
            match lock recoveryGateLock (fun () -> familyRecoveryPorts) with
            | None ->
                return FamilyRecovery.FamilyBlocked(NonEmpty.one (RecoveryBlock.RecoveryCoordinatorUnavailable root))
            | Some ports ->
                return! FamilyRecoveryCoordinator.runOnce (SessionRecoveryWorkflow.recoverFamilyDirect ports) root
        }

    /// Await family recovery before business effects. Returns FamilyRecovery so
    /// callers must match FamilyBlocked (P0-RECOVERY-JOIN-001: no collapse to unit).
    member this.EnsureRecoveryDone(root: SessionId) : Task<FamilyRecovery> = this.RequireFamilyRecovery root

    member this.ArmRecovery(sessionId: SessionId) =
        this.RecoveryArming.[SessionId.value sessionId] <- RecoverySlot.afterFailureAdvance

    member this.TryRecoveryArming(sessionId: SessionId) =
        match this.RecoveryArming.TryGetValue(SessionId.value sessionId) with
        | true, arming -> Some arming
        | false, _ -> None

    member this.RecordAttemptPlan (sessionId: SessionId) (providerRun: ProviderRunIdentity) (plan: AttemptPlan) =
        this.AttemptPlans.[SessionId.value sessionId + "\u001f" + ProviderRunIdentity.value providerRun] <- plan

    member this.TryAttemptPlan (sessionId: SessionId) (providerRun: ProviderRunIdentity) =
        let key =
            SessionId.value sessionId + "\u001f" + ProviderRunIdentity.value providerRun

        match this.AttemptPlans.TryGetValue(key) with
        | true, plan -> Some plan
        | false, _ -> None

    /// Session deletion drops arming and attempt plans for this session.
    member this.ClearSession(sessionId: string) =
        this.RecoveryArming.Remove sessionId |> ignore
        this.ClearAttemptPlansFor sessionId

    /// One-shot arming consumption (FALLBACK-012): drops arming only; attempt
    /// plans stay until the turn resolves them.
    member this.ClearRecovery(sessionId: string) =
        this.RecoveryArming.Remove sessionId |> ignore

    /// Drops attempt plans whose key prefix matches (used for a session and
    /// for its linked Blogger keys during session deletion).
    member this.ClearAttemptPlansFor(key: string) =
        this.AttemptPlans.Keys
        |> Seq.filter (fun planKey -> planKey.StartsWith(key + "\u001f", StringComparison.Ordinal))
        |> Seq.toList
        |> List.iter (fun planKey -> this.AttemptPlans.Remove planKey |> ignore)
