module VibeFs.MuxPlugin.MuxTools.AgentTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Prompts
open VibeFs.Kernel.ToolPolicy
open VibeFs.Mux.Contract
open VibeFs.MuxPlugin.Delegate
open VibeFs.MuxPlugin.MuxPrompts
open VibeFs.MuxPlugin.MuxTools.Shared
open VibeFs.Opencode.Core

[<Global>]
type AbortController() =
    member _.signal: obj = jsNative
    member _.abort(): unit = jsNative

let private experimentsFor (role: string) : obj =
    let disabled = deniedTools role (Array.toList registeredToolNames) |> Array.ofList
    createObj [
        "subagentRole", box role
        "toolPolicy", box (createObj [ "disabledTools", box disabled ])
    ]

let private optionsFor (aiSettingsAgentId: string) (role: string) : obj =
    createObj [ "experiments", experimentsFor role; "aiSettingsAgentId", box aiSettingsAgentId ]

let private requireWorkspace (config: obj) (toolName: string) : string option =
    match strField config "workspaceId" with
    | None -> Some $"{toolName} requires workspaceId"
    | Some _ -> None

let private joinReports (reports: string array) : string =
    reports |> Array.map (fun r -> r.Trim()) |> String.concat "\n\n"

let private abortableConfig (config: obj) (signal: obj) = Dyn.withKey config "abortSignal" signal

module Tool =
    let bind
        (deps: obj)
        (agentId: string)
        (title: string)
        (aiSettingsAgentId: string)
        (role: string)
        (buildPrompt: obj -> obj -> Async<string>)
        : obj -> obj -> JS.Promise<string> =
        fun config args ->
            async {
                match requireWorkspace config title with
                | Some err -> return err
                | None ->
                    let! prompt = buildPrompt config args
                    let opts = Some (optionsFor aiSettingsAgentId role)
                    return!
                        runMuxSubagent deps config agentId prompt title opts
                        |> Async.AwaitPromise
            }
            |> Async.StartAsPromise

    let bindParallel
        (deps: obj)
        (agentId: string)
        (title: string)
        (aiSettingsAgentId: string)
        (role: string)
        (buildPrompts: obj -> obj -> Async<string array>)
        : obj -> obj -> JS.Promise<string> =
        fun config args ->
            async {
                match requireWorkspace config title with
                | Some err -> return err
                | None ->
                    let! prompts = buildPrompts config args
                    if prompts.Length = 0 then
                        return "Error: `intents` must be a non-empty array."
                    else
                        let controller = AbortController()
                        let opts = Some (optionsFor aiSettingsAgentId role)
                        let! reports =
                            prompts
                            |> Array.map (fun prompt ->
                                async {
                                    try
                                        let! r = runMuxSubagent deps (abortableConfig config controller.signal) agentId prompt title opts |> Async.AwaitPromise
                                        return Some r
                                    with _ ->
                                        controller.abort()
                                        return None
                                })
                            |> Async.Parallel
                        return joinReports (reports |> Array.choose id)
            }
            |> Async.StartAsPromise

let private buildCoderPrompts (_config: obj) (args: obj) : Async<string array> =
    async {
        let intents = Dyn.get args "intents"
        if Dyn.isNullish intents || not (Dyn.isArray intents) then return [||]
        else
            let intentsArr = intents :?> obj array
            if intentsArr.Length = 0 then return [||]
            else
                return
                    intentsArr
                    |> Array.map (fun intent ->
                        let pair = intent :?> obj array
                        let intentText = string pair.[0]
                        let files =
                            let filesRaw = pair.[1]
                            if Dyn.typeIs filesRaw "string" then [string filesRaw]
                            else filesRaw :?> obj array |> Array.map string |> List.ofArray
                        formatMuxCoderUserPrompt intentText files)
    }

let coderTool (deps: obj) : ToolDefinition =
    let prefixItems = [| strProp "The code-change intent."; strArrayProp "The list of affected files." |]
    let itemSchema =
        createObj [ "type", box "array"; "minItems", box 2; "maxItems", box 2; "prefixItems", box prefixItems ]
    let intentsSchema =
        createObj [ "type", box "array"; "items", box itemSchema; "description", box Params.coderIntents ]
    { name = "coder"
      description = coder
      parameters = mkSchema (createObj [ "intents", box intentsSchema ]) [| "intents" |]
      execute = Tool.bindParallel deps "exec" "Coder" "exec" "coder" buildCoderPrompts
      condition = None }

let private buildReaderPrompts (_config: obj) (args: obj) : Async<string array> =
    async {
        let intents = requireStrArray args "intents"
        if Array.isEmpty intents then return [||]
        else return intents |> Array.map formatMuxReaderUserPrompt
    }

let readerTool (deps: obj) : ToolDefinition =
    { name = "reader"
      description = reader
      parameters = mkSchema (createObj [ "intents", box (strArrayProp Params.readerIntents) ]) [| "intents" |]
      execute = Tool.bindParallel deps "explore" "Reader" "explore" "reader" buildReaderPrompts
      condition = None }

let private meditatorPromptFromArgs (config: obj) (args: obj) : Async<string> =
    async {
        let intent = defaultArg (strField args "intent") ""
        let files = requireStrArray args "files" |> List.ofArray
        let cwd = defaultArg (strField config "directory") ""
        let! results = VibeFs.Shell.ReverieFiles.readReverieFiles cwd files |> Async.AwaitPromise
        let sections =
            results
            |> List.map (fun r -> { file = r.filePath; content = r.content } : MeditatorFileSection)
        let skipped = "(skipped)"
        let rendered =
            sections
            |> List.map (fun s -> $"=== {s.file} ===\n\n{Option.defaultValue skipped s.content}")
        let body = rendered |> String.concat "\n\n"
        let basePrompt = formatMuxMeditatorUserPrompt intent (sections |> List.map (fun s -> s.file))
        return if body = "" then basePrompt else $"{body}\n\n{basePrompt}"
    }

let meditatorTool (deps: obj) : ToolDefinition =
    { name = "meditator"
      description = meditator
      parameters =
        mkSchema
            (createObj [ "intent", box (strProp Params.meditatorIntent); "files", box (strArrayProp Params.meditatorFiles) ])
            [| "intent"; "files" |]
      execute = Tool.bind deps "explore" "Meditator" "exec" "meditator" meditatorPromptFromArgs
      condition = None }

let private buildBrowserPrompt (_config: obj) (args: obj) : Async<string> =
    async {
        let intent = defaultArg (strField args "intent") ""
        return formatMuxBrowserUserPrompt intent
    }

let browserTool (deps: obj) : ToolDefinition =
    { name = "browser"
      description = browser
      parameters = mkSchema (createObj [ "intent", box (strProp Params.browserIntent) ]) [| "intent" |]
      execute = Tool.bind deps "explore" "Browser" "explore" "browser" buildBrowserPrompt
      condition = None }
