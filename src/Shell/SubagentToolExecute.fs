module VibeFs.Shell.SubagentToolExecute

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.Domain
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Subagent
open VibeFs.Kernel.SubagentPrompts
open VibeFs.Kernel.ToolArgs
open VibeFs.Kernel.ToolCopy
open VibeFs.Kernel.ToolResult
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.SubagentPromptBuild
open VibeFs.Shell.SubagentSpawn
open VibeFs.Shell.ToolArgsDecode
open VibeFs.Shell.ToolExecute
open VibeFs.Shell.ToolRuntimeContext

type OpencodeSubagentSpawn =
    { Registry: ChildAgentRegistry
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
                resolveSubagentPromise toolName (runCore registry client agent title prompt dir sessionID ctx tools false)
            match decoded with
            | CoderBatch intents ->
                let prompts = promptsFromCoderIntents opencode intents
                if prompts.IsEmpty then return subagentIntentsMustBeNonEmpty
                else return! runParallelSpawns prompts (spawnOne "coder" "Coder")
            | InvestigatorBatch intents ->
                let prompts = promptsFromInvestigatorIntents opencode intents
                if prompts.IsEmpty then return subagentIntentsMustBeNonEmpty
                else return! runParallelSpawns prompts (spawnOne "investigator" "Investigator")
            | Typed (Meditator m) ->
                let! promptResult = meditatorPromptFromFiles opencode dir m.Intent m.Files
                match promptResult with
                | Error e -> return subagentToolFailed "meditator" e
                | Ok prompt -> return! spawnOne "meditator" "Meditator" prompt
            | Typed (Browser b) ->
                return! spawnOne "browser" "Browser" (browserPromptText opencode b.Intent)
            | Typed _ ->
                return subagentToolFailed toolName (InvalidIntent (toolName, "tool", "not a subagent tool"))
    }