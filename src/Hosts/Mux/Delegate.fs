module Wanxiangshu.Hosts.Mux.Delegate

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Runtime.ErrorClassify
open Wanxiangshu.Hosts.Mux.AiSettings
open Wanxiangshu.Hosts.Mux.Wrappers
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.SubagentSpawn
open Wanxiangshu.Runtime.DelegateToolsCodec
open Wanxiangshu.Runtime.ToolExecute
open Wanxiangshu.Runtime.ToolContextCodec
open Wanxiangshu.Hosts.Mux.DelegateTimeout
open Wanxiangshu.Hosts.Mux.DelegateCodec
open Wanxiangshu.Hosts.Mux.DelegateTasks

let private resolveDelegationContext
    (deps: obj)
    (config: obj)
    (title: string)
    (options: obj option)
    : JS.Promise<Result<DelegateCodec.DelegationContext, string>> =
    promise {
        match decodeDelegateConfig config with
        | Error e -> return Error(wireDomainFailure "delegate" e)
        | Ok host ->
            let opts = defaultArg options (box null)
            let optFields = decodeDelegateOptions opts

            let! aiSettings =
                if optFields.AiSettingsAgentId = "" then
                    Promise.lift emptySettings
                else
                    resolveDelegatedAgentAiSettings deps config optFields.AiSettingsAgentId

            return
                Ok
                    { workspaceId = host.WorkspaceId
                      taskService = host.TaskService
                      aiSettings = aiSettings
                      experiments = optFields.Experiments
                      parentRuntimeAiSettings = buildParentRuntimeAiSettings config
                      abortSignal = host.AbortSignal }
    }

let delegateToSubAgent
    (deps: obj)
    (config: obj)
    (agentId: string)
    (prompt: string)
    (title: string)
    (options: obj option)
    : JS.Promise<string> =
    promise {
        let! ctxResult = resolveDelegationContext deps config title options

        match ctxResult with
        | Error msg -> return msg
        | Ok ctx ->
            let! result = DelegateTasks.createAndWaitTask ctx agentId prompt title

            match result with
            | Ok report -> return report
            | Error e -> return wireDomainFailure "delegate" e
    }

let runMuxSubagent
    (deps: obj)
    (config: obj)
    (agentId: string)
    (prompt: string)
    (title: string)
    (options: obj option)
    : JS.Promise<string> =
    delegateToSubAgent deps config agentId prompt title options

let runMuxSubagentWithTaskId
    (deps: obj)
    (config: obj)
    (agentId: string)
    (prompt: string)
    (title: string)
    (options: obj option)
    : JS.Promise<Result<string * string, DomainError>> =
    promise {
        let! ctxResult = resolveDelegationContext deps config title options

        match ctxResult with
        | Error msg -> return Error(InvalidIntent("delegate", "context", msg))
        | Ok ctx -> return! DelegateTasks.createAndWaitTaskCore ctx agentId prompt title
    }

let continueMuxSubagent
    (deps: obj)
    (config: obj)
    (childTaskId: string)
    (prompt: string)
    (title: string)
    (options: obj option)
    : JS.Promise<string> =
    promise {
        let! ctxResult = resolveDelegationContext deps config title options

        match ctxResult with
        | Error msg -> return msg
        | Ok ctx ->
            let! result = DelegateTasks.continueAndWaitTask ctx childTaskId prompt title

            match result with
            | Ok report -> return report
            | Error e -> return wireDomainFailure "delegate" e
    }

type DelegateOutcome = DelegateTimeout.DelegateOutcome

let delegateWithTimeout deps config agentId prompt title options timeoutMs =
    DelegateTimeout.delegateWithTimeout delegateToSubAgent deps config agentId prompt title options timeoutMs
