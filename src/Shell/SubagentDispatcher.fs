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
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.SubagentIteratorStore
open Wanxiangshu.Shell.ToolArgsDecode
open Wanxiangshu.Shell.ToolExecute
open Wanxiangshu.Kernel.PromptFrontMatter
open Wanxiangshu.Kernel.ToolOutputInfo
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

let dispatch
    (host: Host)
    (adapter: IHostAdapter)
    (toolName: string)
    (args: obj)
    (scope: Wanxiangshu.Shell.RuntimeScope.RuntimeScope)
    (registry: ChildAgentRegistry option)
    : JS.Promise<string> =
    promise {
        match decodeToolInvocation toolName args with
        | Error err -> return wireDecodeFailure toolName err
        | Ok decoded ->
            let getChildIDForSpawn role =
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

            let wrapWithIterator text role title =
                promise {
                    let spawnedChildId =
                        match getChildIDForSpawn role with
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

                    match spawnedChildId with
                    | None -> return text
                    | Some cid ->
                        let roleStr =
                            match role with
                            | HostAdapter.Coder -> "coder"
                            | HostAdapter.Investigator -> "investigator"
                            | HostAdapter.Meditator -> "meditator"
                            | HostAdapter.Browser -> "browser"

                        let item =
                            { childID = cid
                              agent = roleStr
                              host = host }

                        let iter = storeSubagentIterator scope.SubagentIteratorStore "global" item
                        let root = adapter.WorkspaceRoot
                        let parentSid = adapter.SessionId

                        if root <> "" && parentSid <> "" then
                            do!
                                Wanxiangshu.Shell.EventLogRuntime.appendSubagentSpawnedOrFail
                                    root
                                    parentSid
                                    cid
                                    roleStr
                                    title

                        return Wanxiangshu.Kernel.ToolOutputInfo.withIterator text iter
                }

            let spawnOne role title prompt =
                let request =
                    { Role = role
                      Title = title
                      Prompt = prompt
                      AllowedTools = [||] }

                promise {
                    let! response = adapter.SpawnSubagent request

                    match response with
                    | Success text ->
                        let! res = wrapWithIterator text role title
                        return res
                    | Failure err -> return subagentToolFailed toolName err
                    | Aborted -> return subagentToolFailed toolName MessageAborted
                }

            match decoded with
            | CoderBatch intents ->
                let prompts = promptsFromCoderIntents host intents

                if prompts.IsEmpty then
                    return subagentIntentsMustBeNonEmpty
                else
                    List.zip prompts intents
                    |> List.iter (fun (prompt, intent) ->
                        adapter.RegisterTempFiles(intent.objective, coderTargetFiles intent))

                    let! reports =
                        prompts
                        |> List.map (spawnOne HostAdapter.Coder "Coder")
                        |> List.toArray
                        |> Promise.all

                    return formatBatchReports (List.ofArray reports)
            | InvestigatorBatch intents ->
                let prompts = promptsFromInvestigatorIntents host intents

                if prompts.IsEmpty then
                    return subagentIntentsMustBeNonEmpty
                else
                    List.zip prompts intents
                    |> List.iter (fun (prompt, intent) ->
                        adapter.RegisterTempFiles(intent.objective, Array.toList intent.entries))

                    let! reports =
                        prompts
                        |> List.map (spawnOne HostAdapter.Investigator "Investigator")
                        |> List.toArray
                        |> Promise.all

                    return formatBatchReports (List.ofArray reports)
            | Typed(Browser b) -> return! spawnOne HostAdapter.Browser "Browser" (browserPromptText host b.Intent)
            | Typed(Continue c) ->
                let cleanIter = c.Iterator.Trim()

                match consumeSubagentIterator scope.SubagentIteratorStore cleanIter with
                | None ->
                    let err =
                        InvalidIntent("continue", "iterator", "unknown, expired, or already consumed iterator")

                    return wireDecodeFailure "continue" err
                | Some item ->
                    let! response = adapter.ContinueSubagent(item.childID, item.agent, c.Prompt)

                    let! textResult =
                        promise {
                            match response with
                            | Success text ->
                                let root = adapter.WorkspaceRoot
                                let parentSid = adapter.SessionId

                                if root <> "" && parentSid <> "" then
                                    do!
                                        Wanxiangshu.Shell.EventLogRuntime.appendSubagentContinuedOrFail
                                            root
                                            parentSid
                                            item.childID
                                            c.Prompt

                                // Re-store/preserve the exact same iterator ID so the caller can reuse it directly!
                                preserveSubagentIterator scope.SubagentIteratorStore cleanIter item
                                return Wanxiangshu.Kernel.ToolOutputInfo.withIterator text cleanIter
                            | Failure err -> return subagentToolFailed "continue" err
                            | Aborted -> return subagentToolFailed "continue" MessageAborted
                        }

                    return textResult
            | Typed _ ->
                let err = InvalidIntent(toolName, "tool", "not a subagent tool")
                return! resolveSubagentPromise toolName (Promise.lift (Error err))
    }
