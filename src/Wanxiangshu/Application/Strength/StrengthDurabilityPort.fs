namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Kernel.Identity

[<RequireQualifiedAccess>]
type StrengthPreparedPublish =
    | Published
    | Rejected of reason: string
    | StorageInvalid of reason: string

type StrengthPreparedRequest =
    { OwnerSessionId: SessionId
      DecisionId: StrengthDecisionId
      TargetProviderRun: ProviderRunIdentity
      ReplicaSessionId: SessionId
      Budget: StrengthBudget
      AnchorDigest: string
      Bundle: StrengthFrameBundle }

/// STRENGTH-006..008: durable Strength capability exposed to Application/Host.
/// The port contains no storage identity. Persist owns EventStore, payload closure,
/// append outcomes and material codecs; callers only ask domain-level questions.
type StrengthDurabilityPort =
    { LoadProjection: unit -> Task<Result<StrengthProjection, string>>
      LoadFrameBundle: StrengthCandidatePrepared -> Task<Result<StrengthFrameBundle, string>>
      PublishPrepared: StrengthPreparedRequest -> Task<StrengthPreparedPublish>
      Append: StrengthEvent -> Task<Result<unit, string>> }
