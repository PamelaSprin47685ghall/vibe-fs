module Wanxiangshu.Hosts.Mux.DelegateTasks

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Runtime.ErrorClassify
open Wanxiangshu.Runtime.SubagentSpawn
open Wanxiangshu.Runtime.DelegateToolsCodec
open Wanxiangshu.Runtime.ToolExecute
open Wanxiangshu.Hosts.Mux.DelegateCodec

let private taskCreate (taskService: obj) (input: obj) : JS.Promise<obj> =
    unbox<JS.Promise<obj>> (taskService?create (input))

let private taskWait (taskService: obj) (taskId: string) (opts: obj) : JS.Promise<obj> =
    unbox<JS.Promise<obj>> (taskService?waitForAgentReport (taskId, opts))

let private translateTaskWaitError
    (err: exn)
    (title: string)
    (taskId: string)
    : JS.Promise<Result<string, DomainError>> =
    match translateJsError err with
    | TaskWaitBackgrounded ->
        Promise.lift (Ok $"{title} task ({taskId}) moved to background. Use task tools to monitor it.")
    | other -> Promise.lift (Error other)

let createAndWaitTaskCore
    (ctx: DelegateCodec.DelegationContext)
    (agentId: string)
    (prompt: string)
    (title: string)
    : JS.Promise<Result<string * string, DomainError>> =
    promise {
        let input =
            DelegateCodec.buildCreateInput
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

let createAndWaitTask
    (ctx: DelegateCodec.DelegationContext)
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

let private taskContinue (taskService: obj) (taskId: string) (prompt: string) (opts: obj) : JS.Promise<obj> =
    unbox<JS.Promise<obj>> (taskService?continueAgentTask (taskId, prompt, opts))

let continueAndWaitTask
    (ctx: DelegateCodec.DelegationContext)
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
