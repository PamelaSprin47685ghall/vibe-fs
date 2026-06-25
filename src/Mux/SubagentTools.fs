module VibeFs.Mux.SubagentTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Subagent
open VibeFs.Kernel.ToolCatalog
open VibeFs.Kernel.Config
open VibeFs.Kernel.SubagentToolPolicy
open VibeFs.Mux.Delegate
open VibeFs.Mux.Wrappers
open VibeFs.Kernel.HostTools
open VibeFs.Shell.MuxJsonSchema
open VibeFs.Shell.MuxSubagentToolExecute

let private disabledToolsForRole (toolNames: string array) (role: string) : string array =
    SubagentToolPolicy.disabledToolNamesForRole mux toolNames role muxSpawnToolUniverse

let disabledToolsForReviewer (toolNames: string array) : string array =
    disabledToolsForRole toolNames "reviewer"

let toolOptions (toolNames: string array) (role: string) (aiSettingsAgentId: string) : obj option =
    Some (createObj [ "experiments", box (createObj [ "subagentRole", box role; "toolPolicy", box (createObj [ "disabledTools", box (disabledToolsForRole toolNames role) ]) ]); "aiSettingsAgentId", box aiSettingsAgentId ])

let private spawnFor (deps: obj) (toolNames: string array) (agentId: string) (title: string) (aiSettingsAgentId: string) (role: string) : MuxSubagentSpawn =
    { ToolNames = toolNames
      AgentId = agentId
      Title = title
      AiSettingsAgentId = aiSettingsAgentId
      Role = role
      ToolOptions = toolOptions toolNames role aiSettingsAgentId }

let private execute (deps: obj) (toolNames: string array) (agentId: string) (title: string) (aiSettingsAgentId: string) (role: string) (config: obj) (args: obj) : JS.Promise<string> =
    executeMuxSubagentTool runMuxSubagent deps (spawnFor deps toolNames agentId title aiSettingsAgentId role) args config

let coderTool (deps: obj) (toolNames: string array) : ToolDefinition =
    { name = "coder"
      description = description "coder"
      parameters = mkSchema (createObj [ "intents", box (muxCoderIntentsSchema Params.coderIntents); "tdd", box (strEnumProp Params.coderTdd [| "red"; "green" |]) ]) (subagentRequiredKeys "coder")
      execute = fun config args -> execute deps toolNames "exec" "Coder" "exec" "coder" config args
      condition = None }

let investigatorTool (deps: obj) (toolNames: string array) : ToolDefinition =
    { name = "investigator"
      description = description "investigator"
      parameters = mkSchema (createObj [ "intents", box (muxInvestigatorIntentsSchema Params.investigatorIntents) ]) (subagentRequiredKeys "investigator")
      execute = fun config args -> execute deps toolNames "explore" "Investigator" "explore" "investigator" config args
      condition = None }

let meditatorTool (deps: obj) (toolNames: string array) : ToolDefinition =
    { name = "meditator"
      description = description "meditator"
      parameters =
        mkSchema
            (createObj [ "intent", box (strProp Params.meditatorIntent); "files", box (strArrayProp Params.meditatorFiles) ])
            (subagentRequiredKeys "meditator")
      execute = fun config args -> execute deps toolNames "explore" "Meditator" "exec" "meditator" config args
      condition = None }

let browserTool (deps: obj) (toolNames: string array) : ToolDefinition =
    { name = "browser"
      description = description "browser"
      parameters = mkSchema (createObj [ "intent", box (strProp Params.browserIntent) ]) (subagentRequiredKeys "browser")
      execute = fun config args -> execute deps toolNames "explore" "Browser" "explore" "browser" config args
      condition = None }