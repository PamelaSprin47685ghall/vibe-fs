namespace Wanxiangshu.Strength
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
open Wanxiangshu.Strength.Prediction

open Wanxiangshu.Foundation.Identity

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

[<RequireQualifiedAccess>]
module StrengthEventTypes =
    let CandidatePrepared = "StrengthCandidatePrepared"
    let CandidatePromoted = "StrengthCandidatePromoted"
    let FramesTraced = "StrengthFramesTraced"
    let CandidateAbandoned = "StrengthCandidateAbandoned"

    let all = [ CandidatePrepared; CandidatePromoted; FramesTraced; CandidateAbandoned ]

    let isStrengthEvent eventType = all |> List.contains eventType

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
