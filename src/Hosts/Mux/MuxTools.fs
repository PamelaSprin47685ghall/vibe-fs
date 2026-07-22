module Wanxiangshu.Hosts.Mux.MuxTools

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Methodology.Schema
open Wanxiangshu.Runtime.MethodologyArgs
open Wanxiangshu.Kernel.Methodology.Registry
open Wanxiangshu.Runtime.Subagent
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Hosts.Mux.Delegate
open Wanxiangshu.Hosts.Mux.SubagentTools
open Wanxiangshu.Hosts.Mux.Wrappers
open Wanxiangshu.Runtime.Dyn

let private methodologyParameters: JsonSchema =
    mkSchema
        (createObj
            [ "methodology", box (strEnumProp "Select which methodology to apply." enumValuesArray.Value)
              "intent", box (strProp intentFieldDescription)
              "background", box (strProp backgroundFieldDescription)
              "note", box (strProp unifiedNoteDescription.Value) ])
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
                        let intent = Wanxiangshu.Runtime.SubagentPrompts.renderMeditatorIntent entry parsed.intent parsed.background parsed.note
                        let prompt = formatPrompt Host.Mimocode (Meditator intent) |> List.head

                        return!
                            runMuxSubagent
                                deps
                                config
                                "explore"
                                prompt
                                "Methodology"
                                (toolOptions toolNames "exec" "meditator")
        }

let meditatorTool (deps: obj) (toolNames: string array) : ToolDefinition =
    { name = unifiedToolName
      description = unifiedToolDescription
      parameters = methodologyParameters
      execute = executeMethodology deps toolNames
      condition = None }

let meditatorToolNames: string array = [| unifiedToolName |]
