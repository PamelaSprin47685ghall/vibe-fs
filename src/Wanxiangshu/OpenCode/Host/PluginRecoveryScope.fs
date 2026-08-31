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
open Wanxiangshu.Execution.Session.ChatExecution
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
/// Created only after PromptIngress durably accepts the exact ProviderRetryAttempt
/// physical user message, then consumed exactly once by the owning recovery CE
/// (XWire.applyNonReplicaTransform).
/// Process-local only: new instance starts empty => None (PAR-011 fail-closed, SW-009).
/// Host callbacks are rendezvous/observation adapters; presence does not drive business
/// branching outside the owning CE (SW-017②). The permit is the CE's internal control-flow
/// fact, not a durable PC.
type private RecoveryArmingPermit =
    { SessionId: SessionId
      PhysicalUserMessageId: PhysicalUserMessageId
      Arming: SlotArming }

/// Typed frozen attempt plan handle — transform's frozen decision, consumed by reconciliation.
/// Cannot be recomputed because projection may advance between transform and reconciliation.
/// Single-flight typed capability: TryTake consumes exactly once.
type private AttemptPlanHandle =
    { SessionId: SessionId
      ProviderRun: ProviderRunIdentity
      Plan: AttemptPlan }

/// Pre-inference purpose decision awaiting the Host-created assistant run bound
/// at transform admission. Keyed by the exact physical user message so later
/// tool/reconcile observations replay the same plan without guessing.
type private PendingAttemptPlanHandle =
    { SessionId: SessionId
      PhysicalUserMessageId: PhysicalUserMessageId
      Plan: PendingAttemptPlan }

[<RequireQualifiedAccess>]
type TransformAttemptPlanBindingError =
    | PendingAttemptPlanMissing of SessionId * PhysicalUserMessageId * ProviderRunIdentity

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
    /// DSL-cross-callback-proof: physical single-flight — opaque one-shot recovery arming permit channel.
    /// Owning recovery CE (XWire.applyNonReplicaTransform) consumes via TryTakeRecoveryPermit;
    /// PromptIngress acceptance only arms via ArmRecovery.
    /// No stringly-typed TryGet/Clear drives business branching (SW-017②, SW-009, PAR-011).
    // DSL-MUTABLE: single-flight — per-session one-shot recovery arming permit channel (typed capability)
    let recoveryArming = Dictionary<string, RecoveryArmingPermit>()

    /// Pre-inference attempt plans, keyed by exact physical user identity. They
    /// become ordinary provider-run keyed AttemptPlans only after Host exposes
    /// the assistant run.
    /// DSL-cross-callback-proof: physical single-flight — transform freezes one
    /// immutable plan under exact (SessionId, PhysicalUserMessageId); the first
    /// later Host observation carrying that same physical parent plus the exact
    /// ProviderRunIdentity consumes it and rekeys it to the provider-run channel.
    /// Mismatched physical material cannot probe/clear it; session deletion is
    /// the only non-bind cleanup. No durable workflow PC is reconstructed here.
    /// DSL-cross-callback-proof: physical
    // DSL-MUTABLE: single-flight — pre-inference attempt plan awaiting exact provider run binding
    let pendingAttemptPlans = Dictionary<string, PendingAttemptPlan>()

    /// DSL-cross-callback-proof: physical single-flight — opaque frozen attempt plan channel.
    /// Owning recovery CE (XWire.reconcileAttempt) consumes via ConsumeAttemptPlan on terminal;
    /// transform adapter records via RecordAttemptPlan; Strength peeks via TryAttemptPlan.
    /// No stringly-typed TryGet/Clear drives business branching (SW-017②, SW-009, PAR-011).
    // DSL-MUTABLE: single-flight — per-provider-run attempt plan channel (frozen decision, typed handle)
    let attemptPlans = Dictionary<string, AttemptPlan>()

    /// DSL-cross-callback-proof: physical resource — crash-zero typed recovery-request ownership projection.
    let pendingChatResumes = Dictionary<ChatExecutionKey, PreProviderResumeRequest>()
    /// DSL-cross-callback-proof: physical resource — crash-zero typed recovery-request ownership projection.
    let authorizedChatRequeues = Dictionary<ChatExecutionKey, ProviderRequeueRequest>()

    /// DSL-cross-callback-proof: physical resource — crash-zero typed recovery-request ownership projection.
    let manualChatInterventions =
        Dictionary<ChatExecutionKey, ManualInterventionRequest>()

    let physicalPlanKey (sessionId: SessionId) (physicalUserMessageId: PhysicalUserMessageId) =
        SessionId.value sessionId
        + "\u001f"
        + PhysicalUserMessageId.value physicalUserMessageId

    let providerPlanKey (sessionId: SessionId) (providerRun: ProviderRunIdentity) =
        SessionId.value sessionId + "\u001f" + ProviderRunIdentity.value providerRun

    let installOrdinaryPendingPlan
        (sessionId: SessionId)
        (physicalUserMessageId: PhysicalUserMessageId)
        (ordinaryPlan: PendingAttemptPlan)
        =
        let key = physicalPlanKey sessionId physicalUserMessageId

        match pendingAttemptPlans.TryGetValue key with
        | true, _ -> Ok()
        | false, _ ->
            pendingAttemptPlans.[key] <- ordinaryPlan
            Ok()

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

    member _.ArmRecovery(sessionId: SessionId, physicalUserMessageId: PhysicalUserMessageId) =
        recoveryArming.[SessionId.value sessionId] <-
            { SessionId = sessionId
              PhysicalUserMessageId = physicalUserMessageId
              Arming = RecoverySlot.afterFailureAdvance }

    /// Owning recovery CE consumes the arming exactly once. Returns Some arming
    /// only on first consume; subsequent call returns None (idempotent consume).
    /// Process-local crash-zero: new instance has empty map => None (PAR-011).
    member _.TryTakeRecoveryPermit
        (sessionId: SessionId, physicalUserMessageId: PhysicalUserMessageId)
        : SlotArming option =
        let key = SessionId.value sessionId

        match recoveryArming.TryGetValue key with
        | true, permit when permit.PhysicalUserMessageId = physicalUserMessageId ->
            recoveryArming.Remove key |> ignore
            Some permit.Arming
        | true, _ -> None
        | false, _ -> None

    member _.FreezePendingAttemptPlan
        (sessionId: SessionId)
        (physicalUserMessageId: PhysicalUserMessageId)
        (plan: PendingAttemptPlan)
        =
        installOrdinaryPendingPlan sessionId physicalUserMessageId plan

    member this.RecordPendingAttemptPlan
        (sessionId: SessionId)
        (physicalUserMessageId: PhysicalUserMessageId)
        (plan: PendingAttemptPlan)
        =
        match this.FreezePendingAttemptPlan sessionId physicalUserMessageId plan with
        | Ok() -> ()
        | Error error -> invalidOp (sprintf "HOST-BOUNDARY-008: conflicting pending attempt plan: %A" error)

    member private _.TryTakePendingAttemptPlan
        (sessionId: SessionId)
        (physicalUserMessageId: PhysicalUserMessageId)
        : PendingAttemptPlan option =
        let pendingKey = physicalPlanKey sessionId physicalUserMessageId

        match pendingAttemptPlans.TryGetValue pendingKey with
        | true, pending ->
            pendingAttemptPlans.Remove pendingKey |> ignore
            Some pending
        | false, _ -> None

    member private this.BindPendingAttemptPlan
        (sessionId: SessionId)
        (physicalUserMessageId: PhysicalUserMessageId)
        (providerRun: ProviderRunIdentity)
        : AttemptPlan option =
        this.TryTakePendingAttemptPlan sessionId physicalUserMessageId
        |> Option.map (fun pending ->
            let plan = AttemptPlanner.bindProviderRun providerRun pending
            attemptPlans.[providerPlanKey sessionId providerRun] <- plan
            plan)

    /// Bind the frozen pre-inference decision to the exact assistant run exposed
    /// by a later Host observation. Repeated observations are idempotent by the
    /// provider-run keyed bound-plan registry.
    member this.TryBindAttemptPlan
        (sessionId: SessionId)
        (physicalUserMessageId: PhysicalUserMessageId)
        (providerRun: ProviderRunIdentity)
        : AttemptPlan option =
        match this.TryAttemptPlan sessionId providerRun with
        | Some established when established.Profile.PhysicalUserMessageId = physicalUserMessageId -> Some established
        | Some _ -> None
        | None -> this.BindPendingAttemptPlan sessionId physicalUserMessageId providerRun

    member this.RecordAttemptPlan (sessionId: SessionId) (providerRun: ProviderRunIdentity) (plan: AttemptPlan) =
        attemptPlans.[providerPlanKey sessionId providerRun] <- plan

    /// Owning CE consumes the frozen plan exactly once on terminal reconciliation.
    /// Provisional/unknown turns must use TryAttemptPlan (peek) to keep the plan alive;
    /// only terminal outcomes (TurnCompleted/TurnFailed/TurnAborted) call ConsumeAttemptPlan.
    /// Returns None if already consumed or never recorded.
    member this.ConsumeAttemptPlan (sessionId: SessionId) (providerRun: ProviderRunIdentity) : AttemptPlan option =
        let key = providerPlanKey sessionId providerRun

        match attemptPlans.TryGetValue key with
        | true, plan ->
            attemptPlans.Remove key |> ignore
            Some plan
        | false, _ -> None

    /// Read-only peek for Strength evidence — not for recovery branching.
    member this.TryPeekAttemptPlan (sessionId: SessionId) (providerRun: ProviderRunIdentity) =
        let key = providerPlanKey sessionId providerRun

        match attemptPlans.TryGetValue key with
        | true, plan -> Some plan
        | false, _ -> None

    member this.TryAttemptPlan (sessionId: SessionId) (providerRun: ProviderRunIdentity) =
        this.TryPeekAttemptPlan sessionId providerRun

    member _.PublishPendingChatResume(request: PreProviderResumeRequest) =
        pendingChatResumes.[request.ExecutionKey] <- request

    member _.PublishAuthorizedChatRequeue(request: ProviderRequeueRequest) =
        let key =
            match request with
            | ProviderRequeueRequest.RetryFreshAttempt(started, _)
            | ProviderRequeueRequest.AdvanceFallback(started, _) ->
                { SessionId = started.Accepted.SessionId
                  PhysicalUserMessageId = started.Accepted.PhysicalUserMessageId }

        authorizedChatRequeues.[key] <- request

    member _.PublishManualChatIntervention(request: ManualInterventionRequest) =
        manualChatInterventions.[request.ExecutionState.Key] <- request

    member _.PendingChatRecoveryOwnership() =
        {| Resumes = pendingChatResumes.Values |> Seq.toArray
           Requeues = authorizedChatRequeues.Values |> Seq.toArray
           ManualInterventions = manualChatInterventions.Values |> Seq.toArray |}

    /// Session deletion drops arming and attempt plans for this session.
    member this.ClearSession(sessionId: string) =
        recoveryArming.Remove sessionId |> ignore
        this.ClearAttemptPlansFor sessionId

        let clearExecutionRequests (requests: Dictionary<ChatExecutionKey, 'request>) =
            requests.Keys
            |> Seq.filter (fun key -> SessionId.value key.SessionId = sessionId)
            |> Seq.toArray
            |> Array.iter (fun key -> requests.Remove key |> ignore)

        clearExecutionRequests pendingChatResumes
        clearExecutionRequests authorizedChatRequeues
        clearExecutionRequests manualChatInterventions

    /// Drops attempt plans whose key prefix matches (used for a session and
    /// for its linked Blogger keys during session deletion). Prefer ConsumeAttemptPlan.
    member this.ClearAttemptPlansFor(key: string) =
        attemptPlans.Keys
        |> Seq.filter (fun planKey -> planKey.StartsWith(key + "\u001f", StringComparison.Ordinal))
        |> Seq.toList
        |> List.iter (fun planKey -> attemptPlans.Remove planKey |> ignore)

        pendingAttemptPlans.Keys
        |> Seq.filter (fun planKey -> planKey.StartsWith(key + "\u001f", StringComparison.Ordinal))
        |> Seq.toList
        |> List.iter (fun planKey -> pendingAttemptPlans.Remove planKey |> ignore)
