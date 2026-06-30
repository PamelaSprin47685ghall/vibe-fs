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

let private methodologyParameters : JsonSchema =
    mkSchema
        (createObj
            [ "methodology", box (strEnumProp "Select which methodology to apply." enumValuesArray)
              "intent", box (strProp intentFieldDescription)
              "background", box (strProp backgroundFieldDescription)
              "note", box (strProp unifiedNoteDescription) ])
        [| "methodology"; "intent"; "background"; "note" |]

let private executeMethodology (deps: obj) (toolNames: string array) : obj -> obj -> JS.Promise<string> =
    fun config args ->
        promise {
            match parse args with
            | Error message -> return "Error: " + message
            | Ok parsed ->
                match strField config "workspaceId" with
                | None -> return "Methodology notebook requires workspaceId"
                | Some _ ->
                    match tryFindEntry parsed.methodology with
                    | None -> return "Error: unknown methodology: " + parsed.methodology
                    | Some entry ->
                        let intent = renderMeditatorIntent entry parsed.intent parsed.note
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

let methodologyTool (deps: obj) (toolNames: string array) : ToolDefinition =
    { name = unifiedToolName
      description = unifiedToolDescription
      parameters = methodologyParameters
      execute = executeMethodology deps toolNames
      condition = None }

let methodologyToolNames : string array =
    [| unifiedToolName |]
