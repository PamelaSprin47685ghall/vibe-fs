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

let private spawnCtx (host: Host) (registry: ChildAgentRegistry) (ctx: obj) (client: obj) (context: obj) =
    { Host = host; Registry = registry; Client = client; PluginCtx = ctx; ToolContext = context }

let private executeSubagent (host: Host) (registry: ChildAgentRegistry) (ctx: obj) (toolName: string) (args: obj) (context: obj) =
    match getClientFromPluginCtx ctx with
    | Error e -> resolveStr (wireEncodeToolError "OpencodeClient" e)
    | Ok client ->
        executeOpencodeSubagentTool runSubagentCoreResult (spawnCtx host registry ctx client context) toolName args

let coderTool (host: Host) (registry: ChildAgentRegistry) (ctx: obj) : obj =
    let coderRequiredKeys = subagentRequiredKeys "coder"
    define coder
        (subagentZodShape coderRequiredKeys (createObj [ "intents", coderIntentsSchema Params.coderIntents; "tdd", enumReq [| "red"; "green" |] Params.coderTdd; "_ui", uiParam ]))
        (fun args context -> executeSubagent host registry ctx "coder" args context)

let investigatorTool (host: Host) (registry: ChildAgentRegistry) (ctx: obj) : obj =
    let investigatorRequiredKeys = subagentRequiredKeys "investigator"
    define investigator
        (subagentZodShape investigatorRequiredKeys (createObj [ "intents", investigatorIntentsSchema Params.investigatorIntents; "_ui", uiParam ]))
        (fun args context -> executeSubagent host registry ctx "investigator" args context)

let meditatorTool (host: Host) (registry: ChildAgentRegistry) (ctx: obj) : obj =
    let meditatorRequiredKeys = subagentRequiredKeys "meditator"
    define meditator
        (subagentZodShape meditatorRequiredKeys (createObj [ "intent", strReq Params.meditatorIntent; "files", strArrayReq Params.meditatorFiles ]))
        (fun args context -> executeSubagent host registry ctx "meditator" args context)

let browserTool (host: Host) (registry: ChildAgentRegistry) (ctx: obj) : obj =
    let browserRequiredKeys = subagentRequiredKeys "browser"
    define browser
        (subagentZodShape browserRequiredKeys (createObj [ "intent", strReq Params.browserIntent ]))
        (fun args context -> executeSubagent host registry ctx "browser" args context)