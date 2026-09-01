namespace Wanxiangshu.Strength.Persistence

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Projection

[<RequireQualifiedAccess>]
type StrengthPreparedPublish =
    | Published
    | Rejected of reason: string
    | StorageInvalid of reason: string

[<RequireQualifiedAccess>]
type StrengthDurableAppend =
    | Applied
    | SemanticRejected of reason: string
    | StorageFailed of reason: string

type StrengthPreparedRequest =
    { OwnerSessionId: SessionId
      DecisionId: StrengthDecisionId
      TargetProviderRun: ProviderRunIdentity
      ReplicaSessionId: SessionId
      Budget: StrengthBudget
      AnchorDigest: string
      Bundle: StrengthFrameBundle }

type StrengthDurabilityPort =
    { LoadProjection: unit -> Task<Result<StrengthProjection, string>>
      LoadFrameBundle: StrengthCandidatePrepared -> Task<Result<StrengthFrameBundle, string>>
      PublishPrepared: StrengthPreparedRequest -> Task<StrengthPreparedPublish>
      Append: StrengthEvent -> Task<StrengthDurableAppend> }
