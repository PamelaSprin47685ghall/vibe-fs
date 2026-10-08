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
    | ProviderRecoveryOwned
    | StalePhysicalEvidence
    | StalePolicyEvidence

[<RequireQualifiedAccess>]
type ChatExecutionRecoveryDecision =
    | Ignore of ChatExecutionKey * IgnoreReason
    | ReconcilePhysical of PhysicalReconciliationRequest
    | ResumePreProvider of PreProviderResumeRequest
    | Finalize of TerminalFinalizationRequest
    | MarkManualIntervention of ManualInterventionRequest

[<RequireQualifiedAccess>]
module ChatExecutionRecovery =

    let private observationKey (observation: ProviderPhysicalObservation) =
        match observation with
        | ProviderPhysicalObservation.ReceiptMissing
        | ProviderPhysicalObservation.ReceiptAmbiguous -> None
        | ProviderPhysicalObservation.ProviderAbsent key -> Some key
        | ProviderPhysicalObservation.ProviderAlive started
        | ProviderPhysicalObservation.ProviderTerminal(started, _) ->
            Some
                { SessionId = started.Accepted.SessionId
                  PhysicalUserMessageId = started.Accepted.PhysicalUserMessageId }

    let private resourceKey (observation: PhysicalResourceObservation) =
        match observation with
        | PhysicalResourceObservation.ResourceAbsent key
        | PhysicalResourceObservation.ResourceHeld key
        | PhysicalResourceObservation.ResourceReleased key
        | PhysicalResourceObservation.ResourceUnknown key -> key

    let private manual (reason: ManualInterventionReason) (evidence: ChatExecutionRecoveryEvidence) =
        ChatExecutionRecoveryDecision.MarkManualIntervention
            { ExecutionState = evidence.ExecutionState
              ProviderObservation = evidence.ProviderObservation
              ResourceObservation = evidence.ResourceObservation
              InterventionReason = reason }

    let private stale (evidence: ChatExecutionRecoveryEvidence) =
        let providerEvidenceStale =
            match evidence.ProviderObservation with
            | ProviderPhysicalObservation.ProviderAlive started
            | ProviderPhysicalObservation.ProviderTerminal(started, _) ->
                started.Accepted <> evidence.ExecutionState.acceptedEvidence
            | ProviderPhysicalObservation.ReceiptMissing
            | ProviderPhysicalObservation.ReceiptAmbiguous
            | ProviderPhysicalObservation.ProviderAbsent _ -> false

        (observationKey evidence.ProviderObservation
         |> Option.exists ((<>) evidence.ExecutionState.key))
        || resourceKey evidence.ResourceObservation <> evidence.ExecutionState.key
        || providerEvidenceStale

    let private terminalDecision
        (evidence: ChatExecutionRecoveryEvidence)
        (terminalEvidence: ChatExecutionTerminalEvidence)
        (disposition: ChatExecutionTerminalDisposition)
        =
        match evidence.ResourceObservation with
        | PhysicalResourceObservation.ResourceHeld key ->
            ChatExecutionRecoveryDecision.ReconcilePhysical(
                PhysicalReconciliationRequest.ReleaseTerminalResource(key, terminalEvidence, disposition)
            )
        | PhysicalResourceObservation.ResourceAbsent key
        | PhysicalResourceObservation.ResourceReleased key ->
            ChatExecutionRecoveryDecision.Ignore(key, IgnoreReason.DurableTerminalAlreadySettled)
        | PhysicalResourceObservation.ResourceUnknown _ ->
            manual ManualInterventionReason.PhysicalOutcomeUnknown evidence

    let private acceptedDecision (evidence: ChatExecutionRecoveryEvidence) =
        match evidence.ProviderObservation with
        | ProviderPhysicalObservation.ReceiptMissing -> manual ManualInterventionReason.MissingExternalReceipt evidence
        | ProviderPhysicalObservation.ReceiptAmbiguous ->
            manual ManualInterventionReason.AmbiguousExternalReceipt evidence
        | ProviderPhysicalObservation.ProviderAbsent _ ->
            ChatExecutionRecoveryDecision.ResumePreProvider
                { ExecutionKey = evidence.ExecutionState.key
                  AcceptedEvidence = evidence.ExecutionState.acceptedEvidence }
        | ProviderPhysicalObservation.ProviderAlive started ->
            ChatExecutionRecoveryDecision.ReconcilePhysical(
                PhysicalReconciliationRequest.PersistProviderStarted started
            )
        | ProviderPhysicalObservation.ProviderTerminal(started, disposition) ->
            ChatExecutionRecoveryDecision.ReconcilePhysical(
                PhysicalReconciliationRequest.PersistProviderStartedAndTerminal(started, disposition)
            )

    let private finalize (started: ProviderStartedEvidence) (disposition: ChatExecutionTerminalDisposition) =
        ChatExecutionRecoveryDecision.Finalize
            { ExecutionKey =
                { SessionId = started.Accepted.SessionId
                  PhysicalUserMessageId = started.Accepted.PhysicalUserMessageId }
              TerminalEvidence = ChatExecutionTerminalEvidence.AfterProviderStart started
              TerminalDisposition = disposition }

    let private providerPolicyDecision
        (evidence: ChatExecutionRecoveryEvidence)
        (started: ProviderStartedEvidence)
        (decision: ExecutionFailureDecision)
        =
        let authorizationMatches (authorization: ProviderRecoveryAuthorization) =
            authorization.LogicalRun = started.Accepted.LogicalRunId
            && authorization.ProviderRun = started.ProviderRun
            && authorization.RequestKind = started.RequestKind

        match decision.Resolution with
        | ExecutionFailureResolution.RetryFreshAttempt authorization when authorizationMatches authorization ->
            ChatExecutionRecoveryDecision.Ignore(evidence.ExecutionState.key, IgnoreReason.ProviderRecoveryOwned)
        | ExecutionFailureResolution.TerminalizeProviderStarted(key, disposition) when key = evidence.ExecutionState.key ->
            finalize started disposition
        | ExecutionFailureResolution.RetryFreshAttempt _
        | ExecutionFailureResolution.TerminalizeProviderStarted _ ->
            ChatExecutionRecoveryDecision.Ignore(evidence.ExecutionState.key, IgnoreReason.StalePolicyEvidence)
        | ExecutionFailureResolution.PreserveCurrentFact
        | ExecutionFailureResolution.AwaitAcceptanceReconciliation _
        | ExecutionFailureResolution.TerminalizeAcceptedPreProvider _ ->
            manual ManualInterventionReason.NoAuthorizedProviderDisposition evidence

    let private providerAbsentDecision (evidence: ChatExecutionRecoveryEvidence) (started: ProviderStartedEvidence) =
        match evidence.FailureDecisionEvidence with
        | RecoveryPolicyEvidence.FailureDecision decision -> providerPolicyDecision evidence started decision
        | RecoveryPolicyEvidence.NoFailureDecision ->
            manual ManualInterventionReason.NoAuthorizedProviderDisposition evidence

    let private startedDecision (evidence: ChatExecutionRecoveryEvidence) (started: ProviderStartedEvidence) =
        match evidence.ProviderObservation with
        | ProviderPhysicalObservation.ReceiptMissing -> manual ManualInterventionReason.MissingExternalReceipt evidence
        | ProviderPhysicalObservation.ReceiptAmbiguous ->
            manual ManualInterventionReason.AmbiguousExternalReceipt evidence
        | ProviderPhysicalObservation.ProviderAlive observed when observed = started ->
            ChatExecutionRecoveryDecision.Ignore(evidence.ExecutionState.key, IgnoreReason.ProviderStillAlive)
        | ProviderPhysicalObservation.ProviderTerminal(observed, disposition) when observed = started ->
            finalize started disposition
        | ProviderPhysicalObservation.ProviderAbsent _ -> providerAbsentDecision evidence started
        | ProviderPhysicalObservation.ProviderAlive _
        | ProviderPhysicalObservation.ProviderTerminal _ ->
            ChatExecutionRecoveryDecision.Ignore(evidence.ExecutionState.key, IgnoreReason.StalePhysicalEvidence)

    let private notCommittedDecision (evidence: ChatExecutionRecoveryEvidence) =
        match evidence.ExecutionState with
        | ChatExecutionState.Accepted _ -> acceptedDecision evidence
        | ChatExecutionState.Started started -> startedDecision evidence started
        | ChatExecutionState.EndedBeforeStart _
        | ChatExecutionState.EndedAfterStart _ -> manual ManualInterventionReason.PhysicalOutcomeUnknown evidence

    let private persistenceDecision (evidence: ChatExecutionRecoveryEvidence) =
        match evidence.PersistenceCommitment with
        | PersistenceCommitment.Unknown -> manual ManualInterventionReason.PersistenceOutcomeUnknown evidence
        | PersistenceCommitment.NoNewWrite -> manual ManualInterventionReason.PersistenceOutcomeUnknown evidence
        | PersistenceCommitment.Committed ->
            ChatExecutionRecoveryDecision.Ignore(evidence.ExecutionState.key, IgnoreReason.RecoveryAlreadyCommitted)
        | PersistenceCommitment.NotCommitted -> notCommittedDecision evidence

    let terminalOfEnded (evidence: ChatExecutionRecoveryEvidence) : ChatExecutionRecoveryDecision =
        match evidence.ExecutionState with
        | ChatExecutionState.EndedBeforeStart(accepted, outcome) ->
            terminalDecision
                evidence
                (ChatExecutionTerminalEvidence.PreProvider accepted)
                (PreStartOutcome.disposition outcome)
        | ChatExecutionState.EndedAfterStart(started, disposition) ->
            terminalDecision evidence (ChatExecutionTerminalEvidence.AfterProviderStart started) disposition
        | ChatExecutionState.Accepted _
        | ChatExecutionState.Started _ -> persistenceDecision evidence

    let decide (evidence: ChatExecutionRecoveryEvidence) : ChatExecutionRecoveryDecision =
        if stale evidence then
            ChatExecutionRecoveryDecision.Ignore(evidence.ExecutionState.key, IgnoreReason.StalePhysicalEvidence)
        else
            terminalOfEnded evidence
