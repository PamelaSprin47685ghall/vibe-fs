namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure.Resources
open Wanxiangshu.Kernel

[<RequireQualifiedAccess>]
module PairProgrammingCalibration =

    [<Literal>]
    let ToolEstimatePath = "host/pair-programming-tool-estimate"

    [<Literal>]
    let ElapsedPath = "host/pair-programming-elapsed"

    let private nonBlank value =
        value
        |> Option.map (fun text -> text.Trim())
        |> Option.filter (String.IsNullOrWhiteSpace >> not)

    let private join fragments =
        fragments |> List.map nonBlank |> List.choose id |> String.concat "\n\n"

    let compose tip toolEstimate guideline =
        join [ tip; toolEstimate; Some guideline ]

    let composeWithElapsed tip elapsed toolEstimate guideline =
        join [ tip; elapsed; toolEstimate; Some guideline ]

    let renderToolEstimate language remaining =
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
        ProviderProse.render
            language
            ElapsedPath
            (Map [ "elapsed", elapsedLabel language elapsedMilliseconds ])
