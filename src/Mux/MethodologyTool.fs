module VibeFs.Mux.MethodologyTool

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.Methodology
open VibeFs.Mux.Wrappers

let private enumArrayProp (desc: string) (values: string array) : obj =
    createObj [
        "type", box "array"
        "items", box (createObj [ "type", box "string"; "enum", box values ])
        "minItems", box 1
        "description", box desc
    ]

let selectMethodologyTool : ToolDefinition =
    { name = selectMethodologyToolName
      description = methodologyCatalog
      parameters =
          mkSchema
              (createObj [
                  "methods", box (enumArrayProp "One or more reasoning methodology names from the catalog." (List.toArray methodologyEnumValues))
                  "plan", box (strProp "Concise execution plan for applying the selected methodologies.")
              ])
              [| "methods"; "plan" |]
      execute = fun _ _ -> resolveStr methodologyToolResultText
      condition = None }
