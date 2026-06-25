module VibeFs.Opencode.SearchTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Shell

open VibeFs.Kernel.Domain
open VibeFs.Kernel.FuzzyPath
open VibeFs.Kernel.FuzzyQuery
open VibeFs.Kernel.FuzzyFormat
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.SearchPrompts
open VibeFs.Shell.WebSearchCodec
open VibeFs.Shell.WebToolsCodec
open VibeFs.Shell.ToolRuntimeContext
open VibeFs.Kernel.Subagent
open VibeFs.Kernel.ToolCatalog
open VibeFs.Kernel.ToolCopy
open VibeFs.Kernel.ToolResult
open VibeFs.Shell.WebSearchApi
open VibeFs.Opencode.ToolSchema
open VibeFs.Opencode.SessionIo
open VibeFs.Opencode.ToolHelpers
open VibeFs.Shell.PromiseStr
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.SubagentToolExecute
open VibeFs.Shell.FuzzyFinderShell
open VibeFs.Shell.FuzzySearch
open VibeFs.Shell.FuzzyToolsCodec
open VibeFs.Shell.ToolExecute
open VibeFs.Shell.Dyn
open VibeFs.Shell.OpencodeClientCodec
module ToolSchemaModule = VibeFs.Opencode.ToolSchema
module FuzzyCommandsModule = VibeFs.Shell.FuzzySearch

let private addIfSome (entries: ResizeArray<string * obj>) (key: string) (v: 'T option) =
    match v with
    | Some x -> entries.Add(key, box x)
    | None -> ()

let private buildFuzzyTool (description: string) (args: obj) (toolName: string) (decode: obj -> Result<'P, DomainError>) (execute: 'P -> SearchOptions -> JS.Promise<SearchOutcome>) (finderCache: FinderCache) (iteratorStore: VibeFs.Shell.FuzzyIteratorStore.TypedIteratorStore) : obj =
    define description
        args
        (fun args context ->
            let runtime = fromOpencode context ""
            let scopeId = runtime.Execution.SessionId
            if scopeId = "" then resolveStr (toolRequiresActiveSession toolName)
            else
                match decode args with
                | Error e -> resolveStr (wireDecodeFailure toolName e)
                | Ok p ->
                    let o : SearchOptions =
                        { cwd = runtime.Execution.Directory
                          scopeId = scopeId
                          store = Some iteratorStore
                          finderCache = finderCache }
                    promise {
                        let! r = execute p o
                        return r.output
                    })

let fuzzyFindTool (finderCache: FinderCache) (iteratorStore: VibeFs.Shell.FuzzyIteratorStore.TypedIteratorStore) : obj =
    buildFuzzyTool
        ToolSchemaModule.fuzzyFind
        (box {| pattern = strMinNullish 1 Params.fuzzyFindPattern; path = strOpt Params.fuzzyFindPath
                limit = intMinNullish 1 Params.fuzzyFindLimit; iterator = strOpt Params.fuzzyFindIterator |})
        "fuzzy_find"
        decodeFuzzyFindArgs
        FuzzyCommandsModule.fuzzyFind
        finderCache
        iteratorStore

let fuzzyGrepTool (finderCache: FinderCache) (iteratorStore: VibeFs.Shell.FuzzyIteratorStore.TypedIteratorStore) : obj =
    buildFuzzyTool
        ToolSchemaModule.fuzzyGrep
        (box {| pattern = strMinNullish 1 Params.fuzzyGrepPattern; path = strOpt Params.fuzzyGrepPath
                exclude = excludeOpt Params.fuzzyGrepExclude; caseSensitive = boolOpt Params.fuzzyGrepCaseSensitive
                context = intMinNullish 0 Params.fuzzyGrepContext; limit = intMinNullish 1 Params.fuzzyGrepLimit
                iterator = strOpt Params.fuzzyGrepIterator |})
        "fuzzy_grep"
        decodeFuzzyGrepArgs
        FuzzyCommandsModule.fuzzyGrep
        finderCache
        iteratorStore

let websearchTool (registry: ChildAgentRegistry) (ctx: obj) : obj =
    define websearch
        (box {| query = strReq Params.websearchQuery
                numResults = numOpt Params.websearchNumResults
                what_to_summarize = strReq Params.websearchWhatToSummarize |})
        (fun args context ->
            match decodeWebsearchArgs args with
            | Error e -> resolveStr (wireDecodeFailure "websearch" e)
            | Ok ws ->
                match getClientFromPluginCtx ctx with
                | Error e -> resolveStr (wireEncodeToolError "OpencodeClient" e)
                | Ok client ->
                    let runtime = fromOpencode context (pluginDirectoryFromCtx ctx)
                    promise {
                        let body = createObj [ "query", box ws.Query; "max_results", box ws.NumResults ]
                        let! result = webApiPost "web_search" body runtime.AbortSignal
                        match result with
                        | Error e -> return webToolFailed "search" e
                        | Ok data ->
                            let items = parseSearchResults (Dyn.get data "results")
                            let rawResults = formatSearchResults items
                            if items.IsEmpty then
                                return rawResults
                            else
                                let prompt = formatPrompt opencode (WebsearchSummary(ws.WhatToSummarize, rawResults)) |> List.head
                                return! resolveSubagentPromise "executor"
                                    (runSubagentWithCleanup registry client "executor" "Web search summary" prompt
                                        runtime.Execution.Directory runtime.Execution.SessionId context)
                    })

let webfetchTool (ctx: obj) : obj =
    define webfetch
        (box {| url = strReq Params.webfetchUrl
                extract_main = boolOpt Params.webfetchExtractMain
                prefer_llms_txt = enumOpt [| "auto"; "always"; "never" |] Params.webfetchPreferLlmsTxt
                prompt = strOpt Params.webfetchPrompt
                timeout = numOpt Params.webfetchTimeout |})
        (fun args context ->
            match decodeWebfetchArgs args with
            | Error e -> resolveStr (wireDecodeFailure "webfetch" e)
            | Ok wf ->
                let runtime = fromOpencode context (pluginDirectoryFromCtx ctx)
                promise {
                    let bodyEntries = ResizeArray<(string * obj)>()
                    bodyEntries.Add("url", box wf.Url)
                    addIfSome bodyEntries "extract_main" wf.ExtractMain
                    addIfSome bodyEntries "prefer_llms_txt" wf.PreferLlmsTxt
                    addIfSome bodyEntries "prompt" wf.Prompt
                    addIfSome bodyEntries "timeout" wf.Timeout
                    let body = createObj (Seq.toList bodyEntries)
                    let! result = webApiPost "web_fetch" body runtime.AbortSignal
                    match result with
                    | Error e -> return webToolFailed "fetch" e
                    | Ok data ->
                        let title = if Dyn.isNullish (Dyn.get data "title") then None else Some (Dyn.str data "title")
                        let byline = if Dyn.isNullish (Dyn.get data "byline") then None else Some (Dyn.str data "byline")
                        let length_ = if Dyn.isNullish (Dyn.get data "length") then None else Some (unbox<int> (Dyn.get data "length"))
                        let content = if Dyn.isNullish (Dyn.get data "content") then None else Some (Dyn.str data "content")
                        return formatFetchResponse { title = title; byline = byline; length = length_; content = content }
                })