module VibeFs.MuxPlugin.MuxTools.IoTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.ExecutorKernel
open VibeFs.Mux.Contract
open VibeFs.MuxPlugin.Delegate
open VibeFs.MuxPlugin.MuxPrompts
open VibeFs.MuxPlugin.MuxTools.Shared
open VibeFs.Opencode.ToolCopy
open VibeFs.Shell.Read
open VibeFs.Shell.Write

[<Global("Buffer")>]
let private nodeBuffer : obj = jsNative
let private byteLength (s: string) : int = nodeBuffer?byteLength(s, "utf-8")

let private getCwd (config: obj) : string =
    match strField config "cwd" with
    | Some v when not (System.String.IsNullOrWhiteSpace v) -> v
    | _ -> defaultArg (strField config "directory") ""

let private parseLanguage (value: string) : ExecutorLanguage =
    match value.ToLowerInvariant() with
    | "python" -> Python
    | "javascript" -> Javascript
    | _ -> Shell

let private parseTimeout (value: string) : ExecutorTimeoutType =
    match value.ToLowerInvariant() with
    | "long" -> Long
    | "last-resort" -> LastResort
    | _ -> Short

let private buildExecutorOptions (args: obj) (config: obj) : ExecuteOptions =
    { language = parseLanguage (Dyn.str args "language")
      program = Dyn.str args "program"
      dependencies =
          let v = Dyn.get args "dependencies"
          if Dyn.isNullish v then [] else unbox<obj array> v |> Array.map string |> List.ofArray
      timeoutType = parseTimeout (Dyn.str args "timeout")
      cwd = Some (getCwd config) }

let private summarizeWhenNeeded (deps: obj) (config: obj) (output: string) : Async<string> =
    async {
        if not (shouldSummarize byteLength output) then
            return output
        else
            let prompt = formatMuxExecutorSummarizerUserPrompt output
            let! report = runMuxSubagent deps config "executor" prompt "Executor summary" None |> Async.AwaitPromise
            return report
    }

let executorTool (deps: obj) : ToolDefinition =
    { name = "executor"
      description = executor
      parameters =
        mkSchema
            (createObj
                [ "language", box (strEnumProp Params.executorLanguage [| "shell"; "python"; "javascript" |])
                  "program", box (strProp Params.executorProgram)
                  "dependencies", box (strArrayProp Params.executorDeps)
                  "timeout_type", box (strEnumProp Params.executorTimeout [| "short"; "long"; "last-resort" |]) ])
            [| "language"; "program"; "timeout" |]
      execute =
        fun config args ->
            async {
                let opts = buildExecutorOptions args config
                let sessionId = Dyn.str config "sessionID"
                let! execResult = VibeFs.Shell.ExecutorShell.execute opts sessionId |> Async.AwaitPromise
                let output =
                    match execResult with
                    | Completed o | Truncated(o, _) | Failed o | MissingExecutable(_, o) -> o
                return! summarizeWhenNeeded deps config output
            }
            |> Async.StartAsPromise
      condition = None }

/// Per-instance ref for the host-provided file_read executor, captured by wrappers.
type HostReadExec = obj option ref

let readTool (_deps: obj) (hostReadExec: HostReadExec) : ToolDefinition =
    { name = "read"
      description =
        "If path is a directory, returns a formatted directory listing (equivalent to ls -la). Use this instead of running `ls` via runner."
      parameters =
        mkSchema
            (createObj
                [ "path", box (strProp "The absolute or relative path to read")
                  "offset", box (numProp "Line to start from, 1-indexed")
                  "limit", box (numProp "Maximum lines to read") ])
            [| "path" |]
      execute =
        fun config args ->
            async {
                let path = Dyn.str args "path"
                let offset = optInt args "offset"
                let limit = optInt args "limit"
                match hostReadExec.Value with
                | Some hostExec ->
                    let hostFn = hostExec :?> obj -> obj -> JS.Promise<obj>
                    let! result = hostFn args config |> Async.AwaitPromise
                    return string result
                | None ->
                    return! read (Some (getCwd config)) path offset limit
            }
            |> Async.StartAsPromise
      condition = None }

let writeTool (_deps: obj) : ToolDefinition =
    { name = "write"
      description =
        "Write content to a file. Resolves relative paths against the current working directory, creates parent directories if they don't exist, and runs syntax checking on the written content."
      parameters =
        mkSchema
            (createObj
                [ "file_path", box (strProp "The absolute or relative path of the file to write")
                  "content", box (strProp "The content to write to the file") ])
            [| "file_path"; "content" |]
      execute =
        fun config args ->
            async {
                if not (Dyn.has args "file_path") then
                    return "Error: missing required parameter 'file_path'"
                elif not (Dyn.has args "content") then
                    return "Error: missing required parameter 'content'"
                else
                    let filePath = Dyn.str args "file_path"
                    let content = Dyn.str args "content"
                    if System.String.IsNullOrWhiteSpace filePath then
                        return "Error: 'file_path' must not be empty"
                    else
                        try
                            return! write (Some (getCwd config)) filePath content
                        with ex ->
                            return $"Error writing '{filePath}': {ex.Message}"
            }
            |> Async.StartAsPromise
      condition = None }
