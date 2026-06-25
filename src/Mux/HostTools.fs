module VibeFs.Mux.BuiltinTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Domain
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Executor
open VibeFs.Kernel.FuzzyPath
open VibeFs.Kernel.FuzzyQuery
open VibeFs.Kernel.FuzzyFormat
open VibeFs.Kernel.Subagent
open VibeFs.Kernel.ToolCatalog
open VibeFs.Kernel.ToolCopy
open VibeFs.Kernel.ToolResult
open VibeFs.Mux.Delegate
open VibeFs.Mux.Wrappers
open VibeFs.Mux.SubagentTools
open VibeFs.Shell.FileSys
open VibeFs.Shell.FuzzyFinderShell
open VibeFs.Shell.FuzzySearch
open VibeFs.Shell
open VibeFs.Shell.Dyn
open VibeFs.Shell.ExecutorToolsCodec
open VibeFs.Shell.FileToolsCodec
open VibeFs.Shell.FuzzyToolsCodec
open VibeFs.Shell.ToolExecute
open VibeFs.Shell.ToolRuntimeContext

module FuzzyCommandsModule = VibeFs.Shell.FuzzySearch

[<Global("Buffer")>]
let private nodeBuffer : obj = jsNative
let private byteLength (s: string) : int = nodeBuffer?byteLength(s, "utf-8")

let summarizationAgentId = "explore"
let summarizationRole = "executor"
let summarizationAiSettingsAgentId = "explore"

let private summarizeWhenNeeded (deps: obj) (config: obj) (toolNames: string array) (options: ExecuteOptions) (result: ExecuteResult) : JS.Promise<string> =
    promise {
        let output = outputFromResult result
        if not (shouldSummarize byteLength output) then
            return formatToolResponse result None
        else
            let langStr = languageToString options.language
            let timeoutStr = timeoutToString options.timeoutType
            let prompt = formatPrompt mimocode (ExecutorSummary(output, langStr, options.program, options.dependencies, timeoutStr, options.mode)) |> List.head
            let opts = toolOptions toolNames summarizationRole summarizationAiSettingsAgentId
            let! report = runMuxSubagent deps config summarizationAgentId prompt "Executor summary" opts
            return formatToolResponse result (Some report)
    }

let addIfSome (entries: ResizeArray<string * obj>) (key: string) (v: 'T option) =
    match v with
    | Some x -> entries.Add(key, box x)
    | None -> ()

let private formatHostReadResult (result: obj) : string =
    if Dyn.isNullish result then
        ""
    elif Dyn.typeIs result "string" then
        string result
    else
        let content = Dyn.str result "content"
        let warning = Dyn.str result "warning"
        let success = Dyn.get result "success"
        let error = Dyn.str result "error"

        if not (Dyn.isNullish success) then
            if Dyn.truthy success then
                match content, warning with
                | "", "" -> ""
                | "", warning -> warning
                | content, "" -> content
                | content, warning -> $"{content}\n\n{warning}"
            elif error <> "" then
                error
            else
                string result
        elif content <> "" then
            if warning = "" then content else $"{content}\n\n{warning}"
        elif error <> "" then
            error
        else
            string result

let private hostReadResultIsDirectoryError (result: obj) : bool =
    if Dyn.isNullish result || not (Dyn.typeIs result "object") then
        false
    else
        let success = Dyn.get result "success"
        let error = Dyn.str result "error"
        not (Dyn.isNullish success)
        && not (Dyn.truthy success)
        && error.StartsWith "Path is a directory, not a file:"

let executorTool (deps: obj) (toolNames: string array) (_knowledgeGraphRuntime: obj) (sessionScope: VibeFs.Shell.RuntimeScope.RuntimeScope) : ToolDefinition =
    { name = "executor"
      description = description "executor"
      parameters =
        mkSchema
            (createObj
                [ "language", box (strEnumProp Params.executorLanguage [| "shell"; "python"; "javascript" |])
                  "program", box (strProp Params.executorProgram)
                  "dependencies", box (strArrayProp Params.executorDeps)
                  "timeout_type", box (strEnumProp Params.executorTimeout [| "short"; "long"; "last-resort" |])
                  "mode", box (strEnumProp Params.executorMode [| "ro"; "rw" |]) ])
            [| "language"; "program"; "timeout_type"; "mode" |]
      execute = fun config args ->
          match fromMuxConfig config with
          | Error e -> resolveStr (wireEncodeToolError "MuxConfig" e)
          | Ok runtime ->
              let sessionId = runtime.Execution.SessionId
              if sessionId = "" then resolveStr executorRequiresSession
              else
                  match decodeExecutorArgs args with
                  | Error e -> resolveStr (wireDomainFailure "Executor" e)
                  | Ok decoded ->
                      promise {
                          let opts = toExecuteOptions (Some runtime.Execution.Directory) decoded
                          let! execResult =
                              sessionScope.EnqueuePerSession(sessionId, fun () ->
                                  VibeFs.Shell.Executor.execute opts sessionId)
                          return! summarizeWhenNeeded deps config toolNames opts execResult
                      }
      condition = None }

let readTool (_deps: obj) (hostReadExec: HostFunctionCapture) : ToolDefinition =
    { name = "read"
      description = description "read"
      parameters =
        mkSchema
            (createObj
                [ "path", box (strProp Params.readPath)
                  "offset", box (numProp Params.readOffset)
                  "limit", box (numProp Params.readLimit) ])
            [| "path" |]
      execute =
        fun config args ->
            match fromMuxConfig config with
            | Error e -> resolveStr (wireEncodeToolError "MuxConfig" e)
            | Ok runtime ->
                let cwd = Some runtime.Execution.Directory
                promise {
                    match decodeReadArgs args with
                    | Error e -> return wireDecodeFailure "read" e
                    | Ok decoded ->
                        let path = decoded.Path
                        let offset = decoded.Offset
                        let limit = decoded.Limit
                        match hostReadExec.TryGet() with
                        | Some hostExec ->
                            let raw = Dyn.call2 hostExec (readArgsForHost decoded) config
                            let! result =
                                if Dyn.typeIs (Dyn.get raw "then") "function" then
                                    unbox<JS.Promise<obj>> raw
                                else
                                    Promise.lift raw
                            if hostReadResultIsDirectoryError result then
                                return! read cwd path offset limit
                            else
                                return formatHostReadResult result
                        | None ->
                            return! read cwd path offset limit
                }
      condition = None }

let writeTool (_deps: obj) : ToolDefinition =
    { name = "write"
      description = description "write"
      parameters =
        mkSchema
            (createObj
                [ "file_path", box (strProp Params.writeFilePath)
                  "content", box (strProp Params.writeContent) ])
            [| "file_path"; "content" |]
      execute =
        fun config args ->
            match fromMuxConfig config with
            | Error e -> resolveStr (wireEncodeToolError "MuxConfig" e)
            | Ok runtime ->
                let cwd = Some runtime.Execution.Directory
                promise {
                    match decodeWriteArgs args with
                    | Error e -> return wireDecodeFailure "write" e
                    | Ok decoded ->
                        let! result = write cwd decoded.FilePath decoded.Content
                        match result with
                        | Ok msg -> return msg
                        | Error e -> return wireDomainFailure "write" e
                }
      condition = None }

let private searchOptionsFromRuntime (runtime: IToolRuntimeContext) (finderCache: FinderCache) (iteratorStore: VibeFs.Shell.FuzzyIteratorStore.TypedIteratorStore) : SearchOptions =
    let scopeId =
        match runtime.Execution.WorkspaceId with
        | Some w -> w
        | None -> ""
    { cwd = runtime.Execution.Directory
      scopeId = scopeId
      store = Some iteratorStore
      finderCache = finderCache }

let fuzzyFindTool (finderCache: FinderCache) (iteratorStore: VibeFs.Shell.FuzzyIteratorStore.TypedIteratorStore) : ToolDefinition =
    { name = "fuzzy_find"
      description = description "fuzzy_find"
      parameters = mkSchema (createObj [ "pattern", box (strProp Params.fuzzyFindPattern); "path", box (strProp Params.fuzzyFindPath); "limit", box (numProp Params.fuzzyFindLimit); "iterator", box (strProp Params.fuzzyFindIterator) ]) [||]
      execute = fun config args ->
          match fromMuxConfig config with
          | Error e -> resolveStr (wireEncodeToolError "MuxConfig" e)
          | Ok runtime ->
              match decodeFuzzyFindArgs args with
              | Error e -> resolveStr (wireDecodeFailure "fuzzy_find" e)
              | Ok p ->
                  let o = searchOptionsFromRuntime runtime finderCache iteratorStore
                  promise {
                      let! r = FuzzyCommandsModule.fuzzyFind p o
                      return r.output
                  }
      condition = None }

let fuzzyGrepTool (finderCache: FinderCache) (iteratorStore: VibeFs.Shell.FuzzyIteratorStore.TypedIteratorStore) : ToolDefinition =
    { name = "fuzzy_grep"
      description = description "fuzzy_grep"
      parameters = mkSchema (createObj [ "pattern", box (strProp Params.fuzzyGrepPattern); "path", box (strProp Params.fuzzyGrepPath); "exclude", box (strProp Params.fuzzyGrepExclude); "caseSensitive", box (boolProp Params.fuzzyGrepCaseSensitive); "context", box (numProp Params.fuzzyGrepContext); "limit", box (numProp Params.fuzzyGrepLimit); "iterator", box (strProp Params.fuzzyGrepIterator) ]) [||]
      execute = fun config args ->
          match fromMuxConfig config with
          | Error e -> resolveStr (wireEncodeToolError "MuxConfig" e)
          | Ok runtime ->
              match decodeFuzzyGrepArgs args with
              | Error e -> resolveStr (wireDecodeFailure "fuzzy_grep" e)
              | Ok p ->
                  let o = searchOptionsFromRuntime runtime finderCache iteratorStore
                  promise {
                      let! r = FuzzyCommandsModule.fuzzyGrep p o
                      return r.output
                  }
      condition = None }