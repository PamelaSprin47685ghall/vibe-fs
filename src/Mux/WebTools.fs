module VibeFs.Mux.WebTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Domain
open VibeFs.Shell

open VibeFs.Kernel.HostTools
open VibeFs.Kernel.SearchPrompts
open VibeFs.Shell.WebSearchCodec
open VibeFs.Kernel.Subagent
open VibeFs.Kernel.ToolCatalog
open VibeFs.Mux.BuiltinTools
open VibeFs.Mux.Delegate
open VibeFs.Mux.SubagentTools
open VibeFs.Mux.Wrappers
open VibeFs.Shell.OllamaClient
open VibeFs.Shell.Dyn

let private wrapWebError (label: string) (e: DomainError) =
    $"Web {label} failed: {formatDomainError e}"

let websearchTool (deps: obj) (toolNames: string array) : ToolDefinition =
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
                          let opts = toolOptions toolNames summarizationRole summarizationAiSettingsAgentId
                          return! runMuxSubagent deps config summarizationAgentId prompt "Web search summary" opts
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