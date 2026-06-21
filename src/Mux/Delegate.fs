module VibeFs.Mux.Delegate

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Domain
open VibeFs.Mux.AiSettings
open VibeFs.Mux.Wrappers

[<Global>]
type AbortController() =
    member _.signal: obj = jsNative
    member _.abort(): unit = jsNative

let private taskCreate (taskService: obj) (input: obj) : JS.Promise<obj> =
    unbox<JS.Promise<obj>>(taskService?create(input))

let private taskWait (taskService: obj) (taskId: string) (opts: obj) : JS.Promise<obj> =
    unbox<JS.Promise<obj>>(taskService?waitForAgentReport(taskId, opts))

let private requireWorkspaceId (config: obj) (title: string) : WorkspaceId option =
    let wid = Dyn.str config "workspaceId"
    Id.tryWorkspaceId wid

type DelegateOutcome =
    | Report of string
    | TimedOut


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
        match requireWorkspaceId config title with
        | None -> return Error $"{title.ToLower()} requires workspaceId"
        | Some wid ->
            let taskService = Dyn.get config "taskService"
            if isNull taskService then
                return Error $"No task service for {title.ToLower()}"
            else
                let opts = defaultArg options (box null)
                let experiments = Dyn.get opts "experiments"
                let aiSettingsAgentId = Dyn.str opts "aiSettingsAgentId"
                let! aiSettings =
                    if aiSettingsAgentId = "" then
                        Promise.lift emptySettings
                    else
                        resolveDelegatedAgentAiSettings deps config aiSettingsAgentId

                return
                    Ok
                        { workspaceId = wid
                          taskService = taskService
                          aiSettings = aiSettings
                          experiments = experiments
                          parentRuntimeAiSettings = buildParentRuntimeAiSettings config
                          abortSignal = Dyn.get config "abortSignal" }
    }

let private translateTaskWaitError (err: exn) (title: string) (taskId: string) : JS.Promise<string> =
    match translateJsError err with
    | TaskWaitBackgrounded ->
        Promise.lift $"{title} task ({taskId}) moved to background. Use task tools to monitor it."
    | _ -> Promise.reject err

let private createAndWaitTask
    (ctx: DelegationContext)
    (agentId: string)
    (prompt: string)
    (title: string)
    : JS.Promise<string> =
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
        let success = Dyn.truthy (Dyn.get createResult "success")

        if not success then
            let err = Dyn.str createResult "error"
            return $"Failed to create {title.ToLower()} task: {err}"
        else
            let taskId = Dyn.str (Dyn.get createResult "data") "taskId"
            let waitOpts =
                box
                    {| requestingWorkspaceId = Id.workspaceIdValue ctx.workspaceId
                       abortSignal = ctx.abortSignal
                       backgroundOnMessageQueued = false |}

            try
                let! report = taskWait ctx.taskService taskId waitOpts
                return Dyn.str report "reportMarkdown"
            with err ->
                return! translateTaskWaitError err title taskId
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
        | Ok ctx -> return! createAndWaitTask ctx agentId prompt title
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

let delegateWithTimeout
    (deps: obj)
    (config: obj)
    (agentId: string)
    (prompt: string)
    (title: string)
    (options: obj option)
    (timeoutMs: int)
    : JS.Promise<DelegateOutcome> =
    promise {
        let controller = new AbortController()
        let signal = controller.signal
        let configWithSignal = Dyn.withKey config "abortSignal" signal

        let workPromise =
            promise {
                let! report = delegateToSubAgent deps configWithSignal agentId prompt title options
                return box (Report report)
            }

        let timeoutPromise =
            promise {
                do! Promise.sleep timeoutMs
                controller.abort()
                return box TimedOut
            }

        try
            let! winner = Promise.race [| workPromise; timeoutPromise |]
            return unbox<DelegateOutcome> winner
        with err ->
            match translateJsError err with
            | MessageAborted -> return TimedOut
            | _ -> return! Promise.reject err
    }
