namespace Wanxiangshu.Participant.Provider

/// The one owner of the human duration vocabulary used by every LLM-facing
/// calibration line.
[<RequireQualifiedAccess>]
module DurationLabel =
    val format: language: ProviderLanguage -> milliseconds: float -> string
