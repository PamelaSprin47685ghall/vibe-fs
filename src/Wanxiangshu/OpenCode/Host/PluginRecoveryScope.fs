namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Execution.Session.Recovery.SessionRecovery
open Wanxiangshu.Foundation.Identity
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
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open Wanxiangshu.Execution.Session
open Wanxiangshu.Persistence.Journal

/// Typed one-shot recovery arming permit — opaque physical capability, not a queryable flag.
/// Created by the observation adapter (HostTurnObserver.ArmRecovery) after a turn fails,
/// consumed exactly once by the owning recovery CE (XWire.applyNonReplicaTransform).
/// Process-local only: new instance starts empty => None (PAR-011 fail-closed, SW-009).
/// Host callbacks are rendezvous/observation adapters; presence does not drive business
/// branching outside the owning CE (SW-017②). The permit is the CE's internal control-flow
/// fact, not a durable PC.
type private RecoveryArmingPermit =
    { SessionId: SessionId
      Arming: SlotArming }

/// Typed frozen attempt plan handle — transform's frozen decision, consumed by reconciliation.
/// Cannot be recomputed because projection may advance between transform and reconciliation.
/// Single-flight typed capability: TryTake consumes exactly once.
type private AttemptPlanHandle =
    { SessionId: SessionId
      ProviderRun: ProviderRunIdentity
      Plan: AttemptPlan }

/// Family recovery coordination (PROMPT-011 + C5 + RECOVERY-FAMILY) and
/// attempt planning state for one plugin instance: recovery ports attachment,
/// per-session arming and per-provider-run attempt plans.
///
/// Owning recovery CE holds the permits internally; Host callbacks are only
/// rendezvous/observation adapters that deliver typed observations. Physical
/// identity (SessionId / ProviderRunIdentity) is the typed capability key;
/// no stringly-typed TryGet/Clear drives business branching (SW-017, SW-009, PAR-011).
type PluginRecoveryScope(journal: AgentJournal option) =

    // Owning CE internal single-flight channels — process-local, crash-zero.
    // DSL-MUTABLE: single-flight — per-session one-shot recovery arming permit channel (typed capability)
    let recoveryArming = Dictionary<string, SlotArming>()

    // DSL-MUTABLE: single-flight — per-provider-run attempt plan channel (frozen decision, typed handle)
    let attemptPlans = Dictionary<string, AttemptPlan>()

    /// Ordinary business entry never performs cross-process recovery. The permit
    /// only certifies this process's join attempt and intentionally carries no old
    /// durable closure members. Explicit session /continue owns future resume.
    member _.RequireFamilyRecovery(root: SessionId) : Task<FamilyRecovery> =
        let sequence =
            journal
            |> Option.map (AgentJournal.revision >> JournalRevision.value)
            |> Option.defaultValue 0L

        Task.FromResult(FamilyRecovery.FamilyReady(FamilyRecoveryPermit.currentProcess root sequence))

    member this.EnsureRecoveryDone(root: SessionId) : Task<FamilyRecovery> = this.RequireFamilyRecovery root

    member this.ArmRecovery(sessionId: SessionId) =
        recoveryArming.[SessionId.value sessionId] <- RecoverySlot.afterFailureAdvance

    /// Owning recovery CE consumes the arming exactly once. Returns Some arming
    /// only on first consume; subsequent call returns None (idempotent consume).
    /// Process-local crash-zero: new instance has empty map => None (PAR-011).
    member this.TryTakeRecoveryPermit(sessionId: SessionId) : SlotArming option =
        let key = SessionId.value sessionId

        match recoveryArming.TryGetValue key with
        | true, arming ->
            recoveryArming.Remove key |> ignore
            Some arming
        | false, _ -> None

    /// Legacy peek — retained for compatibility but must not drive business branching.
    /// New code must use TryTakeRecoveryPermit.
    member this.TryRecoveryArming(sessionId: SessionId) =
        match recoveryArming.TryGetValue(SessionId.value sessionId) with
        | true, arming -> Some arming
        | false, _ -> None

    member this.RecordAttemptPlan (sessionId: SessionId) (providerRun: ProviderRunIdentity) (plan: AttemptPlan) =
        attemptPlans.[SessionId.value sessionId + "\u001f" + ProviderRunIdentity.value providerRun] <- plan

    /// Owning CE consumes the frozen plan exactly once on terminal reconciliation.
    /// Returns None if already consumed or never recorded.
    member this.TryTakeAttemptPlan (sessionId: SessionId) (providerRun: ProviderRunIdentity) : AttemptPlan option =
        let key =
            SessionId.value sessionId + "\u001f" + ProviderRunIdentity.value providerRun

        match attemptPlans.TryGetValue key with
        | true, plan ->
            attemptPlans.Remove key |> ignore
            Some plan
        | false, _ -> None

    /// Read-only peek for Strength evidence — not for recovery branching.
    member this.TryPeekAttemptPlan (sessionId: SessionId) (providerRun: ProviderRunIdentity) =
        let key =
            SessionId.value sessionId + "\u001f" + ProviderRunIdentity.value providerRun

        match attemptPlans.TryGetValue key with
        | true, plan -> Some plan
        | false, _ -> None

    member this.TryAttemptPlan (sessionId: SessionId) (providerRun: ProviderRunIdentity) =
        this.TryPeekAttemptPlan sessionId providerRun

    /// Session deletion drops arming and attempt plans for this session.
    member this.ClearSession(sessionId: string) =
        recoveryArming.Remove sessionId |> ignore
        this.ClearAttemptPlansFor sessionId

    /// One-shot arming consumption (FALLBACK-012): drops arming only; attempt
    /// plans stay until the turn resolves them. Prefer TryTakeRecoveryPermit.
    member this.ClearRecovery(sessionId: string) =
        recoveryArming.Remove sessionId |> ignore

    /// Re-arm for the durable NoCoverage case — blog frames catching up (PAR-011, SW-009).
    member this.ReArmRecovery(sessionId: SessionId) =
        recoveryArming.[SessionId.value sessionId] <- RecoverySlot.afterFailureAdvance

    /// Drops attempt plans whose key prefix matches (used for a session and
    /// for its linked Blogger keys during session deletion). Prefer TryTakeAttemptPlan.
    member this.ClearAttemptPlansFor(key: string) =
        attemptPlans.Keys
        |> Seq.filter (fun planKey -> planKey.StartsWith(key + "\u001f", StringComparison.Ordinal))
        |> Seq.toList
        |> List.iter (fun planKey -> attemptPlans.Remove planKey |> ignore)
