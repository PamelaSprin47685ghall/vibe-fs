module Wanxiangshu.Mux.SubagentTools

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Subagent
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Kernel.Config
open Wanxiangshu.Kernel.SubagentToolPolicy
open Wanxiangshu.Kernel.HostAdapter
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Mux.Delegate
open Wanxiangshu.Mux.Wrappers
open Wanxiangshu.Shell.ErrorClassify
open Wanxiangshu.Shell.MuxJsonSchema
open Wanxiangshu.Shell.MuxSubagentToolExecute
open Wanxiangshu.Shell.SubagentDispatcher

open Wanxiangshu.Shell.RuntimeScope

let private description (name: string) : string =
    match Wanxiangshu.Kernel.ToolCatalog.description name with
    | Ok d -> d
    | Error e -> failwith e

let private subagentRequiredKeys (toolName: string) : string array =
    match Wanxiangshu.Kernel.ToolCatalog.subagentRequiredKeys toolName with
    | Ok keys -> keys
    | Error e -> failwith e

let private disabledToolsForRole (toolNames: string array) (role: string) : string array =
    SubagentToolPolicy.disabledToolNamesForRole mux toolNames role muxSpawnToolUniverse

let disabledToolsForReviewer (toolNames: string array) : string array =
    disabledToolsForRole toolNames "reviewer"

let toolOptions (toolNames: string array) (role: string) (aiSettingsAgentId: string) : obj option =
    Some(
        createObj
            [ "experiments",
              box (
                  createObj
                      [ "subagentRole", box role
                        "toolPolicy", box (createObj [ "disabledTools", box (disabledToolsForRole toolNames role) ]) ]
              )
              "aiSettingsAgentId", box aiSettingsAgentId ]
    )

let private spawnFor
    (deps: obj)
    (toolNames: string array)
    (agentId: string)
    (title: string)
    (aiSettingsAgentId: string)
    (role: string)
    : MuxSubagentSpawn =
    { ToolNames = toolNames
      AgentId = agentId
      Title = title
      AiSettingsAgentId = aiSettingsAgentId
      Role = role
      ToolOptions = toolOptions toolNames role aiSettingsAgentId }

let private execute
    (deps: obj)
    (toolNames: string array)
    (sessionScope: RuntimeScope)
    (agentId: string)
    (title: string)
    (aiSettingsAgentId: string)
    (role: string)
    (config: obj)
    (args: obj)
    : JS.Promise<string> =
    executeMuxSubagentTool
        runMuxSubagentWithTaskId
        runMuxSubagent
        continueMuxSubagent
        deps
        (spawnFor deps toolNames agentId title aiSettingsAgentId role)
        args
        config
        sessionScope

let coderTool (deps: obj) (toolNames: string array) (sessionScope: RuntimeScope) : ToolDefinition =
    { name = "coder"
      description = description "coder"
      parameters =
        mkSchema
            (createObj
                [ "intents", box (muxCoderIntentsSchema Params.coderIntents)
                  "tdd", box (strEnumProp Params.coderTdd [| "red"; "green" |]) ])
            (subagentRequiredKeys "coder")
      execute = fun config args -> execute deps toolNames sessionScope "exec" "Coder" "exec" "coder" config args
      condition = None }

let investigatorTool (deps: obj) (toolNames: string array) (sessionScope: RuntimeScope) : ToolDefinition =
    { name = "investigator"
      description = description "investigator"
      parameters =
        mkSchema
            (createObj [ "intents", box (muxInvestigatorIntentsSchema Params.investigatorIntents) ])
            (subagentRequiredKeys "investigator")
      execute =
        fun config args ->
            execute deps toolNames sessionScope "explore" "Investigator" "explore" "investigator" config args
      condition = None }

let browserTool (deps: obj) (toolNames: string array) (sessionScope: RuntimeScope) : ToolDefinition =
    { name = "browser"
      description = description "browser"
      parameters =
        mkSchema (createObj [ "intent", box (strProp Params.browserIntent) ]) (subagentRequiredKeys "browser")
      execute =
        fun config args -> execute deps toolNames sessionScope "explore" "Browser" "explore" "browser" config args
      condition = None }

let continueTool (deps: obj) (toolNames: string array) (sessionScope: RuntimeScope) : ToolDefinition =
    { name = "continue"
      description = description "continue"
      parameters =
        mkSchema
            (createObj
                [ "iterator", box (strProp "The subsession iterator ID")
                  "prompt", box (strProp "New instructions or question") ])
            (subagentRequiredKeys "continue")
      execute = fun config args -> execute deps toolNames sessionScope "exec" "Continue" "exec" "continue" config args
      condition = None }
