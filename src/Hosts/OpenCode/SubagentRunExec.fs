module Wanxiangshu.Hosts.Opencode.SubagentRunExec

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Runtime.SubsessionService
open Wanxiangshu.Hosts.Opencode.SubagentIoCleanup
open Wanxiangshu.Hosts.Opencode.SubsessionHostAdapter
open Wanxiangshu.Runtime.ErrorClassify
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.SessionIoSpawn
open Wanxiangshu.Runtime.SubsessionEventStore

module Dyn = Wanxiangshu.Runtime.Dyn

open Wanxiangshu.Hosts.Opencode.SubagentSpawn
open Wanxiangshu.Hosts.Opencode.SubagentTypes
open Wanxiangshu.Hosts.Opencode.SubagentRunDirective

let formatRunFailure (f: RunFailure) : string =
    match f with
    | NoModelConfigured -> "No model available in fallback chain"
    | FallbackExhausted err -> err.Message
    | RecoveryExhausted reason -> reason
    | ProtocolViolation reason -> reason
    | InfrastructureFailure reason -> reason

let mapRunResult (runRes: RunResult) : Result<string, DomainError> =
    match runRes with
    | Succeeded output -> Ok(formatSubagentReport noOutputText abortedPrefix output false)
    | Cancelled -> Ok abortedPrefix
    | Failed reason -> Error(DomainError.InvalidIntent("subagent", "run", formatRunFailure reason))

let runSubagentInternal
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

let resolveOrCreateChild
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

let handleAborted
    (registry: ChildAgentRegistry)
    (client: obj)
    (directory: string)
    (childID: string)
    : JS.Promise<Result<string, DomainError>> =
    promise {
        do! abortAndUnregister registry client directory childID
        return Ok abortedPrefix
    }

let dispatchSubagentRun
    (executeSubagentRun:
        FallbackRuntimeStore
            -> ChildAgentRegistry
            -> obj
            -> string
            -> string
            -> string
            -> string
            -> string
            -> obj
            -> bool
            -> JS.Promise<Result<string, DomainError>>)
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
    : JS.Promise<Result<string, DomainError>> =
    promise {
        if not (isNull signal) && Dyn.truthy (Dyn.get signal "aborted") then
            return! handleAborted registry client directory childID
        else
            return! executeSubagentRun runtime registry client agent directory sessionID childID prompt signal cleanup
    }
