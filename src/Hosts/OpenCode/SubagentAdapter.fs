module Wanxiangshu.Hosts.Opencode.SubagentAdapter

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime
open Wanxiangshu.Kernel.HostAdapter
open Wanxiangshu.Runtime.HostAdapter
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Hosts.Opencode.SessionIo
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.RuntimeScope

open Wanxiangshu.Runtime.ToolRuntimeContext

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
                | Inspector -> "inspector"
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
