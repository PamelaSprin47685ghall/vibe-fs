module Wanxiangshu.Hosts.Opencode.SearchToolsHelper

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime

open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.FuzzyPath
open Wanxiangshu.Kernel.FuzzyQuery
open Wanxiangshu.Kernel.FuzzyFormat
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.SearchPrompts
open Wanxiangshu.Runtime.WebSearchCodec
open Wanxiangshu.Runtime.WebToolsCodec
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Runtime.Subagent
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Kernel.ToolCopy
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Runtime.WebSearchApi
open Wanxiangshu.Hosts.Opencode.ToolSchema
open Wanxiangshu.Hosts.Opencode.SessionIo
open Wanxiangshu.Hosts.Opencode.SubagentIoRun
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.SubagentDispatcher
open Wanxiangshu.Runtime.SubagentBatchSpawn
open Wanxiangshu.Runtime.FuzzyFinderShell
open Wanxiangshu.Runtime.FuzzySearch
open Wanxiangshu.Runtime.FuzzyToolsCodec
open Wanxiangshu.Runtime.ToolExecute
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.Fallback.RuntimeStore

let addIfSome (entries: ResizeArray<string * obj>) (key: string) (v: 'T option) =
    match v with
    | Some x -> entries.Add(key, box x)
    | None -> ()

let buildFuzzyTool
    (description: string)
    (args: obj)
    (toolName: string)
    (decode: obj -> Result<'P, DomainError>)
    (execute: 'P -> SearchOptions -> JS.Promise<SearchOutcome>)
    (finderCache: FinderCache)
    (iteratorStore: Wanxiangshu.Runtime.FuzzyIteratorStore.TypedIteratorStore)
    : obj =
    define description args (fun args context ->
        let runtime = fromOpencode context ""
        let scopeId = Id.sessionIdValue runtime.Execution.SessionId

        if scopeId = "" then
            Promise.lift (toolRequiresActiveSession toolName)
        else
            match decode args with
            | Error e -> Promise.lift (wireDecodeFailure toolName e)
            | Ok p ->
                let o: SearchOptions =
                    { cwd = runtime.Execution.Directory
                      scopeId = scopeId
                      store = Some iteratorStore
                      finderCache = finderCache }

                promise {
                    let! r = execute p o
                    return r.output
                })

let parseFetchResponse (data: obj) : FetchResponse =
    let title =
        if Dyn.isNullish (Dyn.get data "title") then None
        else Some(Dyn.str data "title")
    let byline =
        if Dyn.isNullish (Dyn.get data "byline") then None
        else Some(Dyn.str data "byline")
    let length_ =
        if Dyn.isNullish (Dyn.get data "length") then None
        else Some(unbox<int> (Dyn.get data "length"))
    let content =
        if Dyn.isNullish (Dyn.get data "content") then None
        else Some(Dyn.str data "content")
    { title = title
      byline = byline
      length = length_
      content = content }

let executeWebfetch (ctx: obj) (args: obj) (context: obj) : JS.Promise<string> =
    match decodeWebfetchArgs args with
    | Error e -> Promise.lift (wireDecodeFailure "webfetch" e)
    | Ok wf ->
        let runtime = fromOpencode context (pluginDirectoryFromCtx ctx)

        promise {
            match WebFetchGuard.validateFetchUrl wf.Url with
            | Error msg -> return webToolFailed "fetch" (UpstreamRefused msg)
            | Ok() ->
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
                    let resp = parseFetchResponse data
                    return formatFetchResponse resp
        }

let executeWebsearch
    (host: Host)
    (registry: ChildAgentRegistry)
    (ctx: obj)
    (fallbackRuntime: FallbackRuntimeStore)
    (args: obj)
    (context: obj)
    : JS.Promise<string> =
    match decodeWebsearchArgs args with
    | Error e -> Promise.lift (wireDecodeFailure "websearch" e)
    | Ok ws ->
        match getClientFromPluginCtx ctx with
        | Error e -> Promise.lift (wireEncodeToolError "OpencodeClient" e)
        | Ok client ->
            let runtime = fromOpencode context (pluginDirectoryFromCtx ctx)

            promise {
                let body = createObj [ "query", box ws.Query; "max_results", box ws.NumResults ]
                let! result = webApiPost "web_search" body runtime.AbortSignal

                match result with
                | Error e -> return webToolFailed "search" e
                | Ok data ->
                    let items = parseSearchResults (Dyn.get data "results")

                    if items.IsEmpty then
                        return formatSearchResults items
                    else
                        let structured: Wanxiangshu.Kernel.Prompt.WebSearchResultItem list =
                            items
                            |> List.map (fun r ->
                                { title = r.title
                                  url = r.url
                                  content = r.content })

                        let prompt =
                            formatPrompt host (WebsearchSummary(ws.WhatToSummarize, structured))
                            |> List.head

                        return!
                            resolveSubagentPromise
                                "executor"
                                (runSubagentWithCleanup
                                    fallbackRuntime
                                    registry
                                    client
                                    "executor"
                                    "Web search summary"
                                    prompt
                                    runtime.Execution.Directory
                                    (Id.sessionIdValue runtime.Execution.SessionId)
                                    context)
            }
