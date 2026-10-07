namespace Wanxiangshu.Strength.Persistence

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Projection

[<RequireQualifiedAccess>]
type StrengthPreparedPublish =
    | Published
    | Rejected of reason: string
    | StorageInvalid of reason: string
    | SettlementFailed of EventId * AppendError

[<RequireQualifiedAccess>]
type StrengthDurableAppend =
    | Applied
    | SemanticRejected of reason: string
    | StorageInvalid of reason: string
    | StorageFailed of reason: string
    | SettlementFailed of EventId * AppendError

[<Sealed>]
type StrengthAppendException =
    inherit System.Exception
    new: eventId: EventId * failure: AppendError * cleanupFailures: exn list -> StrengthAppendException
    member EventId: EventId
    member Failure: AppendError
    member CleanupFailures: exn list

type StrengthPreparedRequest =
    { OwnerSessionId: SessionId
      DecisionId: StrengthDecisionId
      TargetProviderRun: ProviderRunIdentity
      ReplicaSessionId: SessionId
      AnchorDigest: string
      Bundle: StrengthFrameBundle }

type StrengthDurabilityPort =
    { LoadProjection: unit -> Task<Result<StrengthProjection, string>>
      LoadFrameBundle: StrengthCandidatePrepared -> Task<Result<StrengthFrameBundle, string>>
      PublishPrepared: StrengthPreparedRequest -> Task<StrengthPreparedPublish>
      Append: StrengthEvent -> Task<StrengthDurableAppend> }
