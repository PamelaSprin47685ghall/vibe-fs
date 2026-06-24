module VibeFs.Opencode.SubagentTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Shell

open VibeFs.Kernel.HostTools
open VibeFs.Kernel.SubagentPrompts
open VibeFs.Kernel.Subagent
open VibeFs.Shell.SubagentIntentsCodec
open VibeFs.Shell.SubagentPromptBuild
open VibeFs.Shell.SubagentSimpleArgsCodec
open VibeFs.Shell.SubagentSpawn
open VibeFs.Kernel.ToolCatalog
open VibeFs.Opencode.ToolSchema
open VibeFs.Opencode.SessionIo
open VibeFs.Opencode.ToolHelpers
open VibeFs.Shell.PromiseStr
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Kernel.Domain
open VibeFs.Shell.Dyn
open VibeFs.Shell.ToolRuntimeContext

let coderTool (registry: ChildAgentRegistry) (ctx: obj) : obj =
    let client () = Dyn.get ctx "client"
    define coder
        (box {| intents = coderIntentsSchema Params.coderIntents; tdd = enumReq [| "red"; "green" |] Params.coderTdd; _ui = uiParam |})
        (fun args context ->
            match decodeIntentsField "coder" args with
            | Error e -> resolveStr (ToolHelpers.formatDomainError "coder" e)
            | Ok intents ->
                match parallelPromptsFromIntents opencode "coder" parseCoderIntents Coder intents with
                | Error e -> resolveStr (ToolHelpers.formatDomainError "coder" e)
                | Ok prompts ->
                    let runtime = fromOpencode context (pluginDirectoryFromCtx ctx)
                    let spawnOne prompt =
                        runSubagent
                            registry
                            (client ())
                            "coder"
                            "Coder"
                            prompt
                            runtime.Execution.Directory
                            runtime.Execution.SessionId
                            context
                            (box null)
                    runParallelSpawns prompts spawnOne)

let investigatorTool (registry: ChildAgentRegistry) (ctx: obj) : obj =
    let client () = Dyn.get ctx "client"
    define investigator
        (box {| intents = investigatorIntentsSchema Params.investigatorIntents
                _ui = uiParam |})
        (fun args context ->
            match decodeIntentsField "investigator" args with
            | Error e -> resolveStr (ToolHelpers.formatDomainError "investigator" e)
            | Ok intents ->
                match parallelPromptsFromIntents opencode "investigator" parseInvestigatorIntents Investigator intents with
                | Error e -> resolveStr (ToolHelpers.formatDomainError "investigator" e)
                | Ok prompts ->
                    let runtime = fromOpencode context (pluginDirectoryFromCtx ctx)
                    let spawnOne prompt =
                        runSubagent
                            registry
                            (client ())
                            "investigator"
                            "Investigator"
                            prompt
                            runtime.Execution.Directory
                            runtime.Execution.SessionId
                            context
                            (box null)
                    runParallelSpawns prompts spawnOne)

let meditatorTool (registry: ChildAgentRegistry) (ctx: obj) : obj =
    let client () = Dyn.get ctx "client"
    define meditator
        (box {| intent = strReq Params.meditatorIntent; files = strArrayOpt Params.meditatorFiles |})
        (fun args context ->
            match decodeMeditatorArgs args with
            | Error e -> resolveStr (ToolHelpers.formatDomainError "meditator" e)
            | Ok decoded ->
                let runtime = fromOpencode context (pluginDirectoryFromCtx ctx)
                promise {
                    let! promptResult = meditatorPromptFromFiles opencode runtime.Execution.Directory decoded.Intent decoded.Files
                    match promptResult with
                    | Error e -> return ToolHelpers.formatDomainError "meditator" e
                    | Ok prompt ->
                        return! runSubagent registry (client ()) "meditator" "Meditator" prompt
                            runtime.Execution.Directory runtime.Execution.SessionId context (box null)
                })

let browserTool (registry: ChildAgentRegistry) (ctx: obj) : obj =
    let client () = Dyn.get ctx "client"
    define browser
        (box {| intent = strReq Params.browserIntent |})
        (fun args context ->
            match decodeBrowserArgs args with
            | Error e -> resolveStr (ToolHelpers.formatDomainError "browser" e)
            | Ok decoded ->
                let runtime = fromOpencode context (pluginDirectoryFromCtx ctx)
                runSubagent registry (client ()) "browser" "Browser" (browserPromptText opencode decoded.Intent)
                    runtime.Execution.Directory runtime.Execution.SessionId context (box null))