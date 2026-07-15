module Wanxiangshu.Mux.BuiltinToolsFuzzy

open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Mux.Wrappers
open Wanxiangshu.Shell.FuzzyFinderShell
open Wanxiangshu.Shell.ToolExecute
open Wanxiangshu.Shell.FuzzySearch
open Wanxiangshu.Shell.PromiseStr
open Wanxiangshu.Shell.FuzzyToolsCodec
open Wanxiangshu.Shell.ToolContextCodec
open Wanxiangshu.Shell.ToolRuntimeContext

let private description (name: string) : string =
    match Wanxiangshu.Kernel.ToolCatalog.description name with
    | Ok d -> d
    | Error e -> failwith e

module FuzzyCommandsModule = Wanxiangshu.Shell.FuzzySearch

let private searchOptionsFromRuntime
    (runtime: IToolRuntimeContext)
    (finderCache: FinderCache)
    (iteratorStore: Wanxiangshu.Shell.FuzzyIteratorStore.TypedIteratorStore)
    : SearchOptions =
    let scopeId =
        match runtime.Execution.WorkspaceId with
        | Some w -> Id.workspaceIdValue w
        | None -> ""

    { cwd = runtime.Execution.Directory
      scopeId = scopeId
      store = Some iteratorStore
      finderCache = finderCache }

let fuzzyFindTool
    (finderCache: FinderCache)
    (iteratorStore: Wanxiangshu.Shell.FuzzyIteratorStore.TypedIteratorStore)
    : ToolDefinition =
    { name = "fuzzy_find"
      description = description "fuzzy_find"
      parameters =
        mkSchema
            (createObj
                [ "pattern", box (strArrayProp Params.fuzzyFindPattern)
                  "path", box (strProp Params.fuzzyFindPath)
                  "limit", box (numProp Params.fuzzyFindLimit) ])
            [| "pattern" |]
      execute =
        fun config args ->
            match fromMuxConfig config with
            | Error e -> resolveStr (wireEncodeToolError "MuxConfig" e)
            | Ok runtime ->
                match decodeFuzzyFindArgs args with
                | Error e -> resolveStr (wireDecodeFailure "fuzzy_find" e)
                | Ok p ->
                    let o = searchOptionsFromRuntime runtime finderCache iteratorStore

                    promise {
                        let! r = FuzzyCommandsModule.fuzzyFind p o
                        return r.output
                    }
      condition = None }

let fuzzyGrepTool
    (finderCache: FinderCache)
    (iteratorStore: Wanxiangshu.Shell.FuzzyIteratorStore.TypedIteratorStore)
    : ToolDefinition =
    { name = "fuzzy_grep"
      description = description "fuzzy_grep"
      parameters =
        mkSchema
            (createObj
                [ "pattern", box (strArrayProp Params.fuzzyGrepPattern)
                  "path", box (strProp Params.fuzzyGrepPath)
                  "exclude", box (strProp Params.fuzzyGrepExclude)
                  "searchIgnored", box (boolProp Params.fuzzyGrepSearchIgnored)
                  "caseSensitive", box (boolProp Params.fuzzyGrepCaseSensitive)
                  "context", box (numProp Params.fuzzyGrepContext)
                  "limit", box (numProp Params.fuzzyGrepLimit) ])
            [| "pattern" |]
      execute =
        fun config args ->
            match fromMuxConfig config with
            | Error e -> resolveStr (wireEncodeToolError "MuxConfig" e)
            | Ok runtime ->
                match decodeFuzzyGrepArgs args with
                | Error e -> resolveStr (wireDecodeFailure "fuzzy_grep" e)
                | Ok p ->
                    let o = searchOptionsFromRuntime runtime finderCache iteratorStore

                    promise {
                        let! r = FuzzyCommandsModule.fuzzyGrep p o
                        return r.output
                    }
      condition = None }

let fuzzyContinueTool
    (finderCache: FinderCache)
    (iteratorStore: Wanxiangshu.Shell.FuzzyIteratorStore.TypedIteratorStore)
    : ToolDefinition =
    { name = "fuzzy_continue"
      description = description "fuzzy_continue"
      parameters = mkSchema (createObj [ "iterator", box (strProp Params.fuzzyContinueIterator) ]) [| "iterator" |]
      execute =
        fun config args ->
            match fromMuxConfig config with
            | Error e -> resolveStr (wireEncodeToolError "MuxConfig" e)
            | Ok runtime ->
                match decodeFuzzyContinueArgs args with
                | Error e -> resolveStr (wireDecodeFailure "fuzzy_continue" e)
                | Ok p ->
                    let o = searchOptionsFromRuntime runtime finderCache iteratorStore

                    promise {
                        let! r = FuzzyCommandsModule.fuzzyContinue p o
                        return r.output
                    }
      condition = None }
