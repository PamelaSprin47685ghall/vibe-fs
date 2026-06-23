module VibeFs.Opencode.SubagentTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Shell

open VibeFs.Kernel.HostTools
open VibeFs.Kernel.SubagentPrompts
open VibeFs.Kernel.Subagent
open VibeFs.Shell.SubagentIntentsCodec
open VibeFs.Kernel.ToolCatalog
open VibeFs.Opencode.ToolSchema
open VibeFs.Opencode.SessionIo
open VibeFs.Opencode.ToolHelpers
open VibeFs.Mux.Wrappers
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.Dyn

let coderTool (registry: ChildAgentRegistry) (ctx: obj) : obj =
    let client () = Dyn.get ctx "client"
    define coder
        (box {| intents = coderIntentsSchema Params.coderIntents; tdd = enumReq [| "red"; "green" |] Params.coderTdd; _ui = uiParam |})
        (fun args context ->
            match parseCoderIntents (Dyn.get args "intents") with
            | Error message -> resolveStr message
            | Ok intents ->
                let tc = extractToolContext context (Dyn.str ctx "directory")
                let directory = Dyn.str tc "directory"
                let sessionID = Dyn.str tc "sessionID"
                let prompts = formatPrompt opencode (Coder intents)
                promise {
                    let! reports =
                        prompts
                        |> List.map (fun prompt ->
                            runSubagent
                                registry
                                (client ())
                                "coder"
                                "Coder"
                                prompt
                                directory
                                sessionID
                                context
                                (box null))
                        |> Promise.all
                    return joinReports reports
                })

let investigatorTool (registry: ChildAgentRegistry) (ctx: obj) : obj =
    let client () = Dyn.get ctx "client"
    define investigator
        (box {| intents = investigatorIntentsSchema Params.investigatorIntents
                _ui = uiParam |})
        (fun args context ->
            match parseInvestigatorIntents (Dyn.get args "intents") with
            | Error message -> resolveStr message
            | Ok intents ->
                let tc = extractToolContext context (Dyn.str ctx "directory")
                let prompts = formatPrompt opencode (Investigator intents)
                promise {
                    let! reports =
                        prompts
                        |> List.map (fun prompt ->
                            runSubagent
                                registry
                                (client ())
                                "investigator"
                                "Investigator"
                                prompt
                                (Dyn.str tc "directory")
                                (Dyn.str tc "sessionID")
                                context
                                (box null))
                        |> Promise.all
                    return joinReports reports
                })

let meditatorTool (registry: ChildAgentRegistry) (ctx: obj) : obj =
    let client () = Dyn.get ctx "client"
    define meditator
        (box {| intent = strReq Params.meditatorIntent; files = strArrayOpt Params.meditatorFiles |})
        (fun args context ->
            let tc = extractToolContext context (Dyn.str ctx "directory")
            let directory = Dyn.str tc "directory"
            let sessionID = Dyn.str tc "sessionID"
            let intent = Dyn.str args "intent"
            let files = if Dyn.isNullish (Dyn.get args "files") then [||] else Dyn.get args "files" :?> obj array |> Array.map string
            promise {
                let! readResults = VibeFs.Shell.WorkspaceFiles.readReverieFiles directory (List.ofArray files)
                let sections =
                    Array.map2 (fun file (r: VibeFs.Shell.WorkspaceFiles.ReverieFileResult) ->
                        { file = file; content = r.content } : MeditatorFileSection)
                        files (List.toArray readResults)
                    |> List.ofArray
                let prompt = formatPrompt opencode (Meditator(intent, sections)) |> List.head
                return! runSubagent registry (client ()) "meditator" "Meditator" prompt
                    directory sessionID context (box null)
            })

let browserTool (registry: ChildAgentRegistry) (ctx: obj) : obj =
    let client () = Dyn.get ctx "client"
    define browser
        (box {| intent = strReq Params.browserIntent |})
        (fun args context ->
            let tc = extractToolContext context (Dyn.str ctx "directory")
            runSubagent registry (client ()) "browser" "Browser" (formatPrompt opencode (Browser(Dyn.str args "intent")) |> List.head)
                (Dyn.str tc "directory") (Dyn.str tc "sessionID") context (box null))
