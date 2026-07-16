module Wanxiangshu.Hosts.Opencode.SubagentTools

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.SubagentPrompts
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Kernel.HostAdapter
open Wanxiangshu.Runtime.HostAdapter
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Hosts.Opencode.ToolSchema
open Wanxiangshu.Hosts.Opencode.SessionIo
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.SubagentDispatcher
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.GateTransitions
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Runtime.RuntimeScope

let private subagentRequiredKeys (toolName: string) : string array =
    match Wanxiangshu.Kernel.ToolCatalog.subagentRequiredKeys toolName with
    | Ok keys -> keys
    | Error e -> failwith e

type RunSubagentCoreResult =
    FallbackRuntimeStore
        -> ChildAgentRegistry
        -> obj
        -> string
        -> string
        -> string
        -> string
        -> string
        -> obj
        -> obj
        -> bool
        -> string option
        -> JS.Promise<Result<string, DomainError>>

type OpencodeHostAdapter
    (
        runCore: RunSubagentCoreResult,
        registry: ChildAgentRegistry,
        client: obj,
        ctx: obj,
        toolContext: obj,
        fallbackRuntime: FallbackRuntimeStore,
        sessionScope: RuntimeScope
    ) =
    let workspaceRoot =
        (fromOpencode toolContext (pluginDirectoryFromCtx ctx)).Execution.Directory

    let sessionId =
        (fromOpencode toolContext (pluginDirectoryFromCtx ctx)).Execution.SessionId
        |> Id.sessionIdValue

    interface IHostAdapter with
        member _.WorkspaceRoot = workspaceRoot
        member _.SessionId = sessionId

        member _.SpawnSubagent(request: SubagentRequest) : JS.Promise<SubagentResponse> =
            let agent =
                match request.Role with
                | Coder -> "coder"
                | Investigator -> "investigator"
                | Meditator -> "meditator"
                | Browser -> "browser"

            promise {
                let! result =
                    runCore
                        fallbackRuntime
                        registry
                        client
                        agent
                        request.Title
                        request.Prompt
                        workspaceRoot
                        sessionId
                        toolContext
                        (box null)
                        false
                        None

                return
                    match result with
                    | Ok text -> Success text
                    | Error err -> Failure err
            }

        member _.ContinueSubagent(childID: string, agent: string, prompt: string) : JS.Promise<SubagentResponse> =
            promise {
                let! result =
                    runCore
                        fallbackRuntime
                        registry
                        client
                        agent
                        "Continue"
                        prompt
                        workspaceRoot
                        sessionId
                        toolContext
                        (box null)
                        false
                        (Some childID)

                return
                    match result with
                    | Ok text -> Success text
                    | Error err -> Failure err
            }

        member _.RegisterTempFiles(prompt, files) =
            let key = sessionId + "\u0000" + prompt
            sessionScope.RegisterTempFiles(key, files)

        member _.TryGetTempFiles(prompt) =
            let key = sessionId + "\u0000" + prompt
            sessionScope.TryGetTempFiles(key)

let private executeSubagent
    (host: Host)
    (registry: ChildAgentRegistry)
    (ctx: obj)
    (toolName: string)
    (args: obj)
    (context: obj)
    (runtime: FallbackRuntimeStore)
    (sessionScope: RuntimeScope)
    =
    match getClientFromPluginCtx ctx with
    | Error e -> Promise.lift (wireEncodeToolError "OpencodeClient" e)
    | Ok client ->
        let adapter =
            OpencodeHostAdapter(runSubagentCoreResult, registry, client, ctx, context, runtime, sessionScope)

        dispatch host adapter toolName args sessionScope (Some registry)

let coderTool
    (host: Host)
    (registry: ChildAgentRegistry)
    (ctx: obj)
    (runtime: FallbackRuntimeStore)
    (sessionScope: RuntimeScope)
    : obj =
    let coderRequiredKeys = subagentRequiredKeys "coder"

    define
        coder
        (subagentZodShape
            coderRequiredKeys
            (createObj
                [ "intents", coderIntentsSchema Params.coderIntents
                  "tdd", enumReq [| "red"; "green" |] Params.coderTdd
                  "ui_", uiParam ]))
        (fun args context -> executeSubagent host registry ctx "coder" args context runtime sessionScope)

let investigatorTool
    (host: Host)
    (registry: ChildAgentRegistry)
    (ctx: obj)
    (runtime: FallbackRuntimeStore)
    (sessionScope: RuntimeScope)
    : obj =
    let investigatorRequiredKeys = subagentRequiredKeys "investigator"

    define
        investigator
        (subagentZodShape
            investigatorRequiredKeys
            (createObj
                [ "intents", investigatorIntentsSchema Params.investigatorIntents
                  "ui_", uiParam ]))
        (fun args context -> executeSubagent host registry ctx "investigator" args context runtime sessionScope)

let browserTool
    (host: Host)
    (registry: ChildAgentRegistry)
    (ctx: obj)
    (runtime: FallbackRuntimeStore)
    (sessionScope: RuntimeScope)
    : obj =
    let browserRequiredKeys = subagentRequiredKeys "browser"

    define
        browser
        (subagentZodShape browserRequiredKeys (createObj [ "intent", strReq Params.browserIntent ]))
        (fun args context -> executeSubagent host registry ctx "browser" args context runtime sessionScope)

let continueTool
    (host: Host)
    (registry: ChildAgentRegistry)
    (ctx: obj)
    (runtime: FallbackRuntimeStore)
    (sessionScope: RuntimeScope)
    : obj =
    let continueRequiredKeys = subagentRequiredKeys "continue"

    define
        ToolSchema.continueSpec
        (subagentZodShape
            continueRequiredKeys
            (createObj
                [ "iterator", strReq "The subsession iterator ID"
                  "prompt", strReq "New instructions or question" ]))
        (fun args context -> executeSubagent host registry ctx "continue" args context runtime sessionScope)
