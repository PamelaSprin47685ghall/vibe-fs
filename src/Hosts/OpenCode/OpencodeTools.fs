module Wanxiangshu.Hosts.Opencode.OpencodeTools

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Methodology.Schema
open Wanxiangshu.Runtime.MethodologyArgs
open Wanxiangshu.Kernel.Methodology.Registry
open Wanxiangshu.Runtime.Subagent
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Hosts.Opencode.ToolSchema
open Wanxiangshu.Hosts.Opencode.SessionIo
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Runtime.Fallback.RuntimeStore

let private methodologyArgs: obj =
    createObj
        [ "methodology", enumReq enumValuesArray.Value "Select which methodology to apply."
          "intent", strReq intentFieldDescription
          "background", strReq backgroundFieldDescription
          "note", strReq unifiedNoteDescription.Value
          "not-suitable-via-continue-tool", warnNotSuitableViaContinueToolParam ]

let private executeMethodology
    (host: Host)
    (registry: ChildAgentRegistry)
    (ctx: obj)
    (runtime: FallbackRuntimeStore)
    : obj -> obj -> JS.Promise<string> =
    fun args context ->
        promise {
            match parse args with
            | Error message -> return "Error: " + message
            | Ok parsed ->
                match tryFindEntry parsed.methodology with
                | None -> return "Error: unknown methodology: " + parsed.methodology
                | Some entry ->
                    let intent = Wanxiangshu.Runtime.SubagentPrompts.renderMeditatorIntent entry parsed.intent parsed.background parsed.note
                    let tc = extractToolContext context (str ctx "directory")
                    let directory = str tc "directory"
                    let sessionID = str tc "sessionID"
                    let prompt = formatPrompt host (Meditator intent) |> List.head

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

let meditatorTool (host: Host) (registry: ChildAgentRegistry) (ctx: obj) (runtime: FallbackRuntimeStore) : obj =
    define unifiedToolDescription methodologyArgs (executeMethodology host registry ctx runtime)

let registerMeditatorTools
    (registry: ChildAgentRegistry)
    (ctx: obj)
    (host: Host)
    (runtime: FallbackRuntimeStore)
    (target: obj)
    : unit =
    target?(unifiedToolName) <- box (meditatorTool host registry ctx runtime)
