namespace Wanxiangshu.Participant.Provider

open System

/// The one owner of the human duration vocabulary. Every LLM-facing
/// calibration line — the per-round Pair Hint and the delegation horizon —
/// states elapsed and estimated time through this label.
[<RequireQualifiedAccess>]
module DurationLabel =

    /// Minutes and seconds, clamped at zero, in the bound language.
    let format (language: ProviderLanguage) (milliseconds: float) : string =
        let totalSeconds =
            milliseconds |> max 0.0 |> (fun value -> Math.Floor(value / 1000.0)) |> int64

        let minutes = totalSeconds / 60L
        let seconds = totalSeconds % 60L

        match language with
        | ProviderLanguage.SimplifiedChinese -> sprintf "%d 分钟 %d 秒" minutes seconds
        | ProviderLanguage.English ->
            let minuteUnit = if minutes = 1L then "minute" else "minutes"
            let secondUnit = if seconds = 1L then "second" else "seconds"
            sprintf "%d %s %d %s" minutes minuteUnit seconds secondUnit
