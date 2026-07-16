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

let private taskCreate (taskService: obj) (input: obj) : JS.Promise<obj> =
    unbox<JS.Promise<obj>> (taskService?create (input))

let private taskWait (taskService: obj) (taskId: string) (opts: obj) : JS.Promise<obj> =
    unbox<JS.Promise<obj>> (taskService?waitForAgentReport (taskId, opts))

let private createInput
    (workspaceId: WorkspaceId)
    (agentId: string)
    (prompt: string)
    (title: string)
    (modelString: string option)
    (thinkingLevel: string option)
    (parentRuntimeAiSettings: obj)
    (experiments: obj)
    : obj =
    let o = createObj []
    o?("parentWorkspaceId") <- Id.workspaceIdValue workspaceId
    o?("kind") <- "agent"
    o?("agentId") <- agentId
    o?("prompt") <- prompt
    o?("title") <- title
    o?("experiments") <- experiments

    match modelString with
    | Some m when m.Trim() <> "" -> o?("modelString") <- m
    | _ -> ()

    match thinkingLevel with
    | Some t when t.Trim() <> "" -> o?("thinkingLevel") <- t
    | _ -> ()

    if not (Dyn.isNullish parentRuntimeAiSettings) then
        o?("parentRuntimeAiSettings") <- parentRuntimeAiSettings

    o

type private DelegationContext =
    { workspaceId: WorkspaceId
      taskService: obj
      aiSettings: DelegatedAiSettings
      experiments: obj
      parentRuntimeAiSettings: obj
      abortSignal: obj }

let private resolveDelegationContext
    (deps: obj)
    (config: obj)
    (title: string)
    (options: obj option)
    : JS.Promise<Result<DelegationContext, string>> =
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

/// Task-wait failures: backgrounding is a graceful outcome (returned as text on
/// the Ok channel, never thrown); every other exception becomes a typed
/// `Error` so callers stop using exceptions as control flow (TASK §5).
let private translateTaskWaitError
    (err: exn)
    (title: string)
    (taskId: string)
    : JS.Promise<Result<string, DomainError>> =
    match translateJsError err with
    | TaskWaitBackgrounded ->
        Promise.lift (Ok $"{title} task ({taskId}) moved to background. Use task tools to monitor it.")
    | other -> Promise.lift (Error other)

let private createAndWaitTaskCore
    (ctx: DelegationContext)
    (agentId: string)
    (prompt: string)
    (title: string)
    : JS.Promise<Result<string * string, DomainError>> =
    promise {
        let input =
            createInput
                ctx.workspaceId
                agentId
                prompt
                title
                ctx.aiSettings.modelString
                ctx.aiSettings.thinkingLevel
                ctx.parentRuntimeAiSettings
                ctx.experiments

        let! createResult = taskCreate ctx.taskService input

        match decodeTaskCreateResult createResult with
        | Error e -> return Ok("", wireDomainFailure "delegate.create" e)
        | Ok taskId ->
            let waitOpts =
                box
                    {| requestingWorkspaceId = Id.workspaceIdValue ctx.workspaceId
                       abortSignal = ctx.abortSignal
                       backgroundOnMessageQueued = false |}

            try
                let! report = taskWait ctx.taskService taskId waitOpts

                match decodeTaskReport report with
                | Ok markdown -> return Ok(taskId, markdown)
                | Error e -> return Ok(taskId, wireDomainFailure "delegate.report" e)
            with err ->
                let! waitErrResult = translateTaskWaitError err title taskId

                match waitErrResult with
                | Ok report -> return Ok(taskId, report)
                | Error e -> return Error e
    }

let private createAndWaitTask
    (ctx: DelegationContext)
    (agentId: string)
    (prompt: string)
    (title: string)
    : JS.Promise<Result<string, DomainError>> =
    promise {
        let! res = createAndWaitTaskCore ctx agentId prompt title

        match res with
        | Ok(_, report) -> return Ok report
        | Error e -> return Error e
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
            let! result = createAndWaitTask ctx agentId prompt title

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
        | Ok ctx -> return! createAndWaitTaskCore ctx agentId prompt title
    }

let private taskContinue (taskService: obj) (taskId: string) (prompt: string) (opts: obj) : JS.Promise<obj> =
    unbox<JS.Promise<obj>> (taskService?continueAgentTask (taskId, prompt, opts))

let private continueAndWaitTask
    (ctx: DelegationContext)
    (childTaskId: string)
    (prompt: string)
    (title: string)
    : JS.Promise<Result<string, DomainError>> =
    promise {
        let waitOpts =
            box
                {| requestingWorkspaceId = Id.workspaceIdValue ctx.workspaceId
                   abortSignal = ctx.abortSignal
                   backgroundOnMessageQueued = false |}

        let! continueResult = taskContinue ctx.taskService childTaskId prompt waitOpts

        match decodeTaskContinueResult continueResult with
        | Error e -> return Ok(wireDomainFailure "delegate.continue" e)
        | Ok() ->
            try
                let! report = taskWait ctx.taskService childTaskId waitOpts

                match decodeTaskReport report with
                | Ok markdown -> return Ok markdown
                | Error e -> return Ok(wireDomainFailure "delegate.report" e)
            with err ->
                return! translateTaskWaitError err title childTaskId
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
            let! result = continueAndWaitTask ctx childTaskId prompt title

            match result with
            | Ok report -> return report
            | Error e -> return wireDomainFailure "delegate" e
    }

type DelegateOutcome = DelegateTimeout.DelegateOutcome

let delegateWithTimeout deps config agentId prompt title options timeoutMs =
    DelegateTimeout.delegateWithTimeout delegateToSubAgent deps config agentId prompt title options timeoutMs
