module VibeFs.Opencode.SubagentTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Shell
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.SubagentPrompts
open VibeFs.Kernel.ToolCatalog
open VibeFs.Opencode.ToolSchema
open VibeFs.Opencode.SessionIo
open VibeFs.Kernel.ToolResult
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.OpencodeClientCodec
open VibeFs.Shell.PromiseStr
open VibeFs.Shell.SubagentToolExecute

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