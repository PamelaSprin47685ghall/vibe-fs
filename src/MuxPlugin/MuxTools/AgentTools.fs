module VibeFs.MuxPlugin.MuxTools.AgentTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.AgentRole
open VibeFs.Kernel.HostKernel
open VibeFs.Mux.Contract
open VibeFs.MuxPlugin.Delegate
open VibeFs.MuxPlugin.PlanTools
open VibeFs.MuxPlugin.MuxPrompts
open VibeFs.MuxPlugin.MuxTools.Shared
open VibeFs.Opencode.ToolCopy

let private experimentsFor (role: AgentRole) : obj =
    let disabledTools = (subagentToolPolicy role).disabledTools @ planToolNames
    createObj [
        "subagentRole", box (AgentRole.toString role)
        "toolPolicy", box (createObj [ "disabledTools", box (disabledTools |> Array.ofList) ])
    ]

let private optionsFor (aiSettingsAgentId: string) (role: AgentRole) : obj =
    createObj [ "experiments", experimentsFor role; "aiSettingsAgentId", box aiSettingsAgentId ]

let private requireWorkspace (config: obj) (toolName: string) : string option =
    match strField config "workspaceId" with
    | None -> Some $"{toolName} requires workspaceId"
    | Some _ -> None

let private joinReports (reports: string array) : string =
    reports |> Array.map (fun r -> r.Trim()) |> String.concat "\n\n"

/// Wrap prompt-building + `runMuxSubagent` for single-intent tools.
module Tool =
    let bind
        (deps: obj)
        (agentId: string)
        (title: string)
        (aiSettingsAgentId: string)
        (role: AgentRole)
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
        (role: AgentRole)
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
                        let opts = Some (optionsFor aiSettingsAgentId role)
                        let! reports =
                            prompts
                            |> Array.map (fun prompt ->
                                async {
                                    return!
                                        runMuxSubagent deps config agentId prompt title opts
                                        |> Async.AwaitPromise
                                })
                            |> Async.Parallel
                        return joinReports reports
            }
            |> Async.StartAsPromise

let private buildEditorPrompts (_config: obj) (args: obj) : Async<string array> =
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
                        let files = pair.[1] :?> obj array |> Array.map string |> List.ofArray
                        formatMuxEditorUserPrompt intentText files)
    }

let editorTool (deps: obj) : ToolDefinition =
    let prefixItems = [| strProp "The code-change intent."; strArrayProp "The list of affected files." |]
    let itemSchema =
        createObj [ "type", box "array"; "minItems", box 2; "maxItems", box 2; "prefixItems", box prefixItems ]
    let intentsSchema =
        createObj [ "type", box "array"; "items", box itemSchema; "description", box Params.editorIntents ]
    { name = "editor"
      description = editor
      parameters = mkSchema (createObj [ "intents", box intentsSchema ]) [| "intents" |]
      execute = Tool.bindParallel deps "exec" "Editor" "exec" Editor buildEditorPrompts
      condition = None }

let private buildGreperPrompts (_config: obj) (args: obj) : Async<string array> =
    async {
        let intents = requireStrArray args "intents"
        if Array.isEmpty intents then return [||]
        else return intents |> Array.map formatMuxGreperUserPrompt
    }

let greperTool (deps: obj) : ToolDefinition =
    { name = "greper"
      description = greper
      parameters = mkSchema (createObj [ "intents", box (strArrayProp Params.greperIntents) ]) [| "intents" |]
      execute = Tool.bindParallel deps "explore" "Greper" "explore" Greper buildGreperPrompts
      condition = None }

let private reveriePromptFromArgs (config: obj) (args: obj) : Async<string> =
    async {
        let intent = defaultArg (strField args "intent") ""
        let files = requireStrArray args "files" |> List.ofArray
        let cwd = defaultArg (strField config "directory") ""
        let! results = VibeFs.Shell.ReverieFiles.readReverieFiles cwd files |> Async.AwaitPromise
        let sections =
            results
            |> List.map (fun r -> { file = r.filePath; content = r.content })
        let skipped = "(skipped)"
        let rendered =
            sections
            |> List.map (fun s -> $"=== {s.file} ===\n\n{Option.defaultValue skipped s.content}")
        let body = rendered |> String.concat "\n\n"
        let basePrompt = formatMuxReverieUserPrompt intent (sections |> List.map (fun s -> s.file))
        return if body = "" then basePrompt else $"{body}\n\n{basePrompt}"
    }

let reverieTool (deps: obj) : ToolDefinition =
    { name = "reverie"
      description = reverie
      parameters =
        mkSchema
            (createObj [ "intent", box (strProp Params.reverieIntent); "files", box (strArrayProp Params.reverieFiles) ])
            [| "intent"; "files" |]
      execute = Tool.bind deps "explore" "Reverie" "exec" Reverie reveriePromptFromArgs
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
      execute = Tool.bind deps "explore" "Browser" "explore" Browser buildBrowserPrompt
      condition = None }
