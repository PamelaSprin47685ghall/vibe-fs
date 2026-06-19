module VibeFs.Mux.HostTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Executor
open VibeFs.Kernel.Fuzzy
open VibeFs.Kernel.Prompts
open VibeFs.Kernel.Subagent
open VibeFs.Kernel.ToolCatalog
open VibeFs.Mux.Delegate
open VibeFs.Mux.Wrappers
open VibeFs.Shell.FileSys
open VibeFs.Shell.FuzzyFinderShell
open VibeFs.Shell.FuzzySearch
open VibeFs.Shell.OllamaClient

module FuzzyCommandsModule = VibeFs.Shell.FuzzySearch

[<Global("Buffer")>]
let private nodeBuffer : obj = jsNative
let private byteLength (s: string) : int = nodeBuffer?byteLength(s, "utf-8")

let private getCwd (config: obj) : string =
    match strField config "cwd" with
    | Some v when not (System.String.IsNullOrWhiteSpace v) -> v
    | _ -> defaultArg (strField config "directory") ""

let private buildExecutorOptions (args: obj) (config: obj) : ExecuteOptions =
    { language = parseLanguage (Dyn.str args "language")
      program = Dyn.str args "program"
      dependencies =
          let v = Dyn.get args "dependencies"
          if Dyn.isNullish v then [] else unbox<obj array> v |> Array.map string |> List.ofArray
      timeoutType = parseTimeout (Dyn.str args "timeout_type")
      cwd = Some (getCwd config) }

let private summarizeWhenNeeded (deps: obj) (config: obj) (options: ExecuteOptions) (output: string) : Async<string> =
    async {
        if not (shouldSummarize byteLength output) then
            return prependSafetyWarningForExecution output options
        else
            let prompt = formatPrompt mimocode (ExecutorSummary output) |> List.head
            let! report = runMuxSubagent deps config "executor" prompt "Executor summary" None |> Async.AwaitPromise
            return prependSafetyWarningForExecution report options
    }

let executorTool (deps: obj) : ToolDefinition =
    { name = "executor"
      description = description "executor"
      parameters =
        mkSchema
            (createObj
                [ "language", box (strEnumProp Params.executorLanguage [| "shell"; "python"; "javascript" |])
                  "program", box (strProp Params.executorProgram)
                  "dependencies", box (strArrayProp Params.executorDeps)
                  "timeout_type", box (strEnumProp Params.executorTimeout [| "short"; "long"; "last-resort" |]) ])
            [| "language"; "program"; "timeout_type" |]
      execute =
        fun config args ->
            async {
                let opts = buildExecutorOptions args config
                let sessionId = Dyn.str config "sessionID"
                let! execResult = VibeFs.Shell.Executor.execute opts sessionId |> Async.AwaitPromise
                let output =
                    match execResult with
                    | Completed o | Truncated(o, _) | Failed o | MissingExecutable(_, o) -> o
                return! summarizeWhenNeeded deps config opts output
            }
            |> Async.StartAsPromise
      condition = None }

let readTool (_deps: obj) (hostReadExec: HostReadExec) : ToolDefinition =
    { name = "read"
      description =
        "If path is a directory, returns a formatted directory listing (equivalent to ls -la). Use this instead of running `ls` via executor."
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

let private buildFinderOptions (config: obj) (finderCache: FinderCache) : SearchOptions =
    { cwd = Dyn.str config "cwd"
      scopeId = Dyn.str config "workspaceId"
      store = None
      finderCache = finderCache }

let fuzzyFindTool (finderCache: FinderCache) : ToolDefinition =
    { name = "fuzzy_find"
      description = description "fuzzy_find"
      parameters = mkSchema (createObj [ "pattern", box (strProp Params.fuzzyFindPattern); "path", box (strProp Params.fuzzyFindPath); "limit", box (numProp Params.fuzzyFindLimit); "iterator", box (strProp Params.fuzzyFindIterator) ]) [||]
      execute = fun config args ->
          let scopeId = Dyn.str config "workspaceId"
          if scopeId = "" then resolveStr "fuzzy_find requires workspaceId"
          else
              let p : FuzzyFindParams =
                  { pattern = strField args "pattern"
                    path = strField args "path"
                    limit = optInt args "limit"
                    iterator = strField args "iterator" }
              let o = buildFinderOptions config finderCache
              async {
                  let! r = FuzzyCommandsModule.fuzzyFind p o |> Async.AwaitPromise
                  return r.output
              }
              |> Async.StartAsPromise
      condition = None }

let fuzzyGrepTool (finderCache: FinderCache) : ToolDefinition =
    { name = "fuzzy_grep"
      description = description "fuzzy_grep"
      parameters = mkSchema (createObj [ "pattern", box (strProp Params.fuzzyGrepPattern); "path", box (strProp Params.fuzzyGrepPath); "exclude", box (strProp Params.fuzzyGrepExclude); "caseSensitive", box (boolProp Params.fuzzyGrepCaseSensitive); "context", box (numProp Params.fuzzyGrepContext); "limit", box (numProp Params.fuzzyGrepLimit); "iterator", box (strProp Params.fuzzyGrepIterator) ]) [||]
      execute = fun config args ->
          let scopeId = Dyn.str config "workspaceId"
          if scopeId = "" then resolveStr "fuzzy_grep requires workspaceId"
          else
              let p : FuzzyGrepParams =
                  { pattern = strField args "pattern"
                    path = strField args "path"
                    exclude = parseExcludeField args
                    caseSensitive = optBool args "caseSensitive"
                    context = optInt args "context"
                    limit = optInt args "limit"
                    iterator = strField args "iterator" }
              let o = buildFinderOptions config finderCache
              async {
                  let! r = FuzzyCommandsModule.fuzzyGrep p o |> Async.AwaitPromise
                  return r.output
              }
              |> Async.StartAsPromise
      condition = None }

let websearchTool (deps: obj) : ToolDefinition =
    { name = "websearch"
      description = description "websearch"
      parameters = mkSchema (createObj [ "query", box (strProp Params.websearchQuery); "numResults", box (numProp Params.websearchNumResults); "what_to_summarize", box (strProp Params.websearchWhatToSummarize) ]) [| "query"; "what_to_summarize" |]
      execute = fun config args ->
          let whatToSummarize = defaultArg (strField args "what_to_summarize") ""
          match strField args "query" with
          | None -> resolveStr "Error: `query` must be a string"
          | Some _ when whatToSummarize = "" -> resolveStr "Error: `what_to_summarize` is required"
          | Some query ->
              let abortSignal = Dyn.get config "abortSignal"
              async {
                  try
                      let numResults = defaultArg (optInt args "numResults") 10
                      let body = createObj [ "query", box query; "max_results", box numResults ]
                      let! data = ollamaPost "web_search" body (if Dyn.isNullish abortSignal then None else Some abortSignal) |> Async.AwaitPromise
                      let results = Dyn.get data "results"
                      let items =
                          if Dyn.isNullish results || not (Dyn.isArray results) then []
                          else (results :?> obj array) |> Array.map (fun r -> { title = Dyn.str r "title"; url = Dyn.str r "url"; content = Dyn.str r "content" }) |> List.ofArray
                      let rawResults = formatSearchResults items
                      if items.IsEmpty then return rawResults
                      else
                          let prompt = formatPrompt mimocode (WebsearchSummary(whatToSummarize, rawResults)) |> List.head
                          let! report = runMuxSubagent deps config "executor" prompt "Web search summary" None |> Async.AwaitPromise
                          return report
                  with ex ->
                      return jsonStringify (createObj [ "success", box false; "error", box ("Web search failed: " + ex.Message) ])
              }
              |> Async.StartAsPromise
      condition = None }

let webfetchTool : ToolDefinition =
    { name = "webfetch"
      description = description "webfetch"
      parameters = mkSchema (createObj [ "url", box (strProp Params.webfetchUrl); "extract_main", box (boolProp Params.webfetchExtractMain); "prefer_llms_txt", box (strEnumProp Params.webfetchPreferLlmsTxt [| "auto"; "always"; "never" |]); "prompt", box (strProp Params.webfetchPrompt); "timeout", box (numProp Params.webfetchTimeout) ]) [| "url" |]
      execute = fun config args ->
          match strField args "url" with
          | None -> resolveStr "Error: `url` must be a string"
          | Some url ->
              let abortSignal = Dyn.get config "abortSignal"
              async {
                  let bodyEntries = ResizeArray<(string * obj)>()
                  bodyEntries.Add("url", box url)
                  match optBool args "extract_main" with Some v -> bodyEntries.Add("extract_main", box v) | None -> ()
                  match strField args "prefer_llms_txt" with Some v -> bodyEntries.Add("prefer_llms_txt", box v) | None -> ()
                  match strField args "prompt" with Some v -> bodyEntries.Add("prompt", box v) | None -> ()
                  match optInt args "timeout" with Some v -> bodyEntries.Add("timeout", box v) | None -> ()
                  let body = createObj (Seq.toList bodyEntries)
                  try
                      let! data = ollamaPost "web_fetch" body (if Dyn.isNullish abortSignal then None else Some abortSignal) |> Async.AwaitPromise
                      let title = if Dyn.isNullish (Dyn.get data "title") then None else Some (Dyn.str data "title")
                      let byline = if Dyn.isNullish (Dyn.get data "byline") then None else Some (Dyn.str data "byline")
                      let length_ = if Dyn.isNullish (Dyn.get data "length") then None else Some (unbox<int> (Dyn.get data "length"))
                      let content = if Dyn.isNullish (Dyn.get data "content") then None else Some (Dyn.str data "content")
                      return formatFetchResponse { title = title; byline = byline; length = length_; content = content }
                  with ex ->
                      return jsonStringify (createObj [ "success", box false; "error", box ("Web fetch failed: " + ex.Message) ])
              }
              |> Async.StartAsPromise
      condition = None }
