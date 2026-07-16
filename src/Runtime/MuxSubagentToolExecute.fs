module Wanxiangshu.Runtime.MuxSubagentToolExecute

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.GateTransitions
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.HostAdapter
open Wanxiangshu.Runtime.HostAdapter
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.Subagent
open Wanxiangshu.Runtime.SubagentPrompts
open Wanxiangshu.Kernel.ToolArgs
open Wanxiangshu.Kernel.ToolCopy
open Wanxiangshu.Runtime.ErrorClassify
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.SubagentDispatcher
open Wanxiangshu.Runtime.SubagentPromptBuild
open Wanxiangshu.Runtime.SubagentSpawn
open Wanxiangshu.Runtime.ToolArgsDecode
open Wanxiangshu.Runtime.ToolExecute
open Wanxiangshu.Runtime.ToolRuntimeContext

open Wanxiangshu.Runtime.RuntimeScope

type MuxSubagentSpawn =
    { ToolNames: string array
      AgentId: string
      Title: string
      AiSettingsAgentId: string
      Role: string
      ToolOptions: obj option }

type RunMuxSubagent = obj -> obj -> string -> string -> string -> obj option -> JS.Promise<string>

type RunMuxSubagentWithTaskId =
    obj -> obj -> string -> string -> string -> obj option -> JS.Promise<Result<string * string, DomainError>>

type ContinueMuxSubagent = obj -> obj -> string -> string -> string -> obj option -> JS.Promise<string>

/// Mux already has a Promise-terminal host path. We do not invent mailbox/lease
/// state here — the Promise completion is the run terminal.
type MuxHostAdapter
    (
        runMuxWithTaskId: RunMuxSubagentWithTaskId,
        continueMux: ContinueMuxSubagent,
        deps: obj,
        config: obj,
        spawn: MuxSubagentSpawn,
        directory: string,
        sessionId: string,
        sessionScope: RuntimeScope
    ) =
    interface IHostAdapter with
        member _.WorkspaceRoot = directory
        member _.SessionId = sessionId

        member _.SpawnSubagent(request: SubagentRequest) : JS.Promise<SubagentResponse> =
            promise {
                try
                    let! res = runMuxWithTaskId deps config spawn.AgentId request.Prompt spawn.Title spawn.ToolOptions

                    match res with
                    | Ok(taskId, report) when taskId <> "" -> return Spawned(taskId, report)
                    | Ok _ -> return Failure(InvalidIntent("mux", "taskId", "missing from created subagent task"))
                    | Error err -> return Failure err
                with ex ->
                    return Failure(translateJsError ex)
            }

        member _.ContinueSubagent(childID: string, agent: string, prompt: string) : JS.Promise<SubagentResponse> =
            promise {
                try
                    let! text = continueMux deps config childID prompt spawn.Title spawn.ToolOptions
                    return Success text
                with ex ->
                    return Failure(translateJsError ex)
            }

        member _.RegisterTempFiles(prompt, files) =
            let key = sessionId + "\u0000" + prompt
            sessionScope.RegisterTempFiles(key, files)

        member _.TryGetTempFiles(prompt) =
            let key = sessionId + "\u0000" + prompt
            sessionScope.TryGetTempFiles(key)

let private muxConfigMessage (title: string) (error: DomainError) : string =
    match error with
    | InvalidIntent("mux", "workspaceId", _) -> muxToolRequiresWorkspaceId title
    | _ -> subagentToolFailed title error

let executeMuxSubagentTool
    (runMuxWithTaskId: RunMuxSubagentWithTaskId)
    (runMux: RunMuxSubagent)
    (continueMux: ContinueMuxSubagent)
    (deps: obj)
    (spawn: MuxSubagentSpawn)
    (args: obj)
    (config: obj)
    (sessionScope: RuntimeScope)
    : JS.Promise<string> =
    promise {
        let toolName = spawn.Role

        match fromMuxConfig config with
        | Error e -> return muxConfigMessage spawn.Title e
        | Ok runtime ->
            let adapter =
                MuxHostAdapter(
                    runMuxWithTaskId,
                    continueMux,
                    deps,
                    config,
                    spawn,
                    runtime.Execution.Directory,
                    Id.sessionIdValue runtime.Execution.SessionId,
                    sessionScope
                )

            let registry = unbox<ChildAgentRegistry> runtime.Execution.ChildRegistry
            return! dispatch mux adapter toolName args sessionScope (Some registry)
    }
