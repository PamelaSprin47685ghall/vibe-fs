module VibeFs.MuxPlugin.Delegate

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.DomainError
open VibeFs.Kernel.JsBoundary
open VibeFs.MuxPlugin.ResolveAiSettings

[<Emit("Promise.resolve($0)")>]
let private resolveStr (s: string) : JS.Promise<string> = jsNative

[<Emit("{}")>]
let private emptyObj () : obj = jsNative

[<Emit("$0[$1] = $2")>]
let private setKey (o: obj) (k: string) (v: obj) : unit = jsNative

[<Emit("new AbortController()")>]
let private abortControllerCreate () : obj = jsNative

[<Emit("$0.signal")>]
let private abortSignalFromController (controller: obj) : obj = jsNative

[<Emit("$0.abort()")>]
let private abortControllerAbort (controller: obj) : unit = jsNative

[<Emit("$0.then(v => ({ __vibeFsValue: v }))")>]
let private wrapWorkResult (promise: JS.Promise<'T>) : JS.Promise<obj> = jsNative

[<Emit("new Promise(resolve => setTimeout(() => resolve({ __vibeFsTimedOut: true }), $0))")>]
let private timeoutResultPromise (timeoutMs: int) : JS.Promise<obj> = jsNative

[<Emit("Promise.race([$0, $1])")>]
let private promiseRace (a: JS.Promise<obj>) (b: JS.Promise<obj>) : JS.Promise<obj> = jsNative

[<Emit("!!$0.__vibeFsTimedOut")>]
let private isTimedOutResult (value: obj) : bool = jsNative

[<Emit("$0.__vibeFsValue !== undefined ? $0.__vibeFsValue : $0")>]
let private unwrapResult (value: obj) : 'T = jsNative

let private requireWorkspaceId (config: obj) (title: string) : string =
    let wid = Dyn.str config "workspaceId"
    if wid = "" then "" else wid

[<Emit("$0.create($1)")>]
let private taskCreate (taskService: obj) (input: obj) : JS.Promise<obj> = jsNative
[<Emit("$0.waitForAgentReport($1, $2)")>]
let private taskWait (taskService: obj) (taskId: string) (opts: obj) : JS.Promise<obj> = jsNative

type DelegateOutcome =
    | Report of string
    | TimedOut

/// Match mux `coerceThinkingLevel` / task tool parent-runtime hint (med → medium).
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

/// Parent live model/thinking from `config.muxEnv`, forwarded as a low-priority
/// fallback in `taskService.create` (same as mux `buildParentRuntimeAiSettings`).
let internal buildParentRuntimeAiSettings (config: obj) : obj =
    let muxEnv = Dyn.get config "muxEnv"
    if Dyn.isNullish muxEnv then
        null
    else
        let modelRaw = Dyn.str muxEnv "MUX_MODEL_STRING"
        let modelOpt = if modelRaw.Trim() = "" then None else Some modelRaw
        let thinkingOpt = coerceThinkingLevel (Dyn.str muxEnv "MUX_THINKING_LEVEL")

        match modelOpt, thinkingOpt with
        | None, None -> null
        | _ ->
            let o = emptyObj ()
            match modelOpt with
            | Some m -> setKey o "modelString" (box m)
            | None -> ()
            match thinkingOpt with
            | Some t -> setKey o "thinkingLevel" (box t)
            | None -> ()
            o

let private createInput
    (workspaceId: string)
    (agentId: string)
    (prompt: string)
    (title: string)
    (modelString: string option)
    (thinkingLevel: string option)
    (parentRuntimeAiSettings: obj)
    (experiments: obj)
    : obj =
    let o = emptyObj ()
    setKey o "parentWorkspaceId" (box workspaceId)
    setKey o "kind" (box "agent")
    setKey o "agentId" (box agentId)
    setKey o "prompt" (box prompt)
    setKey o "title" (box title)
    setKey o "experiments" experiments

    match modelString with
    | Some m when m.Trim() <> "" -> setKey o "modelString" (box m)
    | _ -> ()

    match thinkingLevel with
    | Some t when t.Trim() <> "" -> setKey o "thinkingLevel" (box t)
    | _ -> ()

    if not (Dyn.isNullish parentRuntimeAiSettings) then
        setKey o "parentRuntimeAiSettings" parentRuntimeAiSettings

    o

/// Delegate a sub-agent task via the host's taskService.  Returns the report
/// markdown or an error string.  Mirrors vibe-me-mux delegateToSubAgent, with
/// parent `muxEnv` forwarded as `parentRuntimeAiSettings` like the native task tool.
let delegateToSubAgent
    (deps: obj)
    (config: obj)
    (agentId: string)
    (prompt: string)
    (title: string)
    (options: obj option)
    : JS.Promise<string> =
    let workspaceId = requireWorkspaceId config title
    if workspaceId = "" then
        resolveStr $"{title.ToLower()} requires workspaceId"
    else
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
                        workspaceId
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
                            {| requestingWorkspaceId = workspaceId
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

/// Host taskService delegation for mux plugin tools (not Opencode Session).
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
        let controller = abortControllerCreate ()
        let signal = abortSignalFromController controller
        let configWithSignal = Dyn.withKey config "abortSignal" signal

        let workPromise = delegateToSubAgent deps configWithSignal agentId prompt title options
        let wrappedWork = wrapWorkResult workPromise
        let timeoutPromise = timeoutResultPromise timeoutMs

        try
            let! result = promiseRace wrappedWork timeoutPromise |> Async.AwaitPromise
            if isTimedOutResult result then
                abortControllerAbort controller
                return TimedOut
            else
                return Report (unwrapResult result)
        with err ->
            match translateJsError err with
            | MessageAborted -> return TimedOut
            | _ -> return raise err
    }
