module Wanxiangshu.Methodology.MuxTools

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Methodology.SchemaCommon
open Wanxiangshu.Methodology.Args
open Wanxiangshu.Methodology.Registry
open Wanxiangshu.Kernel.Subagent
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Mux.Delegate
open Wanxiangshu.Mux.SubagentTools
open Wanxiangshu.Mux.Wrappers
open Wanxiangshu.Shell.Dyn

let private fieldProperty (f: MethodologyField) : string * obj =
    let desc = f.description
    match f.kind, f.required, f.minArrayItems with
    | FieldKind.String, _, _ -> f.name, strProp desc
    | FieldKind.StringArray, true, n when n > 0 ->
        f.name,
        createObj
            [ "type", box "array"
              "minItems", box n
              "items", createObj [ "type", box "string"; "minLength", box 1 ]
              "description", box desc ]
    | FieldKind.StringArray, _, _ -> f.name, strArrayProp desc

let private methodologyParameters (schema: MethodologySchema) : JsonSchema =
    let required =
        schema.fields |> List.filter (fun f -> f.required) |> List.map (fun f -> f.name) |> Array.ofList
    mkSchema (createObj (schema.fields |> List.map fieldProperty)) required

let private meditatorPrompt (schema: MethodologySchema) (values: Map<string, string>) (arrays: Map<string, string list>) =
    let yaml = renderInputYaml schema values arrays
    renderMeditatorIntent schema yaml

let private executeSchema (deps: obj) (toolNames: string array) (schema: MethodologySchema) : obj -> obj -> JS.Promise<string> =
    fun config args ->
        promise {
            match parse schema args with
            | Error message -> return "Error: " + message
            | Ok (values, arrays) ->
                match strField config "workspaceId" with
                | None -> return "Methodology notebook requires workspaceId"
                | Some _ ->
                    let intent = meditatorPrompt schema values arrays
                    let prompt = formatPrompt Host.Mimocode (Meditator(intent, [])) |> List.head
                    return!
                        runMuxSubagent
                            deps
                            config
                            "explore"
                            prompt
                            "Methodology"
                            (toolOptions toolNames "exec" "meditator")
        }

let methodologyTool (deps: obj) (toolNames: string array) (schema: MethodologySchema) : ToolDefinition =
    { name = schema.toolName
      description = schema.toolDescription
      parameters = methodologyParameters schema
      execute = executeSchema deps toolNames schema
      condition = None }

let allMethodologyTools (deps: obj) (toolNames: string array) : ToolDefinition array =
    allSchemas |> List.map (methodologyTool deps toolNames) |> Array.ofList

let methodologyToolNames : string array =
    allSchemas |> List.map (fun s -> s.toolName) |> Array.ofList