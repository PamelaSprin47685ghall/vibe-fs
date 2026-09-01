namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Foundation
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Resources

[<RequireQualifiedAccess>]
module PairProgrammingCalibration =

    [<Literal>]
    let ToolEstimatePath = "host/pair-programming-tool-estimate"

    [<Literal>]
    let ElapsedPath = "host/pair-programming-elapsed"

    let private nonBlank (value: string option) =
        value
        |> Option.map (fun text -> text.Trim())
        |> Option.filter (String.IsNullOrWhiteSpace >> not)

    let private instructionTexts fragments =
        fragments |> List.map nonBlank |> List.choose id

    let document tip toolEstimate guideline =
        LlmFacing.instructions (instructionTexts [ tip; toolEstimate; Some guideline ])

    let documentWithElapsed tip elapsed toolEstimate guideline =
        LlmFacing.instructions (instructionTexts [ tip; elapsed; toolEstimate; Some guideline ])

    let compose tip toolEstimate guideline =
        document tip toolEstimate guideline |> LlmFacing.render

    let composeWithElapsed tip elapsed toolEstimate guideline =
        documentWithElapsed tip elapsed toolEstimate guideline |> LlmFacing.render

    let renderToolEstimate language (remaining: int) =
        ProviderProse.render language ToolEstimatePath (Map [ "remaining", string remaining ])

    let private elapsedLabel language elapsedMilliseconds =
        let totalSeconds =
            elapsedMilliseconds
            |> max 0.0
            |> fun value -> Math.Floor(value / 1000.0)
            |> int64

        let minutes = totalSeconds / 60L
        let seconds = totalSeconds % 60L

        match language with
        | ProviderLanguage.SimplifiedChinese -> sprintf "%d 分钟 %d 秒" minutes seconds
        | ProviderLanguage.English ->
            let minuteUnit = if minutes = 1L then "minute" else "minutes"
            let secondUnit = if seconds = 1L then "second" else "seconds"
            sprintf "%d %s %d %s" minutes minuteUnit seconds secondUnit

    let renderElapsed language elapsedMilliseconds =
        ProviderProse.render language ElapsedPath (Map [ "elapsed", elapsedLabel language elapsedMilliseconds ])
