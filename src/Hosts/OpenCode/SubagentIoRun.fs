module Wanxiangshu.Hosts.Opencode.SubagentIoRun

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Runtime.ErrorClassify
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionPropertyTransitions
open Wanxiangshu.Runtime.Fallback.GateFlagTransitions
open Wanxiangshu.Runtime.DelegatedAiSettings
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.SessionIoSpawn
open Wanxiangshu.Runtime.SubsessionService
open Wanxiangshu.Runtime.SubsessionEventStore
open Wanxiangshu.Hosts.Opencode.SubagentSpawn
open Wanxiangshu.Hosts.Opencode.SubsessionHostAdapter
open Wanxiangshu.Hosts.Opencode.SubagentIoCleanup
open Wanxiangshu.Hosts.Opencode.SubagentIoArgs

module Dyn = Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Hosts.Opencode.SubagentTypes

let private formatRunFailure (f: RunFailure) : string =
    match f with
    | NoModelConfigured -> "No model available in fallback chain"
    | FallbackExhausted err -> err.Message
    | RecoveryExhausted reason -> reason
    | ProtocolViolation reason -> reason
    | InfrastructureFailure reason -> reason

let private resolveParentLiveModel
    (runtime: FallbackRuntimeStore)
    (client: obj)
    (parentSessionID: string)
    : JS.Promise<FallbackModel option> =
    promise {
        if parentSessionID = "" then
            return None
        else
            match runtime.GetModel parentSessionID with
            | Some m -> return Some m
            | None ->
                match getSessionApiFromClient client with
                | Error _ -> return None
                | Ok session ->
                    let! msgsResp = invoke1 (box {| path = box {| id = parentSessionID |} |}) "messages" session
                    let data = Dyn.get msgsResp "data"
                    let msgs = if Dyn.isArray data then unbox<obj[]> data else [||]

                    match Wanxiangshu.Runtime.Fallback.FallbackMessageCodec.tryGetLatestUserModel msgs with
                    | Some m -> return Some m
                    | None ->
                        return!
                            Wanxiangshu.Hosts.Opencode.Fallback.HostEventInspection.tryReadCurrentModel
                                client
                                parentSessionID
    }

let private applyDirective (runtime: FallbackRuntimeStore) (childID: string) (directive: ModelDirective) : unit =
    match directive with
    | RetryChain chain ->
        runtime.SetChain childID chain
        runtime.SetModel childID (List.head chain)
    | DelegateToHost -> runtime.SetChain childID []

let private mapRunResult (runRes: RunResult) : Result<string, DomainError> =
    match runRes with
    | Succeeded output -> Ok(formatSubagentReport noOutputText abortedPrefix output false)
    | Cancelled -> Ok abortedPrefix
    | Failed reason -> Error(DomainError.InvalidIntent("subagent", "run", formatRunFailure reason))

let private extractRunDirective
    (registry: ChildAgentRegistry)
    (runtime: FallbackRuntimeStore)
    (client: obj)
    (agent: string)
    (directory: string)
    (sessionID: string)
    (childID: string)
    : JS.Promise<FallbackConfig * ModelDirective> =
    promise {
        let dir = if directory = "" then "." else directory

        let cfg =
            match Wanxiangshu.Runtime.Fallback.FallbackConfigCodec.loadFallbackConfig dir with
            | Some c -> c
            | None -> Wanxiangshu.Runtime.Fallback.FallbackConfigCodec.emptyConfig

        let parentSessionID =
            if sessionID = "" then
                ""
            else
                registry.ResolveSubsessionParentID(Some sessionID)
                |> Option.defaultValue sessionID

        let! parentLiveModel = resolveParentLiveModel runtime client parentSessionID

        let! hostExplicitModelOpt =
            Wanxiangshu.Hosts.Opencode.Fallback.HostEventInspection.tryGetAgentExplicitModel client agent

        let hostConfigured = Option.isSome hostExplicitModelOpt

        return
            cfg,
            Wanxiangshu.Runtime.Fallback.FallbackConfigCodec.resolveModelDirective
                cfg
                agent
                hostConfigured
                (runtime.GetChain childID)
                (runtime.GetChain parentSessionID)
                parentLiveModel
    }

let private runSubagentInternal
    (registry: ChildAgentRegistry)
    (runtime: FallbackRuntimeStore)
    (client: obj)
    (agent: string)
    (directory: string)
    (sessionID: string)
    (childID: string)
    (prompt: string)
    (signal: obj)
    (cleanup: bool)
    (cfg: FallbackConfig)
    (directive: ModelDirective)
    : JS.Promise<Result<string, DomainError>> =
    promise {
        let svc =
            SubsessionService(
                directory,
                (fun _ -> createHost client agent directory),
                fun _ -> create (if directory = "" then "" else directory)
            )

        try
            let! runRes = svc.StartRun(childID, sessionID, prompt, cfg, directive, abortSignal = signal)
            do! cleanupChildIfRequested registry cleanup client directory childID
            return mapRunResult runRes
        with err ->
            do! cleanupChildIfRequested registry cleanup client directory childID
            return Error(translateJsError err)
    }

let executeSubagentRun
    (runtime: FallbackRuntimeStore)
    (registry: ChildAgentRegistry)
    (client: obj)
    (agent: string)
    (directory: string)
    (sessionID: string)
    (childID: string)
    (prompt: string)
    (signal: obj)
    (cleanup: bool)
    : JS.Promise<Result<string, DomainError>> =
    promise {
        let! cfg, directive = extractRunDirective registry runtime client agent directory sessionID childID
        applyDirective runtime childID directive

        return!
            runSubagentInternal
                registry
                runtime
                client
                agent
                directory
                sessionID
                childID
                prompt
                signal
                cleanup
                cfg
                directive
    }

let private resolveOrCreateChild
    (registry: ChildAgentRegistry)
    (client: obj)
    (sessionID: string)
    (agent: string)
    (existingChildID: string option)
    (options: SubagentLaunchOptions)
    : JS.Promise<Result<string, DomainError>> =
    promise {
        match existingChildID with
        | Some cid ->
            let parentID =
                registry.ResolveSubsessionParentID(if sessionID = "" then None else Some sessionID)

            registry.RegisterChildAgent(cid, agent, parentID)
            return Ok cid
        | None -> return! startSubagentSession registry client options
    }

let private handleAborted
    (registry: ChildAgentRegistry)
    (client: obj)
    (directory: string)
    (childID: string)
    : JS.Promise<Result<string, DomainError>> =
    promise {
        do! abortAndUnregister registry client directory childID
        return Ok abortedPrefix
    }

let private dispatchSubagentRun
    (runtime: FallbackRuntimeStore)
    (registry: ChildAgentRegistry)
    (client: obj)
    (agent: string)
    (directory: string)
    (sessionID: string)
    (childID: string)
    (prompt: string)
    (signal: obj)
    (cleanup: bool)
    : JS.Promise<Result<string, DomainError>> =
    promise {
        if not (isNull signal) && Dyn.truthy (Dyn.get signal "aborted") then
            return! handleAborted registry client directory childID
        else
            let! result =
                executeSubagentRun runtime registry client agent directory sessionID childID prompt signal cleanup

            return result
    }

let runSubagentCoreResult
    (runtime: FallbackRuntimeStore)
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
        let options = buildSubagentOptions agent title prompt directory sessionID tools

        try
            let! childResult = resolveOrCreateChild registry client sessionID agent existingChildID options

            match childResult with
            | Error err -> return Error err
            | Ok childID ->
                try
                    return!
                        dispatchSubagentRun
                            runtime
                            registry
                            client
                            agent
                            directory
                            sessionID
                            childID
                            prompt
                            signal
                            cleanup
                with err ->
                    return Error(translateJsError err)
        with err ->
            return Error(translateJsError err)
    }

let runSubagentWithCleanup
    (runtime: FallbackRuntimeStore)
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
    (runtime: FallbackRuntimeStore)
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
