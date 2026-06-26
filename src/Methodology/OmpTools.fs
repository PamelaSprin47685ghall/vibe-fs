module Wanxiangshu.Methodology.OmpTools

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Methodology.SchemaCommon
open Wanxiangshu.Methodology.Args
open Wanxiangshu.Methodology.Registry
open Wanxiangshu.Kernel.Subagent
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Omp.ChildSession
open Wanxiangshu.Omp.Codec
open Wanxiangshu.Omp.OmpToolSchema
open Wanxiangshu.Shell.Dyn

module Dyn = Wanxiangshu.Shell.Dyn

let private executeSchema (pi: obj) (schema: MethodologySchema) =
    fun (_id: string) (params': obj) (signal: obj) (_u: obj) (ctx: obj) ->
        promise {
            match parse schema params' with
            | Error message -> return errorResult ("Error: " + message)
            | Ok (values, arrays) ->
                let yaml = renderInputYaml schema values arrays
                let intent = renderMeditatorIntent schema yaml
                let prompt = formatPrompt omp (Meditator(intent, [])) |> List.head
                try
                    let! text = runSubagent pi ctx [||] prompt (Some signal)
                    return textResult text
                with ex -> return asErrorResult ex
        }

let registerMethodologyTools (pi: obj) : unit =
    let tb = Dyn.get pi "typebox"
    for schema in allSchemas do
        pi?registerTool(
            createObj [
                "name", box schema.toolName
                "label", box schema.toolName
                "description", box schema.toolDescription
                "parameters", methodologyParameters schema tb
                "execute", box (executeSchema pi schema)
            ])