module Wanxiangshu.Opencode.SubagentTools

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Shell
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.SubagentPrompts
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Kernel.HostAdapter
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Opencode.ToolSchema
open Wanxiangshu.Opencode.SessionIo
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.OpencodeClientCodec
open Wanxiangshu.Shell.PromiseStr
open Wanxiangshu.Shell.SubagentDispatcher
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.ToolRuntimeContext
open Wanxiangshu.Shell.RuntimeScope

type RunSubagentCoreResult =
    FallbackRuntimeState -> ChildAgentRegistry -> obj -> string -> string -> string -> string -> string -> obj -> obj -> bool -> JS.Promise<Result<string, DomainError>>

type OpencodeHostAdapter(runCore: RunSubagentCoreResult, registry: ChildAgentRegistry, client: obj, ctx: obj, toolContext: obj, fallbackRuntime: FallbackRuntimeState, sessionScope: RuntimeScope) =
    let workspaceRoot = (fromOpencode toolContext (pluginDirectoryFromCtx ctx)).Execution.Directory
    let sessionId = (fromOpencode toolContext (pluginDirectoryFromCtx ctx)).Execution.SessionId |> Id.sessionIdValue

    interface IHostAdapter with
        member _.WorkspaceRoot = workspaceRoot
        member _.SessionId = sessionId
        member _.SpawnSubagent(request: SubagentRequest) : JS.Promise<SubagentResponse> =
            let agent =
                match request.Role with
                | Coder -> "coder"
                | Investigator -> "investigator"
                | Meditator -> "meditator"
                | Browser -> "browser"
            promise {
                let! result = runCore fallbackRuntime registry client agent request.Title request.Prompt workspaceRoot sessionId toolContext (box null) false
                return
                    match result with
                    | Ok text -> Success text
                    | Error err -> Failure err
            }
        member _.RegisterTempFiles(prompt, files) =
            let key = sessionId + "\u0000" + prompt
            sessionScope.RegisterTempFiles(key, files)
        member _.TryGetTempFiles(prompt) =
            let key = sessionId + "\u0000" + prompt
            sessionScope.TryGetTempFiles(key)

let private executeSubagent (host: Host) (registry: ChildAgentRegistry) (ctx: obj) (toolName: string) (args: obj) (context: obj) (runtime: FallbackRuntimeState) (sessionScope: RuntimeScope) =
    match getClientFromPluginCtx ctx with
    | Error e -> resolveStr (wireEncodeToolError "OpencodeClient" e)
    | Ok client ->
        let adapter = OpencodeHostAdapter(runSubagentCoreResult, registry, client, ctx, context, runtime, sessionScope)
        dispatch host adapter toolName args

let coderTool (host: Host) (registry: ChildAgentRegistry) (ctx: obj) (runtime: FallbackRuntimeState) (sessionScope: RuntimeScope) : obj =
    let coderRequiredKeys = subagentRequiredKeys "coder"
    define coder
        (subagentZodShape coderRequiredKeys (createObj [ "intents", coderIntentsSchema Params.coderIntents; "tdd", enumReq [| "red"; "green" |] Params.coderTdd; "_ui", uiParam ]))
        (fun args context -> executeSubagent host registry ctx "coder" args context runtime sessionScope)

let investigatorTool (host: Host) (registry: ChildAgentRegistry) (ctx: obj) (runtime: FallbackRuntimeState) (sessionScope: RuntimeScope) : obj =
    let investigatorRequiredKeys = subagentRequiredKeys "investigator"
    define investigator
        (subagentZodShape investigatorRequiredKeys (createObj [ "intents", investigatorIntentsSchema Params.investigatorIntents; "_ui", uiParam ]))
        (fun args context -> executeSubagent host registry ctx "investigator" args context runtime sessionScope)

let meditatorTool (host: Host) (registry: ChildAgentRegistry) (ctx: obj) (runtime: FallbackRuntimeState) (sessionScope: RuntimeScope) : obj =
    let meditatorRequiredKeys = subagentRequiredKeys "meditator"
    define meditator
        (subagentZodShape meditatorRequiredKeys (createObj [ "intent", strReq Params.meditatorIntent; "files", strArrayReq Params.meditatorFiles ]))
        (fun args context -> executeSubagent host registry ctx "meditator" args context runtime sessionScope)

let browserTool (host: Host) (registry: ChildAgentRegistry) (ctx: obj) (runtime: FallbackRuntimeState) (sessionScope: RuntimeScope) : obj =
    let browserRequiredKeys = subagentRequiredKeys "browser"
    define browser
        (subagentZodShape browserRequiredKeys (createObj [ "intent", strReq Params.browserIntent ]))
        (fun args context -> executeSubagent host registry ctx "browser" args context runtime sessionScope)
