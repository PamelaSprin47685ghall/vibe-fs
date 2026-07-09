module Wanxiangshu.Tests.OpencodeSearchToolsCoverageTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Opencode.SearchTools
open Wanxiangshu.Shell.FuzzyIteratorStore
open Wanxiangshu.Shell.FuzzyFinderShell
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Shell.WebToolsCodec

let private specOf (name: string) =
    match Wanxiangshu.Kernel.ToolCatalog.specOf name with
    | Ok s -> s
    | Error e -> failwith e

let searchToolsFuzzyFindTool () =
    let finderCache = FinderCache()
    let iteratorStore = createTypedIteratorStore 200
    let tool = fuzzyFindTool finderCache iteratorStore
    check "fuzzyFind tool non-null" (not (isNull tool))

let searchToolsFuzzyGrepTool () =
    let finderCache = FinderCache()
    let iteratorStore = createTypedIteratorStore 200
    let tool = fuzzyGrepTool finderCache iteratorStore
    check "fuzzyGrep tool non-null" (not (isNull tool))

let searchToolsWebsearchTool () =
    let registry = ChildAgentRegistry.Create()
    let ctx = createObj []
    let tool = websearchTool Opencode registry ctx (FallbackRuntimeState())
    check "websearch tool non-null" (not (isNull tool))

let searchToolsWebfetchTool () =
    let ctx = createObj []
    let tool = webfetchTool ctx
    check "webfetch tool non-null" (not (isNull tool))

let searchToolsFuzzyFindToolName () =
    let finderCache = FinderCache()
    let iteratorStore = createTypedIteratorStore 200
    let tool = fuzzyFindTool finderCache iteratorStore
    let spec = specOf "fuzzy_find"
    equal "fuzzyFind tool name" spec.name "fuzzy_find"

let searchToolsFuzzyGrepToolName () =
    let finderCache = FinderCache()
    let iteratorStore = createTypedIteratorStore 200
    let tool = fuzzyGrepTool finderCache iteratorStore
    let spec = specOf "fuzzy_grep"
    equal "fuzzyGrep tool name" spec.name "fuzzy_grep"

let searchToolsWebfetchToolName () =
    let ctx = createObj []
    let tool = webfetchTool ctx
    let spec = specOf "webfetch"
    equal "webfetch tool name" spec.name "webfetch"

let searchToolsWebfetchToolFullOptionsDecode () =
    let args =
        createObj
            [ "url", box "https://example.com"
              "extract_main", box true
              "prefer_llms_txt", box "auto"
              "prompt", box "summarize"
              "timeout", box 30 ]

    match decodeWebfetchArgs args with
    | Error e -> check "webfetch decode should succeed" false
    | Ok wf ->
        check "url decoded" (wf.Url = "https://example.com")
        check "extract_main decoded" (wf.ExtractMain = Some true)
        check "prefer_llms_txt decoded" (wf.PreferLlmsTxt = Some "auto")
        check "prompt decoded" (wf.Prompt = Some "summarize")
        check "timeout decoded" (wf.Timeout = Some 30)

let run () =
    promise {
        searchToolsFuzzyFindTool ()
        searchToolsFuzzyGrepTool ()
        searchToolsWebsearchTool ()
        searchToolsWebfetchTool ()
        searchToolsFuzzyFindToolName ()
        searchToolsFuzzyGrepToolName ()
        searchToolsWebfetchToolName ()
        searchToolsWebfetchToolFullOptionsDecode ()
    }
