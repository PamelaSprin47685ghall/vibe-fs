namespace Wanxiangshu.Execution.Session.ChatExecution

open Fable.Core
open Fable.Core.JsInterop
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

    /// CUT-12: which single next step restart recovery may take is decided by
    /// wanxiangli/chat-execution `recover`. This module computes the exact-key and
    /// evidence equalities, hands over the phase and observation kinds, and builds
    /// the typed request from the evidence it already holds.
    [<Import("ChatExecutionLaw", "wanxiangli/chat-execution")>]
    let private lawClass: obj = jsNative

    [<Emit("new $0()")>]
    let private construct (cls: obj) : obj = jsNative

    let private law: obj = construct lawClass

    let private phaseInput (state: ChatExecutionState) : obj =
        let disposition (value: ChatExecutionTerminalDisposition) =
            match value with
            | ChatExecutionTerminalDisposition.Completed -> "Completed"
            | ChatExecutionTerminalDisposition.Cancelled -> "Cancelled"
            | ChatExecutionTerminalDisposition.Rejected -> "Rejected"
            | ChatExecutionTerminalDisposition.Failed -> "Failed"

        match state with
        | ChatExecutionState.Accepted _ -> createObj [ "phase" ==> "Accepted" ]
        | ChatExecutionState.Started _ -> createObj [ "phase" ==> "ProviderStarted" ]
        | ChatExecutionState.EndedBeforeStart(_, outcome) ->
            createObj
                [ "phase" ==> "EndedBeforeStart"
                  "disposition" ==> disposition (PreStartOutcome.disposition outcome) ]
        | ChatExecutionState.EndedAfterStart(_, value) ->
            createObj [ "phase" ==> "EndedAfterStart"; "disposition" ==> disposition value ]

    let private observedStarted (observation: ProviderPhysicalObservation) =
        match observation with
        | ProviderPhysicalObservation.ProviderAlive started
        | ProviderPhysicalObservation.ProviderTerminal(started, _) -> Some started
        | ProviderPhysicalObservation.ReceiptMissing
        | ProviderPhysicalObservation.ReceiptAmbiguous
        | ProviderPhysicalObservation.ProviderAbsent _ -> None

    let private policyInput (evidence: ChatExecutionRecoveryEvidence) : obj =
        match evidence.FailureDecisionEvidence, evidence.ExecutionState.startedEvidence with
        | RecoveryPolicyEvidence.NoFailureDecision, _ -> null
        | RecoveryPolicyEvidence.FailureDecision decision, started ->
            let resolution, matches =
                match decision.Resolution with
                | ExecutionFailureResolution.RetryFreshAttempt authorization ->
                    "RetryFreshAttempt",
                    started
                    |> Option.exists (fun started ->
                        authorization.LogicalRun = started.Accepted.LogicalRunId
                        && authorization.ProviderRun = started.ProviderRun
                        && authorization.RequestKind = started.RequestKind)
                | ExecutionFailureResolution.TerminalizeProviderStarted(key, _) ->
                    "TerminalizeProviderStarted", key = evidence.ExecutionState.key
                | ExecutionFailureResolution.PreserveCurrentFact -> "PreserveCurrentFact", false
                | ExecutionFailureResolution.AwaitAcceptanceReconciliation _ -> "AwaitAcceptanceReconciliation", false
                | ExecutionFailureResolution.TerminalizeAcceptedPreProvider _ -> "TerminalizeAcceptedPreProvider", false

            createObj [ "resolution" ==> resolution; "matches" ==> matches ]

    let private recoveryInput (evidence: ChatExecutionRecoveryEvidence) : obj =
        let state = evidence.ExecutionState
        let observed = observedStarted evidence.ProviderObservation

        createObj
            [ "current" ==> phaseInput state
              "provider"
              ==> (match evidence.ProviderObservation with
                   | ProviderPhysicalObservation.ReceiptMissing -> "ReceiptMissing"
                   | ProviderPhysicalObservation.ReceiptAmbiguous -> "ReceiptAmbiguous"
                   | ProviderPhysicalObservation.ProviderAbsent _ -> "Absent"
                   | ProviderPhysicalObservation.ProviderAlive _ -> "Alive"
                   | ProviderPhysicalObservation.ProviderTerminal _ -> "Terminal")
              "resource"
              ==> (match evidence.ResourceObservation with
                   | PhysicalResourceObservation.ResourceAbsent _ -> "Absent"
                   | PhysicalResourceObservation.ResourceHeld _ -> "Held"
                   | PhysicalResourceObservation.ResourceReleased _ -> "Released"
                   | PhysicalResourceObservation.ResourceUnknown _ -> "Unknown")
              "persistence"
              ==> (match evidence.PersistenceCommitment with
                   | PersistenceCommitment.Unknown -> "Unknown"
                   | PersistenceCommitment.NoNewWrite -> "NoNewWrite"
                   | PersistenceCommitment.Committed -> "Committed"
                   | PersistenceCommitment.NotCommitted -> "NotCommitted")
              "providerKeySame"
              ==> (observationKey evidence.ProviderObservation
                   |> Option.forall (fun key -> key = state.key))
              "resourceKeySame" ==> (resourceKey evidence.ResourceObservation = state.key)
              "providerAcceptedSame"
              ==> (observed
                   |> Option.forall (fun started -> started.Accepted = state.acceptedEvidence))
              "providerSameAsStarted"
              ==> (match observed, state.startedEvidence with
                   | Some observed, Some started -> observed = started
                   | _ -> false)
              "policy" ==> policyInput evidence ]

    let private terminalOf (state: ChatExecutionState) =
        match state with
        | ChatExecutionState.EndedBeforeStart(accepted, outcome) ->
            ChatExecutionTerminalEvidence.PreProvider accepted, PreStartOutcome.disposition outcome
        | ChatExecutionState.EndedAfterStart(started, disposition) ->
            ChatExecutionTerminalEvidence.AfterProviderStart started, disposition
        | ChatExecutionState.Accepted _
        | ChatExecutionState.Started _ -> invalidOp "recover released a terminal resource of a non-terminal execution"

    let private finalize (started: ProviderStartedEvidence) (disposition: ChatExecutionTerminalDisposition) =
        ChatExecutionRecoveryDecision.Finalize
            { ExecutionKey =
                { SessionId = started.Accepted.SessionId
                  PhysicalUserMessageId = started.Accepted.PhysicalUserMessageId }
              TerminalEvidence = ChatExecutionTerminalEvidence.AfterProviderStart started
              TerminalDisposition = disposition }

    let private unexpected (what: string) : 'T =
        invalidOp ("wanxiangli/chat-execution recover returned " + what)

    let decide (evidence: ChatExecutionRecoveryEvidence) : ChatExecutionRecoveryDecision =
        let state = evidence.ExecutionState
        let verdict: obj = law?recover (recoveryInput evidence)

        match unbox<string> verdict?decision with
        | "ignore" ->
            let reason =
                match unbox<string> verdict?reason with
                | "DurableTerminalAlreadySettled" -> IgnoreReason.DurableTerminalAlreadySettled
                | "ProviderStillAlive" -> IgnoreReason.ProviderStillAlive
                | "RecoveryAlreadyCommitted" -> IgnoreReason.RecoveryAlreadyCommitted
                | "ProviderRecoveryOwned" -> IgnoreReason.ProviderRecoveryOwned
                | "StalePhysicalEvidence" -> IgnoreReason.StalePhysicalEvidence
                | "StalePolicyEvidence" -> IgnoreReason.StalePolicyEvidence
                | other -> unexpected ("an unknown ignore reason " + other)

            let key =
                match reason, evidence.ResourceObservation with
                | IgnoreReason.DurableTerminalAlreadySettled, resource -> resourceKey resource
                | _ -> state.key

            ChatExecutionRecoveryDecision.Ignore(key, reason)
        | "manual" ->
            let reason =
                match unbox<string> verdict?reason with
                | "MissingExternalReceipt" -> ManualInterventionReason.MissingExternalReceipt
                | "AmbiguousExternalReceipt" -> ManualInterventionReason.AmbiguousExternalReceipt
                | "PhysicalOutcomeUnknown" -> ManualInterventionReason.PhysicalOutcomeUnknown
                | "PersistenceOutcomeUnknown" -> ManualInterventionReason.PersistenceOutcomeUnknown
                | "NoAuthorizedProviderDisposition" -> ManualInterventionReason.NoAuthorizedProviderDisposition
                | other -> unexpected ("an unknown manual reason " + other)

            manual reason evidence
        | "releaseTerminalResource" ->
            let terminalEvidence, disposition = terminalOf state

            ChatExecutionRecoveryDecision.ReconcilePhysical(
                PhysicalReconciliationRequest.ReleaseTerminalResource(
                    resourceKey evidence.ResourceObservation,
                    terminalEvidence,
                    disposition
                )
            )
        | "resumePreProvider" ->
            ChatExecutionRecoveryDecision.ResumePreProvider
                { ExecutionKey = state.key
                  AcceptedEvidence = state.acceptedEvidence }
        | "persistProviderStarted" ->
            match evidence.ProviderObservation with
            | ProviderPhysicalObservation.ProviderAlive started ->
                ChatExecutionRecoveryDecision.ReconcilePhysical(
                    PhysicalReconciliationRequest.PersistProviderStarted started
                )
            | _ -> unexpected "persistProviderStarted without a live provider"
        | "persistProviderStartedAndTerminal" ->
            match evidence.ProviderObservation with
            | ProviderPhysicalObservation.ProviderTerminal(started, disposition) ->
                ChatExecutionRecoveryDecision.ReconcilePhysical(
                    PhysicalReconciliationRequest.PersistProviderStartedAndTerminal(started, disposition)
                )
            | _ -> unexpected "persistProviderStartedAndTerminal without a terminal provider"
        | "finalize" ->
            match
                unbox<string> verdict?from,
                state.startedEvidence,
                evidence.ProviderObservation,
                evidence.FailureDecisionEvidence
            with
            | "observation", Some started, ProviderPhysicalObservation.ProviderTerminal(_, disposition), _ ->
                finalize started disposition
            | "policy", Some started, _, RecoveryPolicyEvidence.FailureDecision decision ->
                match decision.Resolution with
                | ExecutionFailureResolution.TerminalizeProviderStarted(_, disposition) -> finalize started disposition
                | _ -> unexpected "a policy finalize without a provider-started terminalization"
            | _ -> unexpected "finalize without its started evidence and disposition"
        | other -> unexpected ("an unknown decision " + other)
