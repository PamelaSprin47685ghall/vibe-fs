module Wanxiangshu.Runtime.SubagentBatchSpawn

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.HostAdapter
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Runtime.HostAdapter
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.Subagent
open Wanxiangshu.Kernel.ToolArgs
open Wanxiangshu.Kernel.ToolCopy
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Runtime.SubagentPromptBuild
open Wanxiangshu.Runtime.SubagentBatchArgs
open Wanxiangshu.Runtime.SubagentBatchSpawnCore
open Wanxiangshu.Runtime.SubagentSpawn
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.SubagentIteratorStore
open Wanxiangshu.Runtime.ToolOutputInfo
open Wanxiangshu.Kernel.ToolOutputInfoTypes

module HostAdapter = Wanxiangshu.Kernel.HostAdapter

let runCoderBatch
    (adapter: IHostAdapter)
    (host: Host)
    (scope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    (registry: ChildAgentRegistry option)
    (toolName: string)
    (intents: CoderIntent list)
    : JS.Promise<string> =
    promise {
        let prompts = promptsFromCoderIntents host intents

        List.zip prompts intents
        |> List.iter (fun (prompt, intent) -> adapter.RegisterTempFiles(intent.objective, coderTargetFiles intent))

        let! reports =
            prompts
            |> List.map (spawnOne adapter host scope registry toolName HostAdapter.Coder "Coder")
            |> List.toArray
            |> Promise.all

        return formatBatchReports (List.ofArray reports)
    }

let runInspectorBatch
    (adapter: IHostAdapter)
    (host: Host)
    (scope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    (registry: ChildAgentRegistry option)
    (toolName: string)
    (intents: InspectorIntent list)
    : JS.Promise<string> =
    promise {
        let prompts = promptsFromInspectorIntents host intents

        List.zip prompts intents
        |> List.iter (fun (prompt, intent) -> adapter.RegisterTempFiles(intent.objective, Array.toList intent.entries))

        let! reports =
            prompts
            |> List.map (spawnOne adapter host scope registry toolName HostAdapter.Inspector "Inspector")
            |> List.toArray
            |> Promise.all

        return formatBatchReports (List.ofArray reports)
    }
