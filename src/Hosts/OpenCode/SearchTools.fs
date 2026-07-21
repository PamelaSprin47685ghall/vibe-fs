module Wanxiangshu.Hosts.Opencode.SearchTools

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime

open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.SearchPrompts
open Wanxiangshu.Runtime.WebSearchCodec
open Wanxiangshu.Runtime.WebToolsCodec
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Runtime.Subagent
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Hosts.Opencode.ToolSchema
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.FuzzyFinderShell
open Wanxiangshu.Runtime.FuzzySearch
open Wanxiangshu.Runtime.FuzzyToolsCodec
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Hosts.Opencode.SearchToolsHelper

let fuzzyFindTool
    (finderCache: FinderCache)
    (iteratorStore: Wanxiangshu.Runtime.FuzzyIteratorStore.TypedIteratorStore)
    : obj =
    buildFuzzyTool
        ToolSchema.fuzzyFind
        (box
            {| pattern = strArrayReq Params.fuzzyFindPattern
               path = strOpt Params.fuzzyFindPath
               limit = intMinNullish 1 Params.fuzzyFindLimit |})
        "fuzzy_find"
        decodeFuzzyFindArgs
        locateFuzzyMatches
        finderCache
        iteratorStore

let fuzzyGrepTool
    (finderCache: FinderCache)
    (iteratorStore: Wanxiangshu.Runtime.FuzzyIteratorStore.TypedIteratorStore)
    : obj =
    buildFuzzyTool
        ToolSchema.fuzzyGrep
        (box
            {| pattern = strArrayReq Params.fuzzyGrepPattern
               path = strOpt Params.fuzzyGrepPath
               exclude = excludeOpt Params.fuzzyGrepExclude
               searchIgnored = boolOptional Params.fuzzyGrepSearchIgnored
               caseSensitive = boolOptional Params.fuzzyGrepCaseSensitive
               context = intMinNullish 0 Params.fuzzyGrepContext
               limit = intMinNullish 1 Params.fuzzyGrepLimit |})
        "fuzzy_grep"
        decodeFuzzyGrepArgs
        searchFuzzyContent
        finderCache
        iteratorStore

let fuzzyContinueTool
    (finderCache: FinderCache)
    (iteratorStore: Wanxiangshu.Runtime.FuzzyIteratorStore.TypedIteratorStore)
    : obj =
    buildFuzzyTool
        ToolSchema.fuzzyContinue
        (box {| iterator = strReq Params.fuzzyContinueIterator |})
        "fuzzy_continue"
        decodeFuzzyContinueArgs
        paginateFuzzySearch
        finderCache
        iteratorStore

let websearchTool (host: Host) (registry: ChildAgentRegistry) (ctx: obj) (fallbackRuntime: FallbackRuntimeStore) : obj =
    define
        websearch
        (box
            {| query = strReq Params.websearchQuery
               numResults = numOpt Params.websearchNumResults
               what_to_summarize = strReq Params.websearchWhatToSummarize |})
        (fun args context -> executeWebsearch host registry ctx fallbackRuntime args context)

let webfetchTool (ctx: obj) : obj =
    define
        webfetch
        (box
            {| url = strReq Params.webfetchUrl
               extract_main = boolOptional Params.webfetchExtractMain
               prefer_llms_txt = enumOpt [| "auto"; "always"; "never" |] Params.webfetchPreferLlmsTxt
               prompt = strOpt Params.webfetchPrompt
               timeout = numOpt Params.webfetchTimeout |})
        (fun args context -> executeWebfetch ctx args context)
