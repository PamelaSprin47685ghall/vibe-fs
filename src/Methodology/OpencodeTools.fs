module VibeFs.Methodology.OpencodeTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Methodology.SchemaCommon
open VibeFs.Methodology.Args
open VibeFs.Methodology.Registry
open VibeFs.Kernel.Subagent
open VibeFs.Kernel.HostTools
open VibeFs.Opencode.ToolSchema
open VibeFs.Opencode.ToolHelpers
open VibeFs.Opencode.SessionIo
open VibeFs.Mux.Wrappers
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.Dyn

let private zodField (f: MethodologyField) : string * obj =
    match f.kind with
    | FieldKind.String -> f.name, strReq f.description
    | FieldKind.StringArray when f.required && f.minArrayItems > 0 ->
        f.name, arrayMin (strMin 1 "") f.minArrayItems f.description
    | FieldKind.StringArray -> f.name, strArrayOpt f.description

let private methodologyArgs (schema: MethodologySchema) : obj =
    createObj (schema.fields |> List.map zodField)

let private executeSchema (registry: ChildAgentRegistry) (ctx: obj) (schema: MethodologySchema) : obj -> obj -> JS.Promise<string> =
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
                let prompt = formatPrompt opencode (Meditator(intent, [])) |> List.head
                return!
                    runSubagent
                        registry
                        (get ctx "client")
                        "meditator"
                        "Methodology"
                        prompt
                        directory
                        sessionID
                        context
                        (box null)
        }

let methodologyTool (registry: ChildAgentRegistry) (ctx: obj) (schema: MethodologySchema) : obj =
    define schema.toolDescription (methodologyArgs schema) (executeSchema registry ctx schema)

let registerMethodologyTools (registry: ChildAgentRegistry) (ctx: obj) (target: obj) : unit =
    for schema in allSchemas do
        target?(schema.toolName) <- box (methodologyTool registry ctx schema)