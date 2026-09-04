namespace Wanxiangshu.Mission.Relay.Assessment

open FsToolkit.ErrorHandling
open Fable.Core.JsInterop
open Wanxiangshu.Mission.Relay

module Model =
    let schemaJson =
        """{"type":"object","additionalProperties":false,"required":["language_algorithms","simplicity","structure","granularity","tests_evidence","logic_reliability_boundaries","caller_ergonomics","completeness"],"properties":{"language_algorithms":{"type":"integer","minimum":0,"maximum":10},"simplicity":{"type":"integer","minimum":0,"maximum":10},"structure":{"type":"integer","minimum":0,"maximum":10},"granularity":{"type":"integer","minimum":0,"maximum":10},"tests_evidence":{"type":"integer","minimum":0,"maximum":10},"logic_reliability_boundaries":{"type":"integer","minimum":0,"maximum":10},"caller_ergonomics":{"type":"integer","minimum":0,"maximum":10},"completeness":{"type":"integer","minimum":0,"maximum":10}}}"""

    let private expectedFields = ScoreDimension.all |> List.map ScoreDimension.fieldName

    let private keys (value: obj) : string array = emitJsExpr value "Object.keys($0 ?? {})"
    let private property (value: obj) (name: string) : obj = emitJsExpr (value, name) "$0[$1]"

    let private integer (value: obj) : int option =
        let isInteger: bool = emitJsExpr value "Number.isInteger($0)"

        if isInteger then
            let number: int = unbox value
            if number >= 0 && number <= 10 then Some number else None
        else
            None

    let private readScores value =
        expectedFields
        |> List.traverseResultM (fun field ->
            property value field
            |> integer
            |> Result.requireSome ("review score must be an integer from 0 through 10: " + field))

    let tryParse (value: obj) =
        if isNull value then
            Error "review arguments must be an object"
        elif keys value |> Set.ofArray <> (expectedFields |> Set.ofList) then
            Error "review arguments must contain exactly the eight required score fields"
        else
            readScores value |> Result.bind ScoreVector.tryCreate

