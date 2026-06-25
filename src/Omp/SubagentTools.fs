module VibeFs.Omp.SubagentTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Subagent
open VibeFs.Kernel.SubagentPrompts
open VibeFs.Kernel.ToolCatalog
open VibeFs.Omp.ChildSession
open VibeFs.Omp.Codec
open VibeFs.Omp.OmpToolSchema
open VibeFs.Shell.SubagentIntentsCodec
open VibeFs.Shell.WorkspaceFiles
module Dyn = VibeFs.Shell.Dyn

let private coderChildTools = [| "read"; "edit"; "write"; "find"; "fuzzy_find"; "fuzzy_grep"; "lsp" |]
let private investigatorChildTools = [| "read"; "find"; "fuzzy_find"; "fuzzy_grep" |]

let registerSubagentTools (pi: obj) : unit =
    let tb = Dyn.get pi "typebox"

    pi?registerTool(
        createObj [
            "name", box "coder"
            "label", box "Coder"
            "description", box (description "coder")
            "parameters", coderParameters tb
            "execute",
                box(fun (_id: string) (params': obj) (signal: obj) (_u: obj) (ctx: obj) ->
                    promise {
                        match parseCoderIntents (Dyn.get params' "intents") with
                        | Error message -> return errorResult message
                        | Ok intents ->
                            try
                                let prompts = formatPrompt omp (Coder intents)
                                let! reports =
                                    prompts
                                    |> List.map (fun prompt -> runSubagent pi ctx coderChildTools prompt (Some signal))
                                    |> Promise.all
                                return textResult (joinReports reports)
                            with ex -> return asErrorResult ex
                    })
        ])

    pi?registerTool(
        createObj [
            "name", box "investigator"
            "label", box "Investigator"
            "description", box (description "investigator")
            "parameters", investigatorParameters tb
            "execute",
                box(fun (_id: string) (params': obj) (signal: obj) (_u: obj) (ctx: obj) ->
                    promise {
                        match parseInvestigatorIntents (Dyn.get params' "intents") with
                        | Error message -> return errorResult message
                        | Ok intents ->
                            try
                                let prompts = formatPrompt omp (Investigator intents)
                                let! reports =
                                    prompts
                                    |> List.map (fun prompt -> runSubagent pi ctx investigatorChildTools prompt (Some signal))
                                    |> Promise.all
                                return textResult (joinReports reports)
                            with ex -> return asErrorResult ex
                    })
        ])

    pi?registerTool(
        createObj [
            "name", box "meditator"
            "label", box "Meditator"
            "description", box (description "meditator")
            "parameters", meditatorParameters tb
            "execute",
                box(fun (_id: string) (params': obj) (signal: obj) (_u: obj) (ctx: obj) ->
                    promise {
                        try
                            let cwd = Dyn.str ctx "cwd"
                            let files =
                                let f = Dyn.get params' "files"
                                if Dyn.isNullish f || not (Dyn.isArray f) then [||]
                                else unbox<obj array> f |> Array.map string
                            let! readResults = readReverieFiles cwd (List.ofArray files)
                            let sections =
                                Array.map2
                                    (fun file (r: ReverieFileResult) -> { file = file; content = r.content })
                                    files
                                    (List.toArray readResults)
                                |> Array.toList
                            let intent = Dyn.str params' "intent"
                            let prompt = formatPrompt omp (Meditator(intent, sections)) |> List.head
                            let! text = runSubagent pi ctx [||] prompt (Some signal)
                            return textResult text
                        with ex -> return asErrorResult ex
                    })
        ])

    pi?registerTool(
        createObj [
            "name", box "browser"
            "label", box "Browser"
            "description", box (description "browser")
            "parameters", browserParameters tb
            "execute",
                box(fun (_id: string) (params': obj) (signal: obj) (_u: obj) (ctx: obj) ->
                    promise {
                        try
                            let getAll = Dyn.get pi "getAllTools"
                            let hasBrowser =
                                Dyn.typeIs getAll "function"
                                && (unbox<obj array> (Dyn.call0 getAll) |> Array.exists (fun t -> string t = "browser"))
                            if not hasBrowser then
                                return errorResult "Built-in browser tool is unavailable in this session."
                            else
                                let intent = Dyn.str params' "intent"
                                let prompt = formatPrompt omp (Browser intent) |> List.head
                                let! text = runSubagent pi ctx [| "browser" |] prompt (Some signal)
                                return textResult text
                        with ex -> return asErrorResult ex
                    })
        ])