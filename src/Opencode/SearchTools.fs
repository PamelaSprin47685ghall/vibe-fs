module Wanxiangshu.Opencode.SearchTools

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Shell

open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.FuzzyPath
open Wanxiangshu.Kernel.FuzzyQuery
open Wanxiangshu.Kernel.FuzzyFormat
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.SearchPrompts
open Wanxiangshu.Shell.WebSearchCodec
open Wanxiangshu.Shell.WebToolsCodec
open Wanxiangshu.Shell.ToolRuntimeContext
open Wanxiangshu.Kernel.Subagent
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Kernel.ToolCopy
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Shell.WebSearchApi
open Wanxiangshu.Opencode.ToolSchema
open Wanxiangshu.Opencode.SessionIo
open Wanxiangshu.Opencode.ToolHelpers
open Wanxiangshu.Shell.PromiseStr
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.SubagentToolExecute
open Wanxiangshu.Shell.FuzzyFinderShell
open Wanxiangshu.Shell.FuzzySearch
open Wanxiangshu.Shell.FuzzyToolsCodec
open Wanxiangshu.Shell.ToolExecute
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OpencodeClientCodec
module ToolSchemaModule = Wanxiangshu.Opencode.ToolSchema
module FuzzyCommandsModule = Wanxiangshu.Shell.FuzzySearch

let private addIfSome (entries: ResizeArray<string * obj>) (key: string) (v: 'T option) =
    match v with
    | Some x -> entries.Add(key, box x)
    | None -> ()

let private buildFuzzyTool (description: string) (args: obj) (toolName: string) (decode: obj -> Result<'P, DomainError>) (execute: 'P -> SearchOptions -> JS.Promise<SearchOutcome>) (finderCache: FinderCache) (iteratorStore: Wanxiangshu.Shell.FuzzyIteratorStore.TypedIteratorStore) : obj =
    define description
        args
        (fun args context ->
            let runtime = fromOpencode context ""
            let scopeId = Id.sessionIdValue runtime.Execution.SessionId
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

let fuzzyFindTool (finderCache: FinderCache) (iteratorStore: Wanxiangshu.Shell.FuzzyIteratorStore.TypedIteratorStore) : obj =
    buildFuzzyTool
        ToolSchemaModule.fuzzyFind
        (box {| pattern = strMinNullish 1 Params.fuzzyFindPattern; path = strOpt Params.fuzzyFindPath
                limit = intMinNullish 1 Params.fuzzyFindLimit; iterator = strOpt Params.fuzzyFindIterator |})
        "fuzzy_find"
        decodeFuzzyFindArgs
        FuzzyCommandsModule.fuzzyFind
        finderCache
        iteratorStore

let fuzzyGrepTool (finderCache: FinderCache) (iteratorStore: Wanxiangshu.Shell.FuzzyIteratorStore.TypedIteratorStore) : obj =
    buildFuzzyTool
        ToolSchemaModule.fuzzyGrep
        (box {| pattern = strMinNullish 1 Params.fuzzyGrepPattern; path = strOpt Params.fuzzyGrepPath
                exclude = excludeOpt Params.fuzzyGrepExclude; caseSensitive = boolOptional Params.fuzzyGrepCaseSensitive
                context = intMinNullish 0 Params.fuzzyGrepContext; limit = intMinNullish 1 Params.fuzzyGrepLimit
                iterator = strOpt Params.fuzzyGrepIterator |})
        "fuzzy_grep"
        decodeFuzzyGrepArgs
        FuzzyCommandsModule.fuzzyGrep
        finderCache
        iteratorStore

let websearchTool (host: Host) (registry: ChildAgentRegistry) (ctx: obj) : obj =
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
                                let prompt = formatPrompt host (WebsearchSummary(ws.WhatToSummarize, rawResults)) |> List.head
                                return! resolveSubagentPromise "executor"
                                    (runSubagentWithCleanup registry client "executor" "Web search summary" prompt
                                        runtime.Execution.Directory (Id.sessionIdValue runtime.Execution.SessionId) context)
                    })

let webfetchTool (ctx: obj) : obj =
    define webfetch
        (box {| url = strReq Params.webfetchUrl
                extract_main = boolOptional Params.webfetchExtractMain
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