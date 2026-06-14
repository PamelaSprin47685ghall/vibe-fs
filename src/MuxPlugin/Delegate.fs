module VibeFs.MuxPlugin.Delegate

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel

[<Emit("Promise.resolve($0)")>]
let private resolveStr (s: string) : JS.Promise<string> = jsNative

let private requireWorkspaceId (config: obj) (title: string) : string =
    let wid = Dyn.str config "workspaceId"
    if wid = "" then "" else wid

[<Emit("$0.create($1)")>]
let private taskCreate (taskService: obj) (input: obj) : JS.Promise<obj> = jsNative
[<Emit("$0.waitForAgentReport($1, $2)")>]
let private taskWait (taskService: obj) (taskId: string) (opts: obj) : JS.Promise<obj> = jsNative
[<Emit("($0 != null && $0 !== undefined)")>]
let private isNotNullish (o: obj) : bool = jsNative

let private createInput (workspaceId: string) (agentId: string) (prompt: string) (title: string)
                        (modelString: string) (thinkingLevel: string) (experiments: obj) : obj =
    box {| parentWorkspaceId = workspaceId
           kind = "agent"
           agentId = agentId
           prompt = prompt
           title = title
           modelString = (if modelString = "" then unbox null else box modelString)
           thinkingLevel = (if thinkingLevel = "" then unbox null else box thinkingLevel)
           experiments = experiments |}

/// Delegate a sub-agent task via the host's taskService.  Returns the report
/// markdown or an error string.  Mirrors vibe-me-mux delegateToSubAgent.
let delegateToSubAgent (config: obj) (agentId: string) (prompt: string) (title: string)
                       (options: obj option) : JS.Promise<string> =
    let workspaceId = requireWorkspaceId config title
    if workspaceId = "" then resolveStr $"{title.ToLower()} requires workspaceId"
    else
        let taskService = Dyn.get config "taskService"
        if isNull taskService then resolveStr $"No task service for {title.ToLower()}"
        else
            async {
                let opts = defaultArg options (box null)
                let modelStr = Dyn.str opts "modelString"
                let thinking = Dyn.str opts "thinkingLevel"
                let experiments = Dyn.get opts "experiments"
                let input = createInput workspaceId agentId prompt title modelStr thinking experiments
                let! createResult = taskCreate taskService input |> Async.AwaitPromise
                let success = Dyn.truthy (Dyn.get createResult "success")
                if not success then
                    let err = Dyn.str createResult "error"
                    return $"Failed to create {title.ToLower()} task: {err}"
                else
                    let taskId = Dyn.str (Dyn.get createResult "data") "taskId"
                    let abortSignal = Dyn.get config "abortSignal"
                    let waitOpts = box {| requestingWorkspaceId = workspaceId
                                          abortSignal = abortSignal
                                          backgroundOnMessageQueued = false |}
                    try
                        let! report = taskWait taskService taskId waitOpts |> Async.AwaitPromise
                        return Dyn.str report "reportMarkdown"
                    with err ->
                        let errName = if isNotNullish err then Dyn.str err "name" else ""
                        if errName = "ForegroundWaitBackgroundedError" then
                            return $"{title} task ({taskId}) moved to background. Use task tools to monitor it."
                        else return raise err
            } |> Async.StartAsPromise
