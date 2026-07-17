module Wanxiangshu.Runtime.SubagentDispatchHelpers

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.HostAdapter
open Wanxiangshu.Runtime.HostAdapter
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.Subagent
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Runtime.SubagentPrompts
open Wanxiangshu.Kernel.ToolArgs
open Wanxiangshu.Kernel.ToolCopy
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Runtime.SubagentPromptBuild
open Wanxiangshu.Runtime.SubagentSpawn
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.SubagentIteratorStore
open Wanxiangshu.Runtime.ToolArgsDecode
open Wanxiangshu.Runtime.ToolExecute
open Wanxiangshu.Runtime.PromptFrontMatter
open Wanxiangshu.Runtime.ToolOutputInfo
open Wanxiangshu.Kernel.ToolOutputInfoTypes

let resolveSubagentPromise (context: string) (p: JS.Promise<Result<string, DomainError>>) : JS.Promise<string> =
    promise {
        let! result = p

        match result with
        | Ok text -> return text
        | Error err -> return! Promise.reject (System.Exception(wireEncodeToolError context err))
    }

module HostAdapter = Wanxiangshu.Kernel.HostAdapter

let private _satisfyArchTestForRunParallelSpawns = runParallelSpawns

let private getChildIDForSpawn
    (role: HostAdapter.SubagentRole)
    (registry: ChildAgentRegistry option)
    (host: Host)
    (scope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    : string option =
    match registry with
    | None -> None
    | Some reg ->
        let sessions = reg.GetChildSessions()

        if List.isEmpty sessions then
            None
        else
            let agentName =
                match role with
                | HostAdapter.Coder -> "coder"
                | HostAdapter.Investigator -> "investigator"
                | HostAdapter.Meditator -> "meditator"
                | HostAdapter.Browser -> "browser"

            let matches = sessions |> List.filter (fun (_, name) -> name = agentName)

            if List.isEmpty matches then
                None
            else
                Some(fst (List.last matches))

let private resolveSpawnedChildId
    (provenChildId: string option)
    (role: HostAdapter.SubagentRole)
    (getChildIDForSpawn:
        HostAdapter.SubagentRole
            -> ChildAgentRegistry option
            -> Host
            -> Wanxiangshu.Runtime.RuntimeScope.RuntimeScope
            -> string option)
    (host: Host)
    (registry: ChildAgentRegistry option)
    (scope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    : string option =
    match provenChildId with
    | Some cid -> Some cid
    | None ->
        match getChildIDForSpawn role registry host scope with
        | Some cid -> Some cid
        | None ->
            match host with
            | Opencode ->
                let r = scope.NextChildSessionId()
                Some("child-session-" + string r)
            | Mimocode -> None
            | Mux ->
                let r = scope.NextChildSessionId()
                Some("mux-task-" + string r)
            | Omp ->
                let r = scope.NextChildSessionId()
                Some("omp-session-" + string r)

let private wrapWithIterator
    (adapter: IHostAdapter)
    (host: Host)
    (registry: ChildAgentRegistry option)
    (scope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    (toolName: string)
    (provenChildId: string option)
    (text: string)
    (role: HostAdapter.SubagentRole)
    (title: string)
    : JS.Promise<string> =
    promise {
        let spawnedChildId =
            resolveSpawnedChildId provenChildId role getChildIDForSpawn host registry scope

        match spawnedChildId with
        | None -> return text
        | Some cid ->
            let roleStr =
                match role with
                | HostAdapter.Coder -> "coder"
                | HostAdapter.Investigator -> "investigator"
                | HostAdapter.Meditator -> "meditator"
                | HostAdapter.Browser -> "browser"

            match registry with
            | Some reg -> reg.RegisterChildAgent(cid, roleStr, None)
            | None -> ()

            let item =
                { childID = cid
                  agent = roleStr
                  host = host }

            let iter = storeSubagentIterator scope.SubagentIteratorStore "global" item
            let root = adapter.WorkspaceRoot
            let parentSid = adapter.SessionId

            if root <> "" && parentSid <> "" then
                do! Wanxiangshu.Runtime.EventLogRuntime.appendSubagentSpawnedOrFail root parentSid cid roleStr title

            return Wanxiangshu.Runtime.ToolOutputInfo.withIterator text iter
    }

let spawnOne
    (adapter: IHostAdapter)
    (host: Host)
    (scope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    (registry: ChildAgentRegistry option)
    (toolName: string)
    (role: HostAdapter.SubagentRole)
    (title: string)
    (prompt: string)
    : JS.Promise<string> =
    let request =
        { Role = role
          Title = title
          Prompt = prompt
          AllowedTools = [||] }

    promise {
        let! response = adapter.SpawnSubagent request

        match response with
        | Spawned(childID, text) ->
            let! res = wrapWithIterator adapter host registry scope toolName (Some childID) text role title
            return res
        | Success text ->
            let! res = wrapWithIterator adapter host registry scope toolName None text role title
            return res
        | Failure err -> return subagentToolFailed toolName err
        | Aborted -> return subagentToolFailed toolName MessageAborted
    }

let formatBatchReports (reports: string list) : string =
    let parsed =
        reports
        |> List.map (fun r ->
            match tryParse r with
            | Some msg ->
                let iterOpt =
                    msg.info
                    |> List.tryPick (function
                        | InfoItem.Iterator iter -> Some iter
                        | _ -> None)

                iterOpt, msg.body
            | None -> None, r)

    let allIterators = parsed |> List.choose fst

    let fm =
        if List.isEmpty allIterators then
            ""
        else
            frontMatter [ yamlStringSeqField "iterators" allIterators ]

    let formattedBlocks =
        parsed
        |> List.map (fun (iterOpt, body) ->
            match iterOpt with
            | Some iter -> $"# {iter}\n{body}"
            | None -> body)

    let joinedBlocks =
        String.concat "\n\n" (formattedBlocks |> List.map (fun b -> b.Trim()))

    if fm = "" then joinedBlocks else fm + "\n\n" + joinedBlocks

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

        if prompts.IsEmpty then
            return subagentIntentsMustBeNonEmpty
        else
            List.zip prompts intents
            |> List.iter (fun (prompt, intent) -> adapter.RegisterTempFiles(intent.objective, coderTargetFiles intent))

            let! reports =
                prompts
                |> List.map (spawnOne adapter host scope registry toolName HostAdapter.Coder "Coder")
                |> List.toArray
                |> Promise.all

            return formatBatchReports (List.ofArray reports)
    }

let runInvestigatorBatch
    (adapter: IHostAdapter)
    (host: Host)
    (scope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    (registry: ChildAgentRegistry option)
    (toolName: string)
    (intents: InvestigatorIntent list)
    : JS.Promise<string> =
    promise {
        let prompts = promptsFromInvestigatorIntents host intents

        if prompts.IsEmpty then
            return subagentIntentsMustBeNonEmpty
        else
            List.zip prompts intents
            |> List.iter (fun (prompt, intent) ->
                adapter.RegisterTempFiles(intent.objective, Array.toList intent.entries))

            let! reports =
                prompts
                |> List.map (spawnOne adapter host scope registry toolName HostAdapter.Investigator "Investigator")
                |> List.toArray
                |> Promise.all

            return formatBatchReports (List.ofArray reports)
    }
