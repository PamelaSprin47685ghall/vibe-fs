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
let private resolveStr (text: string) : JS.Promise<string> = Promise.lift text

let private parseExecutorLanguage (value: string) : Result<ExecutorLanguage, string> =
    match value.Trim().ToLowerInvariant() with
    | "shell" -> Ok Shell
    | "python" -> Ok Python
    | "javascript" -> Ok Javascript
    | _ -> Error (formatDomainError "Executor" (InvalidIntent ("executor", "language", "expected shell, python, or javascript")))

let executorTool (registry: ChildAgentRegistry) (ctx: obj) : obj =
    let client () = Dyn.get ctx "client"
    define executor
        (box {|
            language = enumReq [| "shell"; "python"; "javascript" |] Params.executorLanguage
            program = strReq Params.executorProgram
            dependencies = strArrayOpt Params.executorDeps
            timeout_type = enumReq [| "short"; "long"; "last-resort" |] Params.executorTimeout
            mode = enumReq [| "ro"; "rw" |] Params.executorMode
        |})
        (fun args context ->
            match parseExecutorLanguage (Dyn.str args "language") with
            | Error message -> resolveStr message
            | Ok lang ->
                let tc = extractToolContext context (Dyn.str ctx "directory")
                let sessionID = Dyn.str tc "sessionID"
                post sessionID (fun () ->
                    let timeout = parseTimeout (Dyn.str args "timeout_type")
                    let deps = if Dyn.isNullish (Dyn.get args "dependencies") then [] else Dyn.get args "dependencies" :?> obj array |> Array.map string |> List.ofArray
                    let options : ExecuteOptions =
                        { program = Dyn.str args "program"; language = lang; dependencies = deps
                          timeoutType = timeout; mode = Dyn.str args "mode"; cwd = Some (Dyn.str tc "directory") }
                    promise {
                        let! result = VibeFs.Shell.Executor.execute options sessionID
                        let output = match result with Completed o | Truncated(o, _) | Failed o -> o | MissingExecutable(_, o) -> o
                        if not (shouldSummarize byteLength output) then
                            return prependSafetyWarningForExecution output options
                        else
                            let langStr = languageToString options.language
                            let timeoutStr = timeoutToString options.timeoutType
                            let prompt = formatPrompt opencode (ExecutorSummary(output, langStr, options.program, options.dependencies, timeoutStr, options.mode)) |> List.head
                            let! summary =
                                runSubagentWithCleanup registry (client ()) "executor" "Executor summary" prompt
                                    (Dyn.str tc "directory") sessionID context
                            return prependSafetyWarningForExecution summary options
                    }))
