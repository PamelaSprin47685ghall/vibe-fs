namespace Wanxiangshu.OpenCode

open Wanxiangshu.Domain
open Wanxiangshu.Kernel.Identity

[<RequireQualifiedAccess>]
type StrengthPreparedPublish =
    | Published
    | Rejected of reason: string
    | StorageInvalid of reason: string

/// STRENGTH-006..008: durable Strength capability exposed to Application/Host.
/// The port contains no storage identity. Persist owns EventStore, payload closure,
/// append outcomes and material codecs; callers only ask domain-level questions.
type StrengthDurabilityPort =
    { LoadProjection: unit -> Result<StrengthProjection, string>
      LoadFrameBundle: StrengthCandidatePrepared -> Result<StrengthFrameBundle, string>
      PublishPrepared:
        SessionId ->
        StrengthDecisionId ->
        ProviderRunIdentity ->
        SessionId ->
        StrengthBudget ->
        string ->
        StrengthFrameBundle ->
            StrengthPreparedPublish
      Append: StrengthEvent -> Result<unit, string> }
