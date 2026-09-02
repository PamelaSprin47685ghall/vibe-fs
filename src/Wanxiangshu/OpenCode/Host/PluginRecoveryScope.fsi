namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Execution.Session.Recovery.SessionRecovery
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Persistence.Journal

/// Binding error when a pending attempt plan cannot be found at bind time.
[<RequireQualifiedAccess>]
type TransformAttemptPlanBindingError =
    | PendingAttemptPlanMissing of SessionId * PhysicalUserMessageId * ProviderRunIdentity

/// Family recovery coordination (PROMPT-011 + C5 + RECOVERY-FAMILY) and
/// attempt planning state for one plugin instance: recovery ports attachment,
/// per-session arming and per-provider-run attempt plans.
type PluginRecoveryScope =
    new: journal: AgentJournal option -> PluginRecoveryScope

    /// Certifies this process's join attempt for family recovery.
    member RequireFamilyRecovery: root: SessionId -> Task<FamilyRecovery>

    /// Idempotent alias for RequireFamilyRecovery.
    member EnsureRecoveryDone: root: SessionId -> Task<FamilyRecovery>

    /// Arms a one-shot recovery permit after PromptIngress acceptance.
    member ArmRecovery: sessionId: SessionId * physicalUserMessageId: PhysicalUserMessageId -> unit

    /// Consumes the arming exactly once.
    member TryTakeRecoveryPermit:
        sessionId: SessionId * physicalUserMessageId: PhysicalUserMessageId -> SlotArming option

    /// Freezes a pre-inference attempt plan under the exact physical user message.
    member FreezePendingAttemptPlan:
        sessionId: SessionId ->
        physicalUserMessageId: PhysicalUserMessageId ->
        plan: PendingAttemptPlan ->
            Result<unit, 'a>

    /// Freezes a pre-inference attempt plan, throwing on conflict.
    member RecordPendingAttemptPlan:
        sessionId: SessionId -> physicalUserMessageId: PhysicalUserMessageId -> plan: PendingAttemptPlan -> unit

    /// Binds the frozen pre-inference decision to the exact assistant run.
    member TryBindAttemptPlan:
        sessionId: SessionId ->
        physicalUserMessageId: PhysicalUserMessageId ->
        providerRun: ProviderRunIdentity ->
            AttemptPlan option

    /// Records an already-bound attempt plan under the provider run.
    member RecordAttemptPlan: sessionId: SessionId -> providerRun: ProviderRunIdentity -> plan: AttemptPlan -> unit

    /// Consumes the frozen plan exactly once on terminal reconciliation.
    member ConsumeAttemptPlan: sessionId: SessionId -> providerRun: ProviderRunIdentity -> AttemptPlan option

    /// Read-only peek for Strength evidence — not for recovery branching.
    member TryPeekAttemptPlan: sessionId: SessionId -> providerRun: ProviderRunIdentity -> AttemptPlan option

    /// Read-only peek alias.
    member TryAttemptPlan: sessionId: SessionId -> providerRun: ProviderRunIdentity -> AttemptPlan option

    member PublishPendingChatResume: request: PreProviderResumeRequest -> unit

    member PublishAuthorizedChatRequeue: request: ProviderRequeueRequest -> unit

    member PublishManualChatIntervention: request: ManualInterventionRequest -> unit

    /// Returns the currently published recovery ownership requests.
    member PendingChatRecoveryOwnership:
        unit ->
            {| Resumes: PreProviderResumeRequest[]
               Requeues: ProviderRequeueRequest[]
               ManualInterventions: ManualInterventionRequest[] |}

    /// Session deletion drops arming and attempt plans for this session.
    member ClearSession: sessionId: string -> unit

    /// Drops attempt plans whose key prefix matches the given key.
    member ClearAttemptPlansFor: key: string -> unit
