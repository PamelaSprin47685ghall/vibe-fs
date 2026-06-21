module VibeFs.Mux.BuiltinTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Domain
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
      mode = Dyn.str args "mode"
      cwd = Some (getCwd config) }

let private summarizeWhenNeeded (deps: obj) (config: obj) (options: ExecuteOptions) (output: string) : JS.Promise<string> =
    promise {
        if not (shouldSummarize byteLength output) then
            return prependSafetyWarningForExecution output options
        else
            let prompt = formatPrompt mimocode (ExecutorSummary output) |> List.head
            let! report = runMuxSubagent deps config "executor" prompt "Executor summary" None
            return prependSafetyWarningForExecution report options
    }

let private describeDomainError (error: DomainError) : string =
    match error with
    | UpstreamTimeout seconds -> $"upstream timed out after {seconds}s"
    | UpstreamRefused reason -> $"upstream refused: {reason}"
    | UnknownJsError message -> message
    | SystemPanic message -> message
    | MessageAborted -> "request aborted"
    | SessionBusy -> "session busy"
    | TaskWaitBackgrounded -> "task moved to background"
    | ExecutorExecutableMissing executable -> $"missing executable: {executable}"
    | ParseError(context, detail) -> $"parse error in {context}: {detail}"
    | ToolNotPermitted(agent, tool) -> $"tool '{tool}' not permitted for '{agent}'"
    | InvalidIntent(tool, field, detail) -> $"invalid {tool}.{field}: {detail}"

let addIfSome (entries: ResizeArray<string * obj>) (key: string) (v: 'T option) =
    match v with
    | Some x -> entries.Add(key, box x)
    | None -> ()

let private wrapWebError (label: string) (e: DomainError) =
    $"Web {label} failed: {describeDomainError e}"

let executorTool (deps: obj) : ToolDefinition =
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
      execute =
        fun config args ->
            promise {
                let opts = buildExecutorOptions args config
                let sessionId = Dyn.str config "sessionID"
                let! execResult = VibeFs.Shell.Executor.execute opts sessionId
                let output =
                    match execResult with
                    | Completed o | Truncated(o, _) | Failed o | MissingExecutable(_, o) -> o
                return! summarizeWhenNeeded deps config opts output
            }
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
            promise {
                let path = Dyn.str args "path"
                let offset = optInt args "offset"
                let limit = optInt args "limit"
                match hostReadExec.TryGet() with
                | Some hostExec ->
                    let hostFn = hostExec :?> obj -> obj -> JS.Promise<obj>
                    let! result = hostFn args config
                    return string result
                | None ->
                    return! read (Some (getCwd config)) path offset limit
            }
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
            promise {
                if not (Dyn.has args "file_path") then
                    return describeDomainError (InvalidIntent ("write", "file_path", "missing required parameter"))
                elif not (Dyn.has args "content") then
                    return describeDomainError (InvalidIntent ("write", "content", "missing required parameter"))
                else
                    let filePath = Dyn.str args "file_path"
                    let content = Dyn.str args "content"
                    if System.String.IsNullOrWhiteSpace filePath then
                        return describeDomainError (InvalidIntent ("write", "file_path", "must not be empty"))
                    else
                        let! result = write (Some (getCwd config)) filePath content
                        match result with
                        | Ok msg -> return msg
                        | Error e -> return describeDomainError e
            }
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
              promise {
                  let! r = FuzzyCommandsModule.fuzzyFind p o
                  return r.output
              }
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
              promise {
                  let! r = FuzzyCommandsModule.fuzzyGrep p o
                  return r.output
              }
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
              promise {
                  let numResults = defaultArg (optInt args "numResults") 10
                  let body = createObj [ "query", box query; "max_results", box numResults ]
                  let! result = ollamaPost "web_search" body (if Dyn.isNullish abortSignal then None else Some abortSignal)
                  match result with
                  | Error e -> return wrapWebError "search" e
                  | Ok data ->
                      let items = parseSearchResults (Dyn.get data "results")
                      let rawResults = formatSearchResults items
                      if items.IsEmpty then return rawResults
                      else
                          let prompt = formatPrompt mimocode (WebsearchSummary(whatToSummarize, rawResults)) |> List.head
                          return! runMuxSubagent deps config "executor" prompt "Web search summary" None
              }
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
              promise {
                  let bodyEntries = ResizeArray<(string * obj)>()
                  bodyEntries.Add("url", box url)
                  addIfSome bodyEntries "extract_main" (optBool args "extract_main")
                  addIfSome bodyEntries "prefer_llms_txt" (strField args "prefer_llms_txt")
                  addIfSome bodyEntries "prompt" (strField args "prompt")
                  addIfSome bodyEntries "timeout" (optInt args "timeout")
                  let body = createObj (Seq.toList bodyEntries)
                  let! result = ollamaPost "web_fetch" body (if Dyn.isNullish abortSignal then None else Some abortSignal)
                  match result with
                  | Error e -> return wrapWebError "fetch" e
                  | Ok data ->
                      let title = if Dyn.isNullish (Dyn.get data "title") then None else Some (Dyn.str data "title")
                      let byline = if Dyn.isNullish (Dyn.get data "byline") then None else Some (Dyn.str data "byline")
                      let length_ = if Dyn.isNullish (Dyn.get data "length") then None else Some (unbox<int> (Dyn.get data "length"))
                      let content = if Dyn.isNullish (Dyn.get data "content") then None else Some (Dyn.str data "content")
                      return formatFetchResponse { title = title; byline = byline; length = length_; content = content }
              }
      condition = None }
