module VibeFs.Opencode.SearchTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.Domain
open VibeFs.Kernel.Fuzzy
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Prompts
open VibeFs.Kernel.Subagent
open VibeFs.Kernel.ToolCatalog
open VibeFs.Shell.OllamaClient
open VibeFs.Opencode.ToolSchema
open VibeFs.Opencode.SessionIo
open VibeFs.Opencode.ToolHelpers
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.FuzzyFinderShell
open VibeFs.Shell.FuzzySearch
module ToolSchemaModule = VibeFs.Opencode.ToolSchema
module FuzzyCommandsModule = VibeFs.Shell.FuzzySearch

let fuzzyFindTool (finderCache: FinderCache) : obj =
    define ToolSchemaModule.fuzzyFind
        (box {| pattern = strMinNullish 1 Params.fuzzyFindPattern; path = strOpt Params.fuzzyFindPath
                limit = intMinNullish 1 Params.fuzzyFindLimit; iterator = strOpt Params.fuzzyFindIterator |})
        (fun args context ->
            let scopeId = Dyn.str context "sessionID"
            if scopeId = "" then resolveStr (formatDomainError "fuzzy_find" (InvalidIntent ("fuzzy_find", "session", "requires an active session")))
            else
                let p : FuzzyFindParams =
                    { pattern = optStr args "pattern"
                      path = optStr args "path"
                      limit = optInt args "limit"
                      iterator = optStr args "iterator" }
                let o : SearchOptions =
                    { cwd = Dyn.str context "directory"
                      scopeId = scopeId
                      store = None
                      finderCache = finderCache }
                promise {
                    let! r = FuzzyCommandsModule.fuzzyFind p o
                    return r.output
                })

let fuzzyGrepTool (finderCache: FinderCache) : obj =
    define ToolSchemaModule.fuzzyGrep
        (box {| pattern = strMinNullish 1 Params.fuzzyGrepPattern; path = strOpt Params.fuzzyGrepPath
                exclude = excludeOpt Params.fuzzyGrepExclude; caseSensitive = boolOpt Params.fuzzyGrepCaseSensitive
                context = intMinNullish 0 Params.fuzzyGrepContext; limit = intMinNullish 1 Params.fuzzyGrepLimit
                iterator = strOpt Params.fuzzyGrepIterator |})
        (fun args context ->
            let scopeId = Dyn.str context "sessionID"
            if scopeId = "" then resolveStr (formatDomainError "fuzzy_grep" (InvalidIntent ("fuzzy_grep", "session", "requires an active session")))
            else
                let p : FuzzyGrepParams =
                    { pattern = optStr args "pattern"
                      path = optStr args "path"
                      exclude = parseExcludeField args
                      caseSensitive = optBool args "caseSensitive"
                      context = optInt args "context"
                      limit = optInt args "limit"
                      iterator = optStr args "iterator" }
                let o : SearchOptions =
                    { cwd = Dyn.str context "directory"
                      scopeId = scopeId
                      store = None
                      finderCache = finderCache }
                promise {
                    let! r = FuzzyCommandsModule.fuzzyGrep p o
                    return r.output
                })

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
                        let results = Dyn.get data "results"
                        let items =
                            if Dyn.isNullish results || not (Dyn.isArray results) then []
                            else (results :?> obj array) |> Array.map (fun r -> { title = Dyn.str r "title"; url = Dyn.str r "url"; content = Dyn.str r "content" }) |> List.ofArray
                        let rawResults = formatSearchResults items
                        if items.IsEmpty then return rawResults
                        else
                            let tc = extractToolContext context (Dyn.str ctx "directory")
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
                    match optBool args "extract_main" with Some v -> bodyEntries.Add("extract_main", box v) | None -> ()
                    match optStr args "prefer_llms_txt" with Some v -> bodyEntries.Add("prefer_llms_txt", box v) | None -> ()
                    match optStr args "prompt" with Some v -> bodyEntries.Add("prompt", box v) | None -> ()
                    match optInt args "timeout" with Some v -> bodyEntries.Add("timeout", box v) | None -> ()
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
