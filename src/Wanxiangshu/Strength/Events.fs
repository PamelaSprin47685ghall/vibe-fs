namespace Wanxiangshu.Strength

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.EventStore



/// STRENGTH-006: Prepared is durable material bound to exactly one owner decision
/// and TargetProviderRun. Large bodies are opaque EventStore PayloadRefs only.
/// The tier budget field is gone: rounds belong to the DelegationRequested fact.
type StrengthCandidatePrepared =
    { OwnerSessionId: SessionId
      DecisionId: StrengthDecisionId
      TargetProviderRun: ProviderRunIdentity
      ReplicaSessionId: SessionId
      AnchorDigest: string
      FrameDigest: string
      ByteLength: int
      MaterialPayloads: PayloadRef list }

/// STRENGTH-007: Promotion repeats the causal identity/digest/refs so the fold can
/// reject a writer that attempts to promote different material or the wrong run.
type StrengthCandidatePromoted =
    { OwnerSessionId: SessionId
      DecisionId: StrengthDecisionId
      TargetProviderRun: ProviderRunIdentity
      FrameDigest: string
      MaterialPayloads: PayloadRef list }

/// STRENGTH-008: association between a Promoted decision and the existing XTrace
/// cursor range it actually entered. End is exclusive.
type StrengthFramesTraced =
    { DecisionId: StrengthDecisionId
      StartInclusive: int64
      EndExclusive: int64 }

type StrengthCandidateAbandoned =
    { DecisionId: StrengthDecisionId
      TargetProviderRun: ProviderRunIdentity }

/// DELEGATE: history material a pre-delegation candidate carries into the
/// current protocol. It is evidence, never an admission credential.
type DelegationImportedMaterial =
    { TargetProviderRun: ProviderRunIdentity
      FrameDigest: string
      ByteLength: int
      MaterialPayloads: PayloadRef list
      TracedStartInclusive: int64 option
      TracedEndExclusive: int64 option }

/// DELEGATE: a pre-delegation candidate whose material never completed
/// promotion. The reason names why it could not be adopted as evidence.
type DelegationRelinquishedMaterial =
    { TargetProviderRun: ProviderRunIdentity option
      Reason: string }

[<RequireQualifiedAccess>]
type DelegationImportOutcome =
    | Adopted of DelegationImportedMaterial
    | Relinquished of DelegationRelinquishedMaterial

/// DELEGATE: a fresh fact that only carries pre-delegation history material
/// into the current protocol (durable-events-026). It never grants a request,
/// binding or candidate: the fold keeps it out of ByDecision and ByTargetRun,
/// so no runnable delegation can be derived from it and no duplicate exists.
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

    let private canonicalRefs refs = PayloadRefs.canonicalize refs

    let requested
        (decisionId: StrengthDecisionId)
        (ownerSessionId: SessionId)
        (ownerLogicalRun: OwnerLogicalRunIdentity)
        (sourcePhysicalUserMessageId: PhysicalUserMessageId)
        (sourceProviderRun: ProviderRunIdentity)
        (sourceToolCallIds: ToolCallId list)
        (requestedRounds: ReadonlyRoundBudget)
        (contractRevision: DelegationContractRevision)
        : StrengthEvent =
        StrengthEvent.DelegationRequested
            { DecisionId = decisionId
              OwnerSessionId = ownerSessionId
              OwnerLogicalRun = ownerLogicalRun
              SourcePhysicalUserMessageId = sourcePhysicalUserMessageId
              SourceProviderRun = sourceProviderRun
              SourceToolCallIds = sourceToolCallIds
              RequestedRounds = requestedRounds
              ContractRevision = contractRevision }

    let bound
        (decisionId: StrengthDecisionId)
        (targetProviderRun: ProviderRunIdentity)
        (replicaSessionId: SessionId)
        (anchorDigest: string)
        : StrengthEvent =
        StrengthEvent.DelegationBound
            { DecisionId = decisionId
              TargetProviderRun = targetProviderRun
              ReplicaSessionId = replicaSessionId
              AnchorDigest = anchorDigest }

    let closed
        (decisionId: StrengthDecisionId)
        (closedFrom: DelegationClosedFrom)
        (reason: DelegationClosedReason)
        : StrengthEvent =
        StrengthEvent.DelegationClosed
            { DecisionId = decisionId
              From = closedFrom
              Reason = reason }

    let prepared
        (ownerSessionId: SessionId)
        (decisionId: StrengthDecisionId)
        (targetProviderRun: ProviderRunIdentity)
        (replicaSessionId: SessionId)
        (anchorDigest: string)
        (frameDigest: string)
        (byteLength: int)
        (materialPayloads: PayloadRef list)
        : StrengthEvent =
        StrengthEvent.Prepared
            { OwnerSessionId = ownerSessionId
              DecisionId = decisionId
              TargetProviderRun = targetProviderRun
              ReplicaSessionId = replicaSessionId
              AnchorDigest = anchorDigest
              FrameDigest = frameDigest
              ByteLength = byteLength
              MaterialPayloads = canonicalRefs materialPayloads }

    let promoted
        (ownerSessionId: SessionId)
        (decisionId: StrengthDecisionId)
        (targetProviderRun: ProviderRunIdentity)
        (frameDigest: string)
        (materialPayloads: PayloadRef list)
        : StrengthEvent =
        StrengthEvent.Promoted
            { OwnerSessionId = ownerSessionId
              DecisionId = decisionId
              TargetProviderRun = targetProviderRun
              FrameDigest = frameDigest
              MaterialPayloads = canonicalRefs materialPayloads }

    let traced (decisionId: StrengthDecisionId) (startInclusive: int64) (endExclusive: int64) : StrengthEvent =
        StrengthEvent.Traced
            { DecisionId = decisionId
              StartInclusive = startInclusive
              EndExclusive = endExclusive }

    let abandoned (decisionId: StrengthDecisionId) (targetProviderRun: ProviderRunIdentity) : StrengthEvent =
        StrengthEvent.Abandoned
            { DecisionId = decisionId
              TargetProviderRun = targetProviderRun }

    let historyImported
        (decisionId: StrengthDecisionId)
        (sourceStreamId: string)
        (sourceEventId: string)
        (importId: string)
        (oldBudgetEvidence: string option)
        (outcome: DelegationImportOutcome)
        : StrengthEvent =
        StrengthEvent.DelegationHistoryImported
            { DecisionId = decisionId
              SourceStreamId = sourceStreamId
              SourceEventId = sourceEventId
              ImportId = importId
              OldBudgetEvidence = oldBudgetEvidence
              Outcome = outcome }
