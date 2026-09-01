namespace Wanxiangshu.Repository.Programming.Js

[<RequireQualifiedAccess>]
type AnchorSpec =
    | Exact of string
    | Regex of string

type AnchorDeclaration =
    { Spec: AnchorSpec
      Occurrence: int option }

module AnchorRules =
    val validateDeclaration: declaration: AnchorDeclaration -> Result<unit, JsFailure>
    val validateOccurrence: declaration: AnchorDeclaration -> Result<unit, JsFailure>
