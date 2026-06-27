module Wanxiangshu.Methodology.OpencodeTools

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Methodology.SchemaCommon
open Wanxiangshu.Methodology.Args
open Wanxiangshu.Methodology.Registry
open Wanxiangshu.Kernel.Subagent
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Opencode.ToolSchema
open Wanxiangshu.Opencode.ToolHelpers
open Wanxiangshu.Opencode.SessionIo
open Wanxiangshu.Mux.Wrappers
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Shell.FallbackRuntimeState

let private zodField (f: MethodologyField) : string * obj =
    match f.kind with
    | FieldKind.String -> f.name, strReq f.description
    | FieldKind.StringArray when f.required && f.minArrayItems > 0 ->
        f.name, arrayMin (strMin 1 "") f.minArrayItems f.description
    | FieldKind.StringArray -> f.name, strArrayOpt f.description

let private methodologyArgs (schema: MethodologySchema) : obj =
    createObj (schema.fields |> List.map zodField)

let private executeSchema (host: Host) (registry: ChildAgentRegistry) (ctx: obj) (runtime: FallbackRuntimeState) (schema: MethodologySchema) : obj -> obj -> JS.Promise<string> =
    fun args context ->
        promise {
            match parse schema args with
            | Error message -> return "Error: " + message
            | Ok (values, arrays) ->
                let yaml = renderInputYaml schema values arrays
                let intent = renderMeditatorIntent schema yaml
                let tc = extractToolContext context (str ctx "directory")
                let directory = str tc "directory"
                let sessionID = str tc "sessionID"
                let prompt = formatPrompt host (Meditator(intent, [])) |> List.head
                let! subResult =
                    runSubagent
                        runtime
                        registry
                        (get ctx "client")
                        "meditator"
                        "Methodology"
                        prompt
                        directory
                        sessionID
                        context
                        (box null)
                match subResult with
                | Ok text -> return text
                | Error err -> return wireEncodeToolError "meditator" err
        }

let methodologyTool (host: Host) (registry: ChildAgentRegistry) (ctx: obj) (runtime: FallbackRuntimeState) (schema: MethodologySchema) : obj =
    define schema.toolDescription (methodologyArgs schema) (executeSchema host registry ctx runtime schema)

let registerMethodologyTools (registry: ChildAgentRegistry) (ctx: obj) (host: Host) (runtime: FallbackRuntimeState) (target: obj) : unit =
    for schema in allSchemas do
        target?(schema.toolName) <- box (methodologyTool host registry ctx runtime schema)
