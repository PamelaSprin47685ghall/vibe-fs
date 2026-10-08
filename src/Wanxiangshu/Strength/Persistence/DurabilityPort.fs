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
type StrengthAppendException(eventId: EventId, failure: AppendError, cleanupFailures: exn list) =
    inherit System.Exception(AppendError.describe failure, AppendError.cause failure |> Option.toObj)
    member _.EventId = eventId
    member _.Failure = failure
    member _.CleanupFailures = cleanupFailures

type StrengthPreparedRequest =
    { OwnerSessionId: SessionId
      DecisionId: StrengthDecisionId
      TargetProviderRun: ProviderRunIdentity
      ReplicaSessionId: SessionId
      AnchorDigest: string
      Bundle: StrengthFrameBundle }

/// STRENGTH-006..008: durable Strength capability exposed to Application/Host.
/// The port contains no storage identity. Persist owns EventStore, payload closure,
/// append outcomes and material codecs; callers only ask domain-level questions.
type StrengthDurabilityPort =
    { LoadProjection: unit -> Task<Result<StrengthProjection, string>>
      LoadFrameBundle: StrengthCandidatePrepared -> Task<Result<StrengthFrameBundle, string>>
      PublishPrepared: StrengthPreparedRequest -> Task<StrengthPreparedPublish>
      Append: StrengthEvent -> Task<StrengthDurableAppend> }
