module Wanxiangshu.Shell.SubagentToolExecute

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Subagent
open Wanxiangshu.Kernel.SubagentPrompts
open Wanxiangshu.Kernel.ToolArgs
open Wanxiangshu.Kernel.ToolCopy
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.SubagentPromptBuild
open Wanxiangshu.Shell.SubagentSpawn
open Wanxiangshu.Shell.ToolArgsDecode
open Wanxiangshu.Shell.ToolExecute
open Wanxiangshu.Shell.ToolRuntimeContext

type OpencodeSubagentSpawn =
    { Host: Host
      Registry: ChildAgentRegistry
      Client: obj
      PluginCtx: obj
      ToolContext: obj }

type private RunSubagentCoreResult =
    ChildAgentRegistry -> obj -> string -> string -> string -> string -> string -> obj -> obj -> bool -> JS.Promise<Result<string, DomainError>>

let resolveSubagentPromise (context: string) (p: JS.Promise<Result<string, DomainError>>) : JS.Promise<string> =
    promise {
        let! result = p
        match result with
        | Ok text -> return text
        | Error err -> return! Promise.reject (System.Exception (wireEncodeToolError context err))
    }

let executeOpencodeSubagentTool
    (runCore: RunSubagentCoreResult)
    (spawn: OpencodeSubagentSpawn)
    (toolName: string)
    (args: obj)
    : JS.Promise<string> =
    promise {
        match decodeToolInvocation toolName args with
        | Error err -> return wireDecodeFailure toolName err
        | Ok decoded ->
            let runtime = fromOpencode spawn.ToolContext (pluginDirectoryFromCtx spawn.PluginCtx)
            let dir = runtime.Execution.Directory
            let sessionID = runtime.Execution.SessionId
            let registry = spawn.Registry
            let client = spawn.Client
            let ctx = spawn.ToolContext
            let tools = box null
            let spawnOne agent title prompt =
                resolveSubagentPromise toolName (runCore registry client agent title prompt dir (Id.sessionIdValue sessionID) ctx tools false)
            match decoded with
            | CoderBatch intents ->
                let prompts = promptsFromCoderIntents spawn.Host intents
                if prompts.IsEmpty then return subagentIntentsMustBeNonEmpty
                else return! runParallelSpawns prompts (spawnOne "coder" "Coder")
            | InvestigatorBatch intents ->
                let prompts = promptsFromInvestigatorIntents spawn.Host intents
                if prompts.IsEmpty then return subagentIntentsMustBeNonEmpty
                else return! runParallelSpawns prompts (spawnOne "investigator" "Investigator")
            | Typed (Meditator m) ->
                let! promptResult = meditatorPromptFromFiles spawn.Host dir m.Intent m.Files
                match promptResult with
                | Error e -> return subagentToolFailed "meditator" e
                | Ok prompt -> return! spawnOne "meditator" "Meditator" prompt
            | Typed (Browser b) ->
                return! spawnOne "browser" "Browser" (browserPromptText spawn.Host b.Intent)
            | Typed _ ->
                return subagentToolFailed toolName (InvalidIntent (toolName, "tool", "not a subagent tool"))
    }