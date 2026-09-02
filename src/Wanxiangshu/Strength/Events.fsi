namespace Wanxiangshu.Strength

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore

type StrengthCandidatePrepared =
    { OwnerSessionId: SessionId
      DecisionId: StrengthDecisionId
      TargetProviderRun: ProviderRunIdentity
      ReplicaSessionId: SessionId
      Budget: StrengthBudget
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

[<RequireQualifiedAccess>]
type StrengthEvent =
    | Prepared of StrengthCandidatePrepared
    | Promoted of StrengthCandidatePromoted
    | Traced of StrengthFramesTraced
    | Abandoned of StrengthCandidateAbandoned

module StrengthEvents =
    val prepared:
        ownerSessionId: SessionId ->
        decisionId: StrengthDecisionId ->
        targetProviderRun: ProviderRunIdentity ->
        replicaSessionId: SessionId ->
        budget: StrengthBudget ->
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
