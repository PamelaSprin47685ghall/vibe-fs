module Wanxiangshu.Opencode.SubagentIo

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Shell.ErrorClassify
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.FallbackRecoveryWait
open Wanxiangshu.Shell.DelegatedAiSettings
open Wanxiangshu.Shell.OpencodeClientCodec
open Wanxiangshu.Shell.SessionIoSpawn
open Wanxiangshu.Opencode.SubagentSpawn
open Wanxiangshu.Shell.ChildSessionMailbox

type OpencodeSessionTurnHost(client: obj, agent: string, runtime: FallbackRuntimeState) =
    interface ISessionTurnHost with
        member _.RunOneTurn(sessionId, model, prompt) =
            let deferred = createDeferred<TurnOutcome> ()

            let mailbox: ChildSessionMailbox =
                ChildSessionMailboxRegistry.GetOrCreate(
                    sessionId,
                    (fun () ->
                        match getSessionApiFromClient client with
                        | Ok session ->
                            let abortPromise: JS.Promise<obj> =
                                invoke1 (box {| path = box {| id = sessionId |} |}) "abort" session

                            abortPromise |> ignore
                        | Error _ -> ())
                )

            let sendFn turnId =
                promise {
                    let modelStr =
                        match model.Variant with
                        | Some v -> sprintf "%s/%s:%s" model.ProviderID model.ModelID v
                        | None -> sprintf "%s/%s" model.ProviderID model.ModelID

                    let body =
                        Wanxiangshu.Shell.OpencodeSessionEventCodec.createPromptBodyWithModelAndNonce
                            (Some agent)
                            (Some modelStr)
                            prompt
                            (Some turnId)

                    let arg =
                        box
                            {| path = box {| id = sessionId |}
                               body = body |}

                    try
                        match getSessionApiFromClient client with
                        | Ok session ->
                            let! _ = invoke1 arg "prompt" session
                            ()
                        | Error _ -> ()
                    with _ex ->
                        () // Prompt rejection handled via event bridge posting TurnError

                    do! mailbox.Post(TurnStarted(turnId))
                }

            let turnId =
                "wanxiangshu-turn-" + System.Guid.NewGuid().ToString("N").Substring(0, 8)

            mailbox.Post(RunTurn(model, prompt, turnId, sendFn, deferred)) |> ignore
            deferred.Promise

module Dyn = Wanxiangshu.Shell.Dyn

open Wanxiangshu.Opencode.SubagentTypes

let runSubagentCoreResult
    (runtime: FallbackRuntimeState)
    (registry: ChildAgentRegistry)
    (client: obj)
    (agent: string)
    (title: string)
    (prompt: string)
    (directory: string)
    (sessionID: string)
    (context: obj)
    (tools: obj)
    (cleanup: bool)
    (existingChildID: string option)
    : JS.Promise<Result<string, DomainError>> =
    promise {
        let signal = getAbortSignal context

        let options =
            { agent = agent
              title = title
              prompt = prompt
              directory = directory
              sessionID = sessionID
              tools = tools
              aiSettings =
                { modelString = None
                  thinkingLevel = None } }

        try
            let! childResult =
                match existingChildID with
                | Some cid ->
                    let parentID =
                        registry.ResolveSubsessionParentID(if sessionID = "" then None else Some sessionID)

                    registry.RegisterChildAgent(cid, agent, parentID)
                    Promise.lift (Ok cid)
                | None -> startSubagentSession registry client options

            match childResult with
            | Error err -> return Error err
            | Ok childID ->
                let abortAndUnregister () =
                    match getSessionApiFromClient client with
                    | Ok session ->
                        let abortPromise: JS.Promise<obj> =
                            invoke1 (box {| path = box {| id = childID |} |}) "abort" session

                        abortPromise |> ignore
                    | Error _ -> ()

                    registry.UnregisterChildAgent(childID)

                let cleanupChildIfRequested () =
                    if cleanup then
                        abortAndUnregister ()

                let! abortedResult =
                    promise {
                        if not (isNull signal) && Dyn.truthy (Dyn.get signal "aborted") then
                            abortAndUnregister ()
                            return Some(Ok abortedPrefix)
                        else
                            return None
                    }

                match abortedResult with
                | Some res -> return res
                | None ->
                    let! currentMessages =
                        match getSessionApiFromClient client with
                        | Ok session ->
                            let msgPromise: JS.Promise<obj> =
                                invoke1 (box {| path = box {| id = childID |} |}) "messages" session

                            msgPromise |> Promise.map (fun r -> unbox<obj[]> (Dyn.get r "data"))
                        | Error _ -> Promise.lift [||]

                    let startCount = currentMessages.Length
                    let runId = "run-" + System.Guid.NewGuid().ToString("N").Substring(0, 8)
                    let started = runtime.StartSubsessionRun(childID, sessionID, runId)

                    if not started then
                        return Error(InvalidIntent("subagent", "run", "Subagent session already running"))
                    else
                        runtime.SetSubsessionPending childID true

                        if directory <> "" then
                            do!
                                Wanxiangshu.Shell.EventLogRuntimeAppend.appendSubsessionRunStartedOrFail
                                    directory
                                    childID
                                    childID
                                    sessionID
                                    runId

                        let host = OpencodeSessionTurnHost(client, agent, runtime)

                        let cfg =
                            let dir = if directory = "" then "." else directory
                            let fallbackConfigOpt = Wanxiangshu.Shell.FallbackConfigCodec.loadFallbackConfig dir

                            match fallbackConfigOpt with
                            | Some cfg -> cfg
                            | None -> Wanxiangshu.Shell.FallbackConfigCodec.emptyConfig

                        let chain =
                            let configChain =
                                match Map.tryFind agent cfg.AgentChains with
                                | Some c -> c
                                | None -> cfg.DefaultChain

                            if configChain.IsEmpty then
                                let runtimeChain = runtime.GetChain childID

                                if runtimeChain.IsEmpty then
                                    [ { ProviderID = "default"
                                        ModelID = "default"
                                        Variant = None
                                        Temperature = None
                                        TopP = None
                                        MaxTokens = None
                                        ReasoningEffort = None
                                        Thinking = false } ]
                                else
                                    runtimeChain
                            else
                                configChain

                        let fetchMessages (sid: string) =
                            promise {
                                match getSessionApiFromClient client with
                                | Ok session ->
                                    let! resp = invoke1 (box {| path = box {| id = sid |} |}) "messages" session
                                    let data = Dyn.get resp "data"

                                    if Dyn.isArray data then
                                        return (unbox<obj[]> data)
                                    else
                                        return [||]
                                | Error _ -> return [||]
                            }

                        try
                            try
                                try
                                    let! loopResult =
                                        runSubsessionLoop host childID prompt cfg chain runtime fetchMessages

                                    let st = runtime.GetOrCreateState childID

                                    let isSuccess =
                                        match loopResult with
                                        | Ok() -> true
                                        | Error _ -> false

                                    let status =
                                        if isSuccess then
                                            SubsessionRunStatus.Settled
                                        elif st.Lifecycle = FallbackLifecycle.Cancelled then
                                            SubsessionRunStatus.Cancelled
                                        else
                                            SubsessionRunStatus.Failed

                                    runtime.UpdateSubsessionRunStatus(childID, runId, status)

                                    if st.Lifecycle = FallbackLifecycle.Cancelled then
                                        return Ok abortedPrefix
                                    elif isSuccess then
                                        let! text = extractSessionText client childID directory startCount
                                        return Ok(formatSubagentReport noOutputText abortedPrefix text false)
                                    else
                                        match loopResult with
                                        | Error msg -> return Error(DomainError.InvalidIntent("subagent", "run", msg))
                                        | Ok() -> return Ok ""
                                with err ->
                                    let st = runtime.GetOrCreateState childID
                                    let isSuccess = st.Lifecycle = FallbackLifecycle.TaskComplete

                                    let status =
                                        if isSuccess then
                                            SubsessionRunStatus.Settled
                                        elif st.Lifecycle = FallbackLifecycle.Cancelled then
                                            SubsessionRunStatus.Cancelled
                                        else
                                            SubsessionRunStatus.Failed

                                    runtime.UpdateSubsessionRunStatus(childID, runId, status)

                                    if st.Lifecycle = FallbackLifecycle.Cancelled then
                                        return Ok abortedPrefix
                                    elif isSuccess then
                                        let! text = extractSessionText client childID directory startCount
                                        return Ok(formatSubagentReport noOutputText abortedPrefix text false)
                                    else
                                        return Error(translateJsError err)
                            finally
                                cleanupChildIfRequested ()
                        finally
                            ChildSessionMailboxRegistry.Remove childID

                            if childID <> "" then
                                runtime.ClearSubsessionPending childID
                                runtime.ClearSubsessionRun(childID, runId)
        with err ->
            return Error(translateJsError err)
    }

let runSubagentWithCleanup
    (runtime: FallbackRuntimeState)
    (registry: ChildAgentRegistry)
    (client: obj)
    (agent: string)
    (title: string)
    (prompt: string)
    (directory: string)
    (sessionID: string)
    (context: obj)
    : JS.Promise<Result<string, DomainError>> =
    runSubagentCoreResult runtime registry client agent title prompt directory sessionID context (box null) true None

let runSubagent
    (runtime: FallbackRuntimeState)
    (registry: ChildAgentRegistry)
    (client: obj)
    (agent: string)
    (title: string)
    (prompt: string)
    (directory: string)
    (sessionID: string)
    (context: obj)
    (tools: obj)
    : JS.Promise<Result<string, DomainError>> =
    runSubagentCoreResult runtime registry client agent title prompt directory sessionID context tools false None
