module Wanxiangshu.Shell.SubagentDispatcher

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.HostAdapter
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Subagent
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Kernel.SubagentPrompts
open Wanxiangshu.Kernel.ToolArgs
open Wanxiangshu.Kernel.ToolCopy
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Shell.SubagentPromptBuild
open Wanxiangshu.Shell.SubagentSpawn
open Wanxiangshu.Shell.ToolArgsDecode
open Wanxiangshu.Shell.ToolExecute

let resolveSubagentPromise (context: string) (p: JS.Promise<Result<string, DomainError>>) : JS.Promise<string> =
    promise {
        let! result = p
        match result with
        | Ok text -> return text
        | Error err -> return! Promise.reject (System.Exception (wireEncodeToolError context err))
    }

module HostAdapter = Wanxiangshu.Kernel.HostAdapter

let dispatch (host: Host) (adapter: IHostAdapter) (toolName: string) (args: obj) : JS.Promise<string> =
    promise {
        match decodeToolInvocation toolName args with
        | Error err -> return wireDecodeFailure toolName err
        | Ok decoded ->
            let spawnOne role title prompt =
                let request = {
                    Role = role
                    Title = title
                    Prompt = prompt
                    AllowedTools = [||]
                }
                promise {
                    let! response = adapter.SpawnSubagent request
                    match response with
                    | Success text -> return text
                    | Failure err -> return subagentToolFailed toolName err
                    | Aborted -> return subagentToolFailed toolName MessageAborted
                }
            match decoded with
            | CoderBatch intents ->
                let prompts = promptsFromCoderIntents host intents
                if prompts.IsEmpty then return subagentIntentsMustBeNonEmpty
                else
                    List.zip prompts intents
                    |> List.iter (fun (prompt, intent) -> adapter.RegisterTempFiles(intent.objective, coderTargetFiles intent))
                    return! runParallelSpawns prompts (spawnOne HostAdapter.Coder "Coder")
            | InvestigatorBatch intents ->
                let prompts = promptsFromInvestigatorIntents host intents
                if prompts.IsEmpty then return subagentIntentsMustBeNonEmpty
                else
                    List.zip prompts intents
                    |> List.iter (fun (prompt, intent) -> adapter.RegisterTempFiles(intent.objective, Array.toList intent.entries))
                    return! runParallelSpawns prompts (spawnOne HostAdapter.Investigator "Investigator")
            | Typed (Meditator m) ->
                let! promptResult = meditatorPromptFromFiles host adapter.WorkspaceRoot m.Intent m.Files
                match promptResult with
                | Error e -> return subagentToolFailed "meditator" e
                | Ok prompt -> return! spawnOne HostAdapter.Meditator "Meditator" prompt
            | Typed (Browser b) ->
                return! spawnOne HostAdapter.Browser "Browser" (browserPromptText host b.Intent)
            | Typed _ ->
                return subagentToolFailed toolName (InvalidIntent (toolName, "tool", "not a subagent tool"))
    }
