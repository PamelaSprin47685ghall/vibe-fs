namespace Wanxiangshu.Domain

open Wanxiangshu.Kernel.Identity

/// STRENGTH-006: Prepared is durable material bound to exactly one owner decision
/// and TargetProviderRun. Large bodies are opaque EventStore PayloadRefs only.
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

[<RequireQualifiedAccess>]
type StrengthEvent =
    | Prepared of StrengthCandidatePrepared
    | Promoted of StrengthCandidatePromoted
    | Traced of StrengthFramesTraced
    | Abandoned of StrengthCandidateAbandoned

module StrengthEvents =

    let private canonicalRefs refs = PayloadRefs.canonicalize refs

    let prepared
        (ownerSessionId: SessionId)
        (decisionId: StrengthDecisionId)
        (targetProviderRun: ProviderRunIdentity)
        (replicaSessionId: SessionId)
        (budget: StrengthBudget)
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
              Budget = budget
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
