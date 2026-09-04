namespace Wanxiangshu.Mission.Relay.Assessment

open Fable.Core.JsInterop
open Wanxiangshu.Mission.Relay

module Surface =
    let schemaJson = Model.schemaJson

    let private scoreObject scores =
        ScoreDimension.all
        |> List.map (fun dimension -> ScoreDimension.fieldName dimension ==> ScoreVector.score dimension scores)
        |> createObj

    let parse (value: obj) =
        match Model.tryParse value with
        | Error error -> box {| ok = false; error = error |}
        | Ok scores ->
            box
                {| ok = true
                   scores = scoreObject scores
                   allPerfect = ScoreVector.allPerfect scores
                   lowDimensions =
                    scores
                    |> ScoreVector.lowDimensions
                    |> List.map ScoreDimension.fieldName
                    |> List.toArray |}

