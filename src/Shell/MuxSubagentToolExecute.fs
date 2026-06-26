module Wanxiangshu.Shell.MuxSubagentToolExecute

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Subagent
open Wanxiangshu.Kernel.SubagentPrompts
open Wanxiangshu.Kernel.ToolArgs
open Wanxiangshu.Kernel.ToolCopy
open Wanxiangshu.Shell.SubagentPromptBuild
open Wanxiangshu.Shell.SubagentSpawn
open Wanxiangshu.Shell.ToolArgsDecode
open Wanxiangshu.Shell.ToolExecute
open Wanxiangshu.Shell.ToolRuntimeContext

type MuxSubagentSpawn =
    { ToolNames: string array
      AgentId: string
      Title: string
      AiSettingsAgentId: string
      Role: string
      ToolOptions: obj option }

type RunMuxSubagent =
    obj -> obj -> string -> string -> string -> obj option -> JS.Promise<string>

let private muxConfigMessage (title: string) (error: DomainError) : string =
    match error with
    | InvalidIntent ("mux", "workspaceId", _) -> muxToolRequiresWorkspaceId title
    | _ -> subagentToolFailed title error

let executeMuxSubagentTool
    (runMux: RunMuxSubagent)
    (deps: obj)
    (spawn: MuxSubagentSpawn)
    (args: obj)
    (config: obj)
    : JS.Promise<string> =
    promise {
        let toolName = spawn.Role
        match fromMuxConfig config with
        | Error e -> return muxConfigMessage spawn.Title e
        | Ok runtime ->
            match decodeToolInvocation toolName args with
            | Error err -> return wireDecodeFailure toolName err
            | Ok decoded ->
                let dir = runtime.Execution.Directory
                let opts = spawn.ToolOptions
                let runOne prompt cfg = runMux deps cfg spawn.AgentId prompt spawn.Title opts
                match decoded with
                | CoderBatch intents ->
                    let prompts = promptsFromCoderIntents mimocode intents
                    if prompts.IsEmpty then return subagentIntentsMustBeNonEmpty
                    else
                        return!
                            runParallelSpawnsWithAbort (List.toArray prompts) (fun prompt cfg -> runOne prompt cfg) config
                | InvestigatorBatch intents ->
                    let prompts = promptsFromInvestigatorIntents mimocode intents
                    if prompts.IsEmpty then return subagentIntentsMustBeNonEmpty
                    else
                        return!
                            runParallelSpawnsWithAbort (List.toArray prompts) (fun prompt cfg -> runOne prompt cfg) config
                | Typed (Meditator m) ->
                    let! promptResult = meditatorPromptFromFiles mimocode dir m.Intent m.Files
                    match promptResult with
                    | Error e -> return subagentToolFailed "meditator" e
                    | Ok prompt -> return! runOne prompt config
                | Typed (Browser b) ->
                    return! runOne (browserPromptText mimocode b.Intent) config
                | Typed _ ->
                    return subagentToolFailed toolName (InvalidIntent (toolName, "tool", "not a subagent tool"))
    }