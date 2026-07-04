module Wanxiangshu.Shell.MuxSubagentToolExecute

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.HostAdapter
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Subagent
open Wanxiangshu.Kernel.SubagentPrompts
open Wanxiangshu.Kernel.ToolArgs
open Wanxiangshu.Kernel.ToolCopy
open Wanxiangshu.Shell.ErrorClassify
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

type RunMuxSubagent =
    obj -> obj -> string -> string -> string -> obj option -> JS.Promise<string>

type MuxHostAdapter(runMux: RunMuxSubagent, deps: obj, config: obj, spawn: MuxSubagentSpawn, directory: string, sessionId: string, sessionScope: RuntimeScope) =
    interface IHostAdapter with
        member _.WorkspaceRoot = directory
        member _.SessionId = sessionId
        member _.SpawnSubagent(request: SubagentRequest) : JS.Promise<SubagentResponse> =
            promise {
                try
                    let! text = runMux deps config spawn.AgentId request.Prompt spawn.Title spawn.ToolOptions
                    return Success text
                with ex ->
                    return Failure (translateJsError ex)
            }
        member _.RegisterTempFiles(prompt, files) =
            let key = sessionId + "\u0000" + prompt
            sessionScope.RegisterTempFiles(key, files)
        member _.TryGetTempFiles(prompt) =
            let key = sessionId + "\u0000" + prompt
            sessionScope.TryGetTempFiles(key)

let private muxConfigMessage (title: string) (error: DomainError) : string =
    match error with
    | InvalidIntent ("mux", "workspaceId", _) -> muxToolRequiresWorkspaceId title
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
            let adapter = MuxHostAdapter(runMux, deps, config, spawn, runtime.Execution.Directory, Id.sessionIdValue runtime.Execution.SessionId, sessionScope)
            return! dispatch mimocode adapter toolName args
    }