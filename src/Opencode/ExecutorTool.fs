module VibeFs.Opencode.ExecutorTool

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.Domain
open VibeFs.Kernel.Executor
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Prompts
open VibeFs.Kernel.Subagent
open VibeFs.Kernel.ToolCatalog
open VibeFs.Opencode.ToolSchema
open VibeFs.Opencode.SessionIo
open VibeFs.Opencode.ToolHelpers
open VibeFs.Shell.ChildAgentRegistry

[<Global("Buffer")>]
let private nodeBuffer : obj = jsNative
let private byteLength (s: string) : int = nodeBuffer?byteLength(s, "utf-8")

let executorTool (registry: ChildAgentRegistry) (ctx: obj) : obj =
    let client () = Dyn.get ctx "client"
    define executor
        (box {| language = strReq Params.executorLanguage; program = strReq Params.executorProgram
                dependencies = strArrayOpt Params.executorDeps; timeout_type = strReq Params.executorTimeout
                mode = enumReq [| "ro"; "rw" |] Params.executorMode |})
        (fun args context ->
            let tc = extractToolContext context (Dyn.str ctx "directory")
            let sessionID = Dyn.str tc "sessionID"
            post sessionID (fun () ->
                let lang = parseLanguage (Dyn.str args "language")
                let timeout = parseTimeout (Dyn.str args "timeout_type")
                let deps = if Dyn.isNullish (Dyn.get args "dependencies") then [] else Dyn.get args "dependencies" :?> obj array |> Array.map string |> List.ofArray
                let options : ExecuteOptions =
                    { program = Dyn.str args "program"; language = lang; dependencies = deps
                      timeoutType = timeout; cwd = Some (Dyn.str tc "directory") }
                promise {
                    let! result = VibeFs.Shell.Executor.execute options sessionID
                    let output = match result with Completed o | Truncated(o, _) | Failed o -> o | MissingExecutable(_, o) -> o
                    if not (shouldSummarize byteLength output) then
                        return prependSafetyWarningForExecution output options
                    else
                        let prompt = formatPrompt opencode (ExecutorSummary output) |> List.head
                        let! summary =
                            runSubagentWithCleanup registry (client ()) "executor" "Executor summary" prompt
                                (Dyn.str tc "directory") sessionID context
                        return prependSafetyWarningForExecution summary options
                }))
