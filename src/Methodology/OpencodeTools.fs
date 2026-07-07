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

let private methodologyArgs : obj =
    box {|
        methodology = enumReq enumValuesArray.Value "Select which methodology to apply."
        intent = strReq intentFieldDescription
        background = strReq backgroundFieldDescription
        note = strReq unifiedNoteDescription.Value
    |}

let private executeMethodology (host: Host) (registry: ChildAgentRegistry) (ctx: obj) (runtime: FallbackRuntimeState) : obj -> obj -> JS.Promise<string> =
    fun args context ->
        promise {
            match parse args with
            | Error message -> return "Error: " + message
            | Ok parsed ->
                match tryFindEntry parsed.methodology with
                | None -> return "Error: unknown methodology: " + parsed.methodology
                | Some entry ->
                    let intent = renderMeditatorIntent entry parsed.intent parsed.note
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

let methodologyTool (host: Host) (registry: ChildAgentRegistry) (ctx: obj) (runtime: FallbackRuntimeState) : obj =
    define unifiedToolDescription methodologyArgs (executeMethodology host registry ctx runtime)

let registerMethodologyTools (registry: ChildAgentRegistry) (ctx: obj) (host: Host) (runtime: FallbackRuntimeState) (target: obj) : unit =
    target?(unifiedToolName) <- box (methodologyTool host registry ctx runtime)
