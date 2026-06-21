module VibeFs.Opencode.SearchTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.Domain
open VibeFs.Kernel.Fuzzy
open VibeFs.Kernel.HostTools
open VibeFs.Mux.BuiltinTools
open VibeFs.Kernel.Prompts
open VibeFs.Kernel.Subagent
open VibeFs.Kernel.ToolCatalog
open VibeFs.Shell.OllamaClient
open VibeFs.Opencode.ToolSchema
open VibeFs.Opencode.SessionIo
open VibeFs.Opencode.ToolHelpers
open VibeFs.Mux.Wrappers
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.FuzzyFinderShell
open VibeFs.Shell.FuzzySearch
module ToolSchemaModule = VibeFs.Opencode.ToolSchema
module FuzzyCommandsModule = VibeFs.Shell.FuzzySearch

let private buildFuzzyTool (description: string) (args: obj) (toolName: string) (buildParams: obj -> 'P) (execute: 'P -> SearchOptions -> JS.Promise<SearchOutcome>) (finderCache: FinderCache) : obj =
    define description
        args
        (fun args context ->
            let scopeId = Dyn.str context "sessionID"
            if scopeId = "" then resolveStr (formatDomainError toolName (InvalidIntent (toolName, "session", "requires an active session")))
            else
                let p = buildParams args
                let o : SearchOptions =
                    { cwd = Dyn.str context "directory"
                      scopeId = scopeId
                      store = None
                      finderCache = finderCache }
                promise {
                    let! r = execute p o
                    return r.output
                })

let fuzzyFindTool (finderCache: FinderCache) : obj =
    buildFuzzyTool
        ToolSchemaModule.fuzzyFind
        (box {| pattern = strMinNullish 1 Params.fuzzyFindPattern; path = strOpt Params.fuzzyFindPath
                limit = intMinNullish 1 Params.fuzzyFindLimit; iterator = strOpt Params.fuzzyFindIterator |})
        "fuzzy_find"
        (fun args ->
            { pattern = optStr args "pattern"
              path = optStr args "path"
              limit = optInt args "limit"
              iterator = optStr args "iterator" })
        FuzzyCommandsModule.fuzzyFind
        finderCache

let fuzzyGrepTool (finderCache: FinderCache) : obj =
    buildFuzzyTool
        ToolSchemaModule.fuzzyGrep
        (box {| pattern = strMinNullish 1 Params.fuzzyGrepPattern; path = strOpt Params.fuzzyGrepPath
                exclude = excludeOpt Params.fuzzyGrepExclude; caseSensitive = boolOpt Params.fuzzyGrepCaseSensitive
                context = intMinNullish 0 Params.fuzzyGrepContext; limit = intMinNullish 1 Params.fuzzyGrepLimit
                iterator = strOpt Params.fuzzyGrepIterator |})
        "fuzzy_grep"
        (fun args ->
            { pattern = optStr args "pattern"
              path = optStr args "path"
              exclude = parseExcludeField args
              caseSensitive = optBool args "caseSensitive"
              context = optInt args "context"
              limit = optInt args "limit"
              iterator = optStr args "iterator" })
        FuzzyCommandsModule.fuzzyGrep
        finderCache

let private abortSignal (context: obj) : obj =
    if Dyn.isNullish context then null else Dyn.get context "abort"

let websearchTool (registry: ChildAgentRegistry) (ctx: obj) : obj =
    let client () = Dyn.get ctx "client"
    define websearch
        (box {| query = strReq Params.websearchQuery
                numResults = numOpt Params.websearchNumResults
                what_to_summarize = strReq Params.websearchWhatToSummarize |})
        (fun args context ->
            let query = Dyn.str args "query"
            let whatToSummarize = Dyn.str args "what_to_summarize"
            let tc = extractToolContext context (Dyn.str ctx "directory")
            if query = "" then resolveStr (formatDomainError "Web search" (InvalidIntent ("websearch", "query", "required")))
            elif whatToSummarize = "" then resolveStr (formatDomainError "Web search" (InvalidIntent ("websearch", "what_to_summarize", "required")))
            else
                let signal = abortSignal context
                promise {
                    let numResults = defaultArg (optInt args "numResults") 10
                    let body = createObj [ "query", box query; "max_results", box numResults ]
                    let! result = ollamaPost "web_search" body (if Dyn.isNullish signal then None else Some signal)
                    match result with
                    | Error e -> return formatDomainError "Web search" e
                    | Ok data ->
                        let items = parseSearchResults (Dyn.get data "results")
                        let rawResults = formatSearchResults items
                        if items.IsEmpty then
                            return rawResults
                        else
                            let prompt = formatPrompt opencode (WebsearchSummary(whatToSummarize, rawResults)) |> List.head
                            return! runSubagentWithCleanup registry (client ()) "executor" "Web search summary" prompt
                                (Dyn.str tc "directory") (Dyn.str tc "sessionID") context
                })

let webfetchTool () : obj =
    define webfetch
        (box {| url = strReq Params.webfetchUrl
                extract_main = boolOpt Params.webfetchExtractMain
                prefer_llms_txt = enumOpt [| "auto"; "always"; "never" |] Params.webfetchPreferLlmsTxt
                prompt = strOpt Params.webfetchPrompt
                timeout = numOpt Params.webfetchTimeout |})
        (fun args context ->
            let url = Dyn.str args "url"
            if url = "" then resolveStr (formatDomainError "Web fetch" (InvalidIntent ("webfetch", "url", "required")))
            else
                let signal = abortSignal context
                promise {
                    let bodyEntries = ResizeArray<(string * obj)>()
                    bodyEntries.Add("url", box url)
                    addIfSome bodyEntries "extract_main" (optBool args "extract_main")
                    addIfSome bodyEntries "prefer_llms_txt" (optStr args "prefer_llms_txt")
                    addIfSome bodyEntries "prompt" (optStr args "prompt")
                    addIfSome bodyEntries "timeout" (optInt args "timeout")
                    let body = createObj (Seq.toList bodyEntries)
                    let! result = ollamaPost "web_fetch" body (if Dyn.isNullish signal then None else Some signal)
                    match result with
                    | Error e -> return formatDomainError "Web fetch" e
                    | Ok data ->
                        let title = if Dyn.isNullish (Dyn.get data "title") then None else Some (Dyn.str data "title")
                        let byline = if Dyn.isNullish (Dyn.get data "byline") then None else Some (Dyn.str data "byline")
                        let length_ = if Dyn.isNullish (Dyn.get data "length") then None else Some (unbox<int> (Dyn.get data "length"))
                        let content = if Dyn.isNullish (Dyn.get data "content") then None else Some (Dyn.str data "content")
                        return formatFetchResponse { title = title; byline = byline; length = length_; content = content }
                })
