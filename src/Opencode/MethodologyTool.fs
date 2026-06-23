module VibeFs.Opencode.MethodologyTool

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.Methodology
open VibeFs.Opencode.ToolSchema

let selectMethodologyTool () : obj =
    define methodologyCatalog
        (box {| methods = enumArrayMin (List.toArray methodologyEnumValues) 1 "One or more reasoning methodology names from the catalog."
                reason = strReq "Concise reasoning for applying the selected methodologies." |})
        (fun _ _ -> Promise.lift methodologyToolResultText)
