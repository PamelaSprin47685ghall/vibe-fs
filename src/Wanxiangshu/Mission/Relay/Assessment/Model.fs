namespace Wanxiangshu.Mission.Relay.Assessment

open FsToolkit.ErrorHandling
open Fable.Core.JsInterop
open Wanxiangshu.Mission.Relay

module Model =
    let schemaJson =
        """{"type":"object","additionalProperties":false,"required":["language_algorithms","simplicity","structure","granularity","tests_evidence","logic_reliability_boundaries","caller_ergonomics","completeness"],"properties":{"language_algorithms":{"type":"string","enum":["PERFECT","REVISE","N/A"]},"simplicity":{"type":"string","enum":["PERFECT","REVISE","N/A"]},"structure":{"type":"string","enum":["PERFECT","REVISE","N/A"]},"granularity":{"type":"string","enum":["PERFECT","REVISE","N/A"]},"tests_evidence":{"type":"string","enum":["PERFECT","REVISE","N/A"]},"logic_reliability_boundaries":{"type":"string","enum":["PERFECT","REVISE","N/A"]},"caller_ergonomics":{"type":"string","enum":["PERFECT","REVISE","N/A"]},"completeness":{"type":"string","enum":["PERFECT","REVISE","N/A"]},"note":{"type":"string"}}}"""

    let private expectedFields = ScoreDimension.all |> List.map ScoreDimension.fieldName

    let private keys (value: obj) : string array =
        emitJsExpr value "Object.keys($0 ?? {})"

    let private property (value: obj) (name: string) : obj = emitJsExpr (value, name) "$0[$1]"

    let private grade (value: obj) : ScoreGrade option =
        let isString: bool = emitJsExpr value "typeof $0 === 'string'"

        if isString then
            ScoreGrade.tryParse (unbox<string> value) |> Result.toOption
        else
            None

    let private readScores value =
        expectedFields
        |> List.traverseResultM (fun field ->
            property value field
            |> grade
            |> Result.requireSome ("review score must be PERFECT, REVISE, or N/A: " + field))

    let private validateNote value =
        let hasNote: bool = emitJsExpr value "'note' in ($0 ?? {})"
        let isString: bool = emitJsExpr value "typeof ($0 ?? {})['note'] === 'string'"

        if not hasNote || isString then
            Ok()
        else
            Error "review note must be a string"

    let private validateKeys value =
        let actualKeys = keys value |> Set.ofArray
        let requiredSet = expectedFields |> Set.ofList
        let allowedSet = requiredSet |> Set.add "note"

        if not (Set.isSubset requiredSet actualKeys) then
            Error "review arguments must contain the eight required score fields"
        elif not (Set.isSubset actualKeys allowedSet) then
            Error "review arguments contain unexpected fields"
        else
            Ok()

    let tryParse (value: obj) =
        if isNull value || emitJsExpr value "typeof $0 !== 'object' || Array.isArray($0)" then
            Error "review arguments must be an object"
        else
            result {
                do! validateKeys value
                do! validateNote value
                let! grades = readScores value
                return! ScoreVector.tryCreate grades
            }
