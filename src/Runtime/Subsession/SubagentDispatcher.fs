module Wanxiangshu.Runtime.SubagentDispatcher

open Wanxiangshu.Runtime.SubagentBatchSpawn
open Wanxiangshu.Runtime.SubagentBatchArgs
open Wanxiangshu.Runtime.SubagentBatchSpawnCore

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
open Wanxiangshu.Runtime.EventLogRuntime

module HostAdapter = Wanxiangshu.Kernel.HostAdapter

let private handleContinue
    (adapter: IHostAdapter)
    (host: Host)
    (scope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    (toolName: string)
    (c: ContinueArgs)
    : JS.Promise<string> =
    promise {
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
                    | Success text
                    | Spawned(_, text) ->
                        let root = adapter.WorkspaceRoot
                        let parentSid = adapter.SessionId

                        if root <> "" && parentSid <> "" then
                            do! appendSubagentContinuedOrFail root parentSid item.childID c.Prompt

                        preserveSubagentIterator scope.SubagentIteratorStore cleanIter item
                        return withIterator text cleanIter
                    | Failure err -> return subagentToolFailed "continue" err
                    | Aborted -> return subagentToolFailed "continue" MessageAborted
                }

            return textResult
    }

let dispatch
    (host: Host)
    (adapter: IHostAdapter)
    (toolName: string)
    (args: obj)
    (scope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    (registry: ChildAgentRegistry option)
    : JS.Promise<string> =
    promise {
        match decodeToolInvocation toolName args with
        | Error err -> return wireDecodeFailure toolName err
        | Ok(Typed(Browser b)) ->
            return!
                spawnOne
                    adapter
                    host
                    scope
                    registry
                    toolName
                    HostAdapter.Browser
                    "Browser"
                    (browserPromptText host b.Intent)
        | Ok(Typed(Continue c)) -> return! handleContinue adapter host scope toolName c
        | Ok(CoderBatch rawIntents) ->
            match validateCoderBatchArgs toolName args with
            | Error err -> return wireDecodeFailure toolName err
            | Ok intents -> return! runCoderBatch adapter host scope registry toolName intents
        | Ok(InvestigatorBatch rawIntents) ->
            match validateInvestigatorBatchArgs toolName args with
            | Error err -> return wireDecodeFailure toolName err
            | Ok intents -> return! runInvestigatorBatch adapter host scope registry toolName intents
        | Ok(Typed _) ->
            let err = InvalidIntent(toolName, "tool", "not a subagent tool")
            return! Promise.lift (wireDecodeFailure toolName err)
    }
