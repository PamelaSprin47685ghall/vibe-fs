module VibeFs.MuxPlugin.MuxTools.AgentTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.AgentRole
open VibeFs.Kernel.HostKernel
open VibeFs.Mux.Contract
open VibeFs.MuxPlugin.Delegate
open VibeFs.MuxPlugin.MuxTools.Shared
open VibeFs.Opencode.ToolCopy

let private experimentsFor (role: AgentRole) : obj =
    createObj [ "subagentRole", box (AgentRole.toString role)
                "toolPolicy", box (createObj [ "disabledTools", box ((subagentToolPolicy role).disabledTools |> Array.ofList) ]) ]

let private optionsFor (aiSettingsAgentId: string) (role: AgentRole) : obj =
    createObj [ "aiSettingsAgentId", box aiSettingsAgentId
                "experiments", box (experimentsFor role) ]

let editorTool (deps: obj) : ToolDefinition =
    let prefixItems = [| strProp "The code-change intent."; strArrayProp "The list of affected files." |]
    let itemSchema = createObj [ "type", box "array"; "minItems", box 2; "maxItems", box 2; "prefixItems", box prefixItems ]
    let intentsSchema = createObj [ "type", box "array"; "items", box itemSchema; "description", box Params.editorIntents ]
    { name = "editor"
      description = editor
      parameters = mkSchema (createObj [ "intents", box intentsSchema ]) [| "intents" |]
      execute = fun config args ->
          if strField config "workspaceId" = None then resolveStr "editor requires workspaceId"
          else
              let intents = Dyn.get args "intents"
              if Dyn.isNullish intents || not (Dyn.isArray intents) then resolveStr "Error: `intents` must be a non-empty array."
              else
                  let intentsArr = intents :?> obj array
                  async {
                      let! reports =
                          intentsArr |> Array.map (fun intent ->
                              let pair = intent :?> obj array
                              let intentText = string pair.[0]
                              let files = pair.[1] :?> obj array |> Array.map string |> List.ofArray
                              let prompt = formatEditorIntent intentText files
                              delegateToSubAgent deps config "exec" prompt "Editor" (Some (optionsFor "exec" Editor))
                              |> Async.AwaitPromise) |> Async.Parallel
                      return String.concat "\n---\n" (List.ofArray reports)
                  } |> Async.StartAsPromise
      condition = None }

let greperTool (deps: obj) : ToolDefinition =
    { name = "greper"
      description = greper
      parameters = mkSchema (createObj [ "intents", box (strArrayProp Params.greperIntents) ]) [| "intents" |]
      execute = fun config args ->
          if strField config "workspaceId" = None then resolveStr "greper requires workspaceId"
          else
              let intents = requireStrArray args "intents"
              if Array.isEmpty intents then resolveStr "Error: `intents` must be a non-empty array."
              else
                  async {
                      let! reports =
                          intents |> Array.map (fun intent ->
                              delegateToSubAgent deps config "explore" intent "Greper" (Some (optionsFor "explore" Greper))
                              |> Async.AwaitPromise) |> Async.Parallel
                      return String.concat "\n---\n" (List.ofArray reports)
                  } |> Async.StartAsPromise
      condition = None }

let reverieTool (deps: obj) : ToolDefinition =
    { name = "reverie"
      description = reverie
      parameters = mkSchema (createObj [ "intent", box (strProp Params.reverieIntent); "files", box (strArrayProp Params.reverieFiles) ]) [| "intent"; "files" |]
      execute = fun config args ->
          if strField config "workspaceId" = None then resolveStr "reverie requires workspaceId"
          else
              let intent = defaultArg (strField args "intent") ""
              let files = requireStrArray args "files" |> List.ofArray
              let sections = files |> List.map (fun f -> { file = f; content = None })
              let prompt = buildReveriePrompt sections intent
              delegateToSubAgent deps config "explore" prompt "Reverie" (Some (optionsFor "exec" Reverie))
      condition = None }

let browserTool (deps: obj) : ToolDefinition =
    { name = "browser"
      description = browser
      parameters = mkSchema (createObj [ "intent", box (strProp Params.browserIntent) ]) [| "intent" |]
      execute = fun config args ->
          if strField config "workspaceId" = None then resolveStr "browser requires workspaceId"
          else
              let intent = defaultArg (strField args "intent") ""
              delegateToSubAgent deps config "explore" intent "Browser" (Some (optionsFor "explore" Browser))
      condition = None }
