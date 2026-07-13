module Wanxiangshu.Shell.MuxSubagentToolExecute

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.HostAdapter
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Subagent
open Wanxiangshu.Kernel.SubagentPrompts
open Wanxiangshu.Kernel.ToolArgs
open Wanxiangshu.Kernel.ToolCopy
open Wanxiangshu.Shell.ErrorClassify
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.SubagentDispatcher
open Wanxiangshu.Shell.SubagentPromptBuild
open Wanxiangshu.Shell.SubagentSpawn
open Wanxiangshu.Shell.ToolArgsDecode
open Wanxiangshu.Shell.ToolExecute
open Wanxiangshu.Shell.ToolRuntimeContext

open Wanxiangshu.Shell.RuntimeScope

type MuxSubagentSpawn =
    { ToolNames: string array
      AgentId: string
      Title: string
      AiSettingsAgentId: string
      Role: string
      ToolOptions: obj option }

type RunMuxSubagent = obj -> obj -> string -> string -> string -> obj option -> JS.Promise<string>

type MuxHostAdapter
    (
        runMux: RunMuxSubagent,
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
                let counterVal = sessionScope.NextChildSessionId()
                let cid = "mux-task-" + string counterVal
                let runId = "run-" + System.Guid.NewGuid().ToString("N").Substring(0, 8)

                let rtOpt =
                    match sessionScope.TryFindKey("fallbackRuntime") with
                    | Some obj -> Some(unbox<FallbackRuntimeState> obj)
                    | None -> None

                let mutable started = true

                match rtOpt with
                | Some rt -> started <- rt.StartSubsessionRun(cid, sessionId, runId)
                | None -> ()

                if not started then
                    return Failure(InvalidIntent("subagent", "run", "Subagent session already running"))
                else
                    try
                        try
                            match fromMuxConfig config with
                            | Ok r ->
                                let registry = unbox<ChildAgentRegistry> r.Execution.ChildRegistry
                                registry.RegisterChildAgent(cid, spawn.Role, None)
                            | Error _ -> ()

                            match rtOpt with
                            | Some rt -> rt.UpdateSubsessionRunStatus(cid, runId, SubsessionRunStatus.Running)
                            | None -> ()

                            let! text = runMux deps config spawn.AgentId request.Prompt spawn.Title spawn.ToolOptions

                            match rtOpt with
                            | Some rt -> rt.UpdateSubsessionRunStatus(cid, runId, SubsessionRunStatus.Settled)
                            | None -> ()

                            return Success text
                        with ex ->
                            match rtOpt with
                            | Some rt -> rt.UpdateSubsessionRunStatus(cid, runId, SubsessionRunStatus.Failed)
                            | None -> ()

                            return Failure(translateJsError ex)
                    finally
                        match rtOpt with
                        | Some rt -> rt.ClearSubsessionRun(cid, runId)
                        | None -> ()
            }

        member _.ContinueSubagent(childID: string, agent: string, prompt: string) : JS.Promise<SubagentResponse> =
            promise {
                let runId = "run-" + System.Guid.NewGuid().ToString("N").Substring(0, 8)

                let rtOpt =
                    match sessionScope.TryFindKey("fallbackRuntime") with
                    | Some obj -> Some(unbox<FallbackRuntimeState> obj)
                    | None -> None

                let mutable started = true

                match rtOpt with
                | Some rt -> started <- rt.StartSubsessionRun(childID, sessionId, runId)
                | None -> ()

                if not started then
                    return Failure(InvalidIntent("subagent", "run", "Subagent session already running"))
                else
                    try
                        try
                            match rtOpt with
                            | Some rt -> rt.UpdateSubsessionRunStatus(childID, runId, SubsessionRunStatus.Running)
                            | None -> ()

                            let! text = runMux deps config agent prompt spawn.Title spawn.ToolOptions

                            match rtOpt with
                            | Some rt -> rt.UpdateSubsessionRunStatus(childID, runId, SubsessionRunStatus.Settled)
                            | None -> ()

                            return Success text
                        with ex ->
                            match rtOpt with
                            | Some rt -> rt.UpdateSubsessionRunStatus(childID, runId, SubsessionRunStatus.Failed)
                            | None -> ()

                            return Failure(translateJsError ex)
                    finally
                        match rtOpt with
                        | Some rt -> rt.ClearSubsessionRun(childID, runId)
                        | None -> ()
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
    (runMux: RunMuxSubagent)
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
                    runMux,
                    deps,
                    config,
                    spawn,
                    runtime.Execution.Directory,
                    Id.sessionIdValue runtime.Execution.SessionId,
                    sessionScope
                )

            let registry = unbox<ChildAgentRegistry> runtime.Execution.ChildRegistry
            return! dispatch mimocode adapter toolName args sessionScope (Some registry)
    }
