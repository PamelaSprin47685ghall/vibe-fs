module Wanxiangshu.Opencode.SubagentTools

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Shell
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.SubagentPrompts
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Opencode.ToolSchema
open Wanxiangshu.Opencode.SessionIo
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.OpencodeClientCodec
open Wanxiangshu.Shell.PromiseStr
open Wanxiangshu.Shell.SubagentToolExecute
open Wanxiangshu.Shell.FallbackRuntimeState

let private spawnCtx (host: Host) (registry: ChildAgentRegistry) (ctx: obj) (client: obj) (context: obj) (runtime: FallbackRuntimeState) =
    { Host = host; Registry = registry; Client = client; PluginCtx = ctx; ToolContext = context; FallbackRuntime = runtime }

let private executeSubagent (host: Host) (registry: ChildAgentRegistry) (ctx: obj) (toolName: string) (args: obj) (context: obj) (runtime: FallbackRuntimeState) =
    match getClientFromPluginCtx ctx with
    | Error e -> resolveStr (wireEncodeToolError "OpencodeClient" e)
    | Ok client ->
        executeOpencodeSubagentTool runSubagentCoreResult (spawnCtx host registry ctx client context runtime) toolName args

let coderTool (host: Host) (registry: ChildAgentRegistry) (ctx: obj) (runtime: FallbackRuntimeState) : obj =
    let coderRequiredKeys = subagentRequiredKeys "coder"
    define coder
        (subagentZodShape coderRequiredKeys (createObj [ "intents", coderIntentsSchema Params.coderIntents; "tdd", enumReq [| "red"; "green" |] Params.coderTdd; "warn_tdd", enumReq [| WarnTdd.canonicalValue |] Params.warnTddDesc; "_ui", uiParam ]))
        (fun args context -> executeSubagent host registry ctx "coder" args context runtime)

let investigatorTool (host: Host) (registry: ChildAgentRegistry) (ctx: obj) (runtime: FallbackRuntimeState) : obj =
    let investigatorRequiredKeys = subagentRequiredKeys "investigator"
    define investigator
        (subagentZodShape investigatorRequiredKeys (createObj [ "intents", investigatorIntentsSchema Params.investigatorIntents; "_ui", uiParam ]))
        (fun args context -> executeSubagent host registry ctx "investigator" args context runtime)

let meditatorTool (host: Host) (registry: ChildAgentRegistry) (ctx: obj) (runtime: FallbackRuntimeState) : obj =
    let meditatorRequiredKeys = subagentRequiredKeys "meditator"
    define meditator
        (subagentZodShape meditatorRequiredKeys (createObj [ "intent", strReq Params.meditatorIntent; "files", strArrayReq Params.meditatorFiles ]))
        (fun args context -> executeSubagent host registry ctx "meditator" args context runtime)

let browserTool (host: Host) (registry: ChildAgentRegistry) (ctx: obj) (runtime: FallbackRuntimeState) : obj =
    let browserRequiredKeys = subagentRequiredKeys "browser"
    define browser
        (subagentZodShape browserRequiredKeys (createObj [ "intent", strReq Params.browserIntent ]))
        (fun args context -> executeSubagent host registry ctx "browser" args context runtime)