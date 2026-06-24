module VibeFs.Mux.SubagentTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Domain
open VibeFs.Kernel.SubagentPrompts
open VibeFs.Kernel.Subagent
open VibeFs.Kernel.SubagentIntents
open VibeFs.Shell.SubagentIntentsCodec
open VibeFs.Shell.SubagentSimpleArgsCodec
open VibeFs.Shell.SubagentPromptBuild
open VibeFs.Shell.SubagentSpawn
open VibeFs.Shell.MuxJsonSchema
open VibeFs.Kernel.ToolCatalog
open VibeFs.Kernel.ToolCopy
open VibeFs.Kernel.Config
open VibeFs.Kernel.SubagentToolPolicy
open VibeFs.Mux.Delegate
open VibeFs.Mux.Wrappers
open VibeFs.Kernel.HostTools
open VibeFs.Shell
open VibeFs.Shell.Dyn
open VibeFs.Shell.ToolRuntimeContext

let private disabledToolsForRole (toolNames: string array) (role: string) : string array =
    SubagentToolPolicy.disabledToolNamesForRole mux toolNames role muxSpawnToolUniverse

let disabledToolsForReviewer (toolNames: string array) : string array =
    disabledToolsForRole toolNames "reviewer"

/// Emit Mux `toolPolicy.disabledTools` for the spawned child: the denied set is computed by
/// `canUseForHost` over `HostTools.muxSpawnToolUniverse` (plus caller `toolNames`).
/// `subagentRole` is what the host binds to `roleScopedHostRemovals` and vibe-fs plugin policy.
let toolOptions (toolNames: string array) (role: string) (aiSettingsAgentId: string) : obj option =
    Some (createObj [ "experiments", box (createObj [ "subagentRole", box role; "toolPolicy", box (createObj [ "disabledTools", box (disabledToolsForRole toolNames role) ]) ]); "aiSettingsAgentId", box aiSettingsAgentId ])

let private muxConfigError (title: string) (error: DomainError) : string =
    match error with
    | InvalidIntent ("mux", "workspaceId", _) -> muxToolRequiresWorkspaceId title
    | _ -> formatDomainError error

module Tool =
    let bind (deps: obj) (toolNames: string array) (agentId: string) (title: string) (aiSettingsAgentId: string) (role: string) (buildPrompt: obj -> obj -> JS.Promise<string>) : obj -> obj -> JS.Promise<string> =
        fun config args ->
            promise {
                match fromMuxConfig config with
                | Error e -> return muxConfigError title e
                | Ok _ ->
                    let! prompt = buildPrompt config args
                    return! runMuxSubagent deps config agentId prompt title (toolOptions toolNames role aiSettingsAgentId)
            }

    let bindParallel (deps: obj) (toolNames: string array) (agentId: string) (title: string) (aiSettingsAgentId: string) (role: string) (buildPrompts: obj -> obj -> JS.Promise<Result<string array, string>>) : obj -> obj -> JS.Promise<string> =
        fun config args ->
            promise {
                match fromMuxConfig config with
                | Error e -> return muxConfigError title e
                | Ok _ ->
                    let! promptsResult = buildPrompts config args
                    match promptsResult with
                    | Error message -> return message
                    | Ok prompts when prompts.Length = 0 -> return "Error: `intents` must be a non-empty array."
                    | Ok prompts ->
                        let opts = toolOptions toolNames role aiSettingsAgentId
                        return!
                            runParallelSpawnsWithAbort
                                prompts
                                (fun prompt cfg -> runMuxSubagent deps cfg agentId prompt title opts)
                                config
            }

let private formatToolDomainError (context: string) (error: DomainError) : string =
    $"{context} failed: {formatDomainError error}"

let private buildPromptsFor (toolName: string) parser constructor (_config: obj) (args: obj) : JS.Promise<Result<string array, string>> =
    promise {
        match decodeIntentsField toolName args with
        | Error e -> return Error (formatToolDomainError toolName e)
        | Ok intents ->
            match parallelPromptsFromIntents mimocode toolName parser constructor intents with
            | Error e -> return Error (formatToolDomainError toolName e)
            | Ok prompts -> return Ok (List.toArray prompts)
    }

let private buildCoderPrompts (_config: obj) (args: obj) : JS.Promise<Result<string array, string>> =
    buildPromptsFor "coder" parseCoderIntents Coder _config args

let coderTool (deps: obj) (toolNames: string array) : ToolDefinition =
    { name = "coder"
      description = description "coder"
      parameters = mkSchema (createObj [ "intents", box (muxCoderIntentsSchema Params.coderIntents); "tdd", box (strEnumProp Params.coderTdd [| "red"; "green" |]) ]) [| "intents"; "tdd" |]
      execute = Tool.bindParallel deps toolNames "exec" "Coder" "exec" "coder" buildCoderPrompts
      condition = None }

let private buildInvestigatorPrompts (_config: obj) (args: obj) : JS.Promise<Result<string array, string>> =
    buildPromptsFor "investigator" parseInvestigatorIntents Investigator _config args

let investigatorTool (deps: obj) (toolNames: string array) : ToolDefinition =
    { name = "investigator"
      description = description "investigator"
      parameters = mkSchema (createObj [ "intents", box (muxInvestigatorIntentsSchema Params.investigatorIntents) ]) [| "intents" |]
      execute = Tool.bindParallel deps toolNames "explore" "Investigator" "explore" "investigator" buildInvestigatorPrompts
      condition = None }

let private meditatorPromptFromArgs (config: obj) (args: obj) : JS.Promise<string> =
    promise {
        match fromMuxConfig config with
        | Error e -> return muxConfigError "Meditator" e
        | Ok runtime ->
            match decodeMeditatorArgs args with
            | Error e -> return formatDomainError e
            | Ok decoded ->
                let! promptResult =
                    meditatorPromptFromFiles mimocode runtime.Execution.Directory decoded.Intent decoded.Files
                match promptResult with
                | Error e -> return formatDomainError e
                | Ok prompt -> return prompt
    }

let meditatorTool (deps: obj) (toolNames: string array) : ToolDefinition =
    { name = "meditator"
      description = description "meditator"
      parameters =
        mkSchema
            (createObj [ "intent", box (strProp Params.meditatorIntent); "files", box (strArrayProp Params.meditatorFiles) ])
            [| "intent"; "files" |]
      execute = Tool.bind deps toolNames "explore" "Meditator" "exec" "meditator" meditatorPromptFromArgs
      condition = None }

let private buildBrowserPrompt (_config: obj) (args: obj) : JS.Promise<string> =
    promise {
        match decodeBrowserArgs args with
        | Error e -> return formatDomainError e
        | Ok decoded -> return browserPromptText mimocode decoded.Intent
    }

let browserTool (deps: obj) (toolNames: string array) : ToolDefinition =
    { name = "browser"
      description = description "browser"
      parameters = mkSchema (createObj [ "intent", box (strProp Params.browserIntent) ]) [| "intent" |]
      execute = Tool.bind deps toolNames "explore" "Browser" "explore" "browser" buildBrowserPrompt
      condition = None }