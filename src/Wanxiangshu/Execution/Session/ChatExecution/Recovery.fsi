namespace Wanxiangshu.Execution.Session.ChatExecution

open Wanxiangshu.Execution.Failure

[<RequireQualifiedAccess>]
type ProviderPhysicalObservation =
    | ReceiptMissing
    | ReceiptAmbiguous
    | ProviderAbsent of ChatExecutionKey
    | ProviderAlive of ProviderStartedEvidence
    | ProviderTerminal of ProviderStartedEvidence * ChatExecutionTerminalDisposition

[<RequireQualifiedAccess>]
type PhysicalResourceObservation =
    | ResourceAbsent of ChatExecutionKey
    | ResourceHeld of ChatExecutionKey
    | ResourceReleased of ChatExecutionKey
    | ResourceUnknown of ChatExecutionKey

[<RequireQualifiedAccess>]
type RecoveryPolicyEvidence =
    | NoFailureDecision
    | FailureDecision of ExecutionFailureDecision

type ChatExecutionRecoveryEvidence =
    { ExecutionState: ChatExecutionState
      ProviderObservation: ProviderPhysicalObservation
      ResourceObservation: PhysicalResourceObservation
      PersistenceCommitment: PersistenceCommitment
      FailureDecisionEvidence: RecoveryPolicyEvidence }

type PreProviderResumeRequest =
    { ExecutionKey: ChatExecutionKey
      AcceptedEvidence: AcceptedChatExecutionEvidence }

[<RequireQualifiedAccess>]
type PhysicalReconciliationRequest =
    | PersistProviderStarted of ProviderStartedEvidence
    | PersistProviderStartedAndTerminal of ProviderStartedEvidence * ChatExecutionTerminalDisposition
    | ReleaseTerminalResource of ChatExecutionKey * ChatExecutionTerminalEvidence * ChatExecutionTerminalDisposition

[<RequireQualifiedAccess>]
type ProviderRequeueRequest =
    | RetryFreshAttempt of ProviderStartedEvidence * ProviderRecoveryAuthorization
    | AdvanceFallback of ProviderStartedEvidence * ProviderRecoveryAuthorization

type TerminalFinalizationRequest =
    { ExecutionKey: ChatExecutionKey
      TerminalEvidence: ChatExecutionTerminalEvidence
      TerminalDisposition: ChatExecutionTerminalDisposition }

[<RequireQualifiedAccess>]
type ManualInterventionReason =
    | MissingExternalReceipt
    | AmbiguousExternalReceipt
    | PhysicalOutcomeUnknown
    | PersistenceOutcomeUnknown
    | NoAuthorizedProviderDisposition

type ManualInterventionRequest =
    { ExecutionState: ChatExecutionState
      ProviderObservation: ProviderPhysicalObservation
      ResourceObservation: PhysicalResourceObservation
      InterventionReason: ManualInterventionReason }

[<RequireQualifiedAccess>]
type IgnoreReason =
    | DurableTerminalAlreadySettled
    | ProviderStillAlive
    | RecoveryAlreadyCommitted
    | StalePhysicalEvidence
    | StalePolicyEvidence

[<RequireQualifiedAccess>]
type ChatExecutionRecoveryDecision =
    | Ignore of ChatExecutionKey * IgnoreReason
    | ReconcilePhysical of PhysicalReconciliationRequest
    | ResumePreProvider of PreProviderResumeRequest
    | RequeueEligible of ProviderRequeueRequest
    | Finalize of TerminalFinalizationRequest
    | MarkManualIntervention of ManualInterventionRequest

[<RequireQualifiedAccess>]
module ChatExecutionRecovery =
    val decide: evidence: ChatExecutionRecoveryEvidence -> ChatExecutionRecoveryDecision
