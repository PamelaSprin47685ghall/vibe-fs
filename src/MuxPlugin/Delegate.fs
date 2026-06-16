module VibeFs.MuxPlugin.Delegate

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.DomainError
open VibeFs.Kernel.JsBoundary
open VibeFs.Kernel.Boundary
open VibeFs.MuxPlugin.ResolveAiSettings

[<Global>]
type AbortController() =
    member _.signal: obj = jsNative
    member _.abort(): unit = jsNative

[<Global("Promise")>]
let private PromiseCtor : obj = jsNative

let private promiseRace<'T> (promises: JS.Promise<'T> array) : JS.Promise<'T> =
    unbox<JS.Promise<'T>> (PromiseCtor?race(promises))

let private resolveStr (s: string) : JS.Promise<string> =
    async { return s } |> Async.StartAsPromise

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

let internal coerceThinkingLevel (value: string) : string option =
    let trimmed = value.Trim()
    if trimmed = "" then None
    elif trimmed = "med" then Some "medium"
    elif
        trimmed = "off"
        || trimmed = "low"
        || trimmed = "medium"
        || trimmed = "high"
        || trimmed = "xhigh"
        || trimmed = "max"
    then
        Some trimmed
    else
        None

type internal ParentRuntimeAiSettings =
    { modelString: string option
      thinkingLevel: string option }

let private trimToOption (value: string) =
    let trimmed = value.Trim()
    if trimmed = "" then None else Some trimmed

let private readMuxEnvSettings (muxEnv: obj) : ParentRuntimeAiSettings =
    { modelString = Dyn.str muxEnv "MUX_MODEL_STRING" |> trimToOption
      thinkingLevel = Dyn.str muxEnv "MUX_THINKING_LEVEL" |> coerceThinkingLevel }

let private toRuntimeAiSettingsObj (settings: ParentRuntimeAiSettings) : obj =
    match settings.modelString, settings.thinkingLevel with
    | None, None -> null
    | _ ->
        let o = createObj []
        match settings.modelString with
        | Some modelString -> o?modelString <- modelString
        | None -> ()
        match settings.thinkingLevel with
        | Some thinkingLevel -> o?thinkingLevel <- thinkingLevel
        | None -> ()
        o

let internal buildParentRuntimeAiSettings (config: obj) : obj =
    let muxEnv = Dyn.get config "muxEnv"
    if Dyn.isNullish muxEnv then
        null
    else
        muxEnv |> readMuxEnvSettings |> toRuntimeAiSettingsObj

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
    o?parentWorkspaceId <- Id.workspaceIdValue workspaceId
    o?kind <- "agent"
    o?agentId <- agentId
    o?prompt <- prompt
    o?title <- title
    o?experiments <- experiments

    match modelString with
    | Some m when m.Trim() <> "" -> o?modelString <- m
    | _ -> ()

    match thinkingLevel with
    | Some t when t.Trim() <> "" -> o?thinkingLevel <- t
    | _ -> ()

    if not (Dyn.isNullish parentRuntimeAiSettings) then
        o?parentRuntimeAiSettings <- parentRuntimeAiSettings

    o

let delegateToSubAgent
    (deps: obj)
    (config: obj)
    (agentId: string)
    (prompt: string)
    (title: string)
    (options: obj option)
    : JS.Promise<string> =
    let workspaceId = requireWorkspaceId config title
    match workspaceId with
    | None -> resolveStr $"{title.ToLower()} requires workspaceId"
    | Some wid ->
        let taskService = Dyn.get config "taskService"
        if isNull taskService then
            resolveStr $"No task service for {title.ToLower()}"
        else
            async {
                let opts = defaultArg options (box null)
                let experiments = Dyn.get opts "experiments"
                let aiSettingsAgentId = Dyn.str opts "aiSettingsAgentId"
                let! aiSettings =
                    if aiSettingsAgentId = "" then
                        async { return emptySettings }
                    else
                        resolveDelegatedAgentAiSettings deps config aiSettingsAgentId |> Async.AwaitPromise

                let input =
                    createInput
                        wid
                        agentId
                        prompt
                        title
                        aiSettings.modelString
                        aiSettings.thinkingLevel
                        (buildParentRuntimeAiSettings config)
                        experiments

                let! createResult = taskCreate taskService input |> Async.AwaitPromise
                let success = Dyn.truthy (Dyn.get createResult "success")

                if not success then
                    let err = Dyn.str createResult "error"
                    return $"Failed to create {title.ToLower()} task: {err}"
                else
                    let taskId = Dyn.str (Dyn.get createResult "data") "taskId"
                    let abortSignal = Dyn.get config "abortSignal"

                    let waitOpts =
                        box
                            {| requestingWorkspaceId = Id.workspaceIdValue wid
                               abortSignal = abortSignal
                               backgroundOnMessageQueued = false |}

                    try
                        let! report = taskWait taskService taskId waitOpts |> Async.AwaitPromise
                        return Dyn.str report "reportMarkdown"
                    with err ->
                        match translateJsError err with
                        | TaskWaitBackgrounded ->
                            return
                                $"{title} task ({taskId}) moved to background. Use task tools to monitor it."
                        | _ -> return raise err
            }
            |> Async.StartAsPromise

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
    : Async<DelegateOutcome> =
    async {
        let controller = new AbortController()
        let signal = controller.signal
        let configWithSignal = Dyn.withKey config "abortSignal" signal

        let workPromise =
            async {
                let! report = delegateToSubAgent deps configWithSignal agentId prompt title options |> Async.AwaitPromise
                return box (Report report)
            }
            |> Async.StartAsPromise

        let timeoutPromise =
            async {
                do! Async.Sleep timeoutMs
                controller.abort()
                return box TimedOut
            }
            |> Async.StartAsPromise

        try
            let! winner = promiseRace [| workPromise; timeoutPromise |] |> Async.AwaitPromise
            return unbox<DelegateOutcome> winner
        with err ->
            match translateJsError err with
            | MessageAborted -> return TimedOut
            | _ -> return raise err
    }
