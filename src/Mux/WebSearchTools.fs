module VibeFs.Mux.WebSearchTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.OllamaFormat
open VibeFs.Mux.Contract
open VibeFs.Mux.Delegate
open VibeFs.Mux.Prompts
open VibeFs.Opencode.ToolSchema
open VibeFs.Shell.FuzzyFinderShell
open VibeFs.Shell.FuzzyCoordinator
open VibeFs.Shell.FuzzyCommands
module ToolSchemaModule = VibeFs.Opencode.ToolSchema
module FuzzyCommandsModule = VibeFs.Shell.FuzzyCommands
open VibeFs.Shell.OllamaClient

let fuzzyFindTool (finderCache: FinderCache) : ToolDefinition =
    { name = "fuzzy_find"
      description = ToolSchemaModule.fuzzyFind
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
              let o : SearchOptions =
                  { cwd = Dyn.str config "cwd"
                    scopeId = scopeId
                    store = None
                    finderCache = finderCache }
              async {
                  let! r = FuzzyCommandsModule.fuzzyFind p o |> Async.AwaitPromise
                  return r.output
              }
              |> Async.StartAsPromise
      condition = None }

let fuzzyGrepTool (finderCache: FinderCache) : ToolDefinition =
    { name = "fuzzy_grep"
      description = ToolSchemaModule.fuzzyGrep
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
              let o : SearchOptions =
                  { cwd = Dyn.str config "cwd"
                    scopeId = scopeId
                    store = None
                    finderCache = finderCache }
              async {
                  let! r = FuzzyCommandsModule.fuzzyGrep p o |> Async.AwaitPromise
                  return r.output
              }
              |> Async.StartAsPromise
      condition = None }

let websearchTool (deps: obj) : ToolDefinition =
    { name = "websearch"
      description = websearch
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
                          let prompt = formatMuxWebsearchSummarizerUserPrompt whatToSummarize rawResults
                          let! report = runMuxSubagent deps config "executor" prompt "Web search summary" None |> Async.AwaitPromise
                          return report
                  with ex ->
                      return jsonStringify (createObj [ "success", box false; "error", box ("Web search failed: " + ex.Message) ])
              }
              |> Async.StartAsPromise
      condition = None }

let webfetchTool : ToolDefinition =
    { name = "webfetch"
      description = webfetch
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
