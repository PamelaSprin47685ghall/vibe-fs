namespace Wanxiangshu.Strength

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.EventStore



type StrengthCandidatePrepared =
    { OwnerSessionId: SessionId
      DecisionId: StrengthDecisionId
      TargetProviderRun: ProviderRunIdentity
      ReplicaSessionId: SessionId
      AnchorDigest: string
      FrameDigest: string
      ByteLength: int
      MaterialPayloads: PayloadRef list }

type StrengthCandidatePromoted =
    { OwnerSessionId: SessionId
      DecisionId: StrengthDecisionId
      TargetProviderRun: ProviderRunIdentity
      FrameDigest: string
      MaterialPayloads: PayloadRef list }

type StrengthFramesTraced =
    { DecisionId: StrengthDecisionId
      StartInclusive: int64
      EndExclusive: int64 }

type StrengthCandidateAbandoned =
    { DecisionId: StrengthDecisionId
      TargetProviderRun: ProviderRunIdentity }

type DelegationImportedMaterial =
    { TargetProviderRun: ProviderRunIdentity
      FrameDigest: string
      ByteLength: int
      MaterialPayloads: PayloadRef list
      TracedStartInclusive: int64 option
      TracedEndExclusive: int64 option }

type DelegationRelinquishedMaterial =
    { TargetProviderRun: ProviderRunIdentity option
      Reason: string }

[<RequireQualifiedAccess>]
type DelegationImportOutcome =
    | Adopted of DelegationImportedMaterial
    | Relinquished of DelegationRelinquishedMaterial

type DelegationHistoryImported =
    { DecisionId: StrengthDecisionId
      SourceStreamId: string
      SourceEventId: string
      ImportId: string
      OldBudgetEvidence: string option
      Outcome: DelegationImportOutcome }

[<RequireQualifiedAccess>]
type StrengthEvent =
    | DelegationRequested of DelegationRequest
    | DelegationBound of DelegationBinding
    | DelegationClosed of DelegationClosed
    | Prepared of StrengthCandidatePrepared
    | Promoted of StrengthCandidatePromoted
    | Traced of StrengthFramesTraced
    | Abandoned of StrengthCandidateAbandoned
    | DelegationHistoryImported of DelegationHistoryImported

module StrengthEvents =
    val requested:
        decisionId: StrengthDecisionId ->
        ownerSessionId: SessionId ->
        ownerLogicalRun: OwnerLogicalRunIdentity ->
        sourcePhysicalUserMessageId: PhysicalUserMessageId ->
        sourceProviderRun: ProviderRunIdentity ->
        sourceToolCallIds: ToolCallId list ->
        requestedRounds: ReadonlyRoundBudget ->
        contractRevision: DelegationContractRevision ->
            StrengthEvent

    val bound:
        decisionId: StrengthDecisionId ->
        targetProviderRun: ProviderRunIdentity ->
        replicaSessionId: SessionId ->
        anchorDigest: string ->
            StrengthEvent

    val closed:
        decisionId: StrengthDecisionId ->
        closedFrom: DelegationClosedFrom ->
        reason: DelegationClosedReason ->
            StrengthEvent

    val prepared:
        ownerSessionId: SessionId ->
        decisionId: StrengthDecisionId ->
        targetProviderRun: ProviderRunIdentity ->
        replicaSessionId: SessionId ->
        anchorDigest: string ->
        frameDigest: string ->
        byteLength: int ->
        materialPayloads: PayloadRef list ->
            StrengthEvent

    val promoted:
        ownerSessionId: SessionId ->
        decisionId: StrengthDecisionId ->
        targetProviderRun: ProviderRunIdentity ->
        frameDigest: string ->
        materialPayloads: PayloadRef list ->
            StrengthEvent

    val traced: decisionId: StrengthDecisionId -> startInclusive: int64 -> endExclusive: int64 -> StrengthEvent
    val abandoned: decisionId: StrengthDecisionId -> targetProviderRun: ProviderRunIdentity -> StrengthEvent

    val historyImported:
        decisionId: StrengthDecisionId ->
        sourceStreamId: string ->
        sourceEventId: string ->
        importId: string ->
        oldBudgetEvidence: string option ->
        outcome: DelegationImportOutcome ->
            StrengthEvent
