module VibeFs.Mux.BuiltinToolsFuzzy

open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Domain
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.ToolCatalog
open VibeFs.Kernel.ToolResult
open VibeFs.Mux.Wrappers
open VibeFs.Shell.FuzzyFinderShell
open VibeFs.Shell.ToolExecute
open VibeFs.Shell.FuzzySearch
open VibeFs.Shell.PromiseStr
open VibeFs.Shell.FuzzyToolsCodec
open VibeFs.Shell.ToolContextCodec
open VibeFs.Shell.ToolRuntimeContext

module FuzzyCommandsModule = VibeFs.Shell.FuzzySearch

let private searchOptionsFromRuntime (runtime: IToolRuntimeContext) (finderCache: FinderCache) (iteratorStore: VibeFs.Shell.FuzzyIteratorStore.TypedIteratorStore) : SearchOptions =
    let scopeId =
        match runtime.Execution.WorkspaceId with
        | Some w -> Id.workspaceIdValue w
        | None -> ""
    { cwd = runtime.Execution.Directory
      scopeId = scopeId
      store = Some iteratorStore
      finderCache = finderCache }

let fuzzyFindTool (finderCache: FinderCache) (iteratorStore: VibeFs.Shell.FuzzyIteratorStore.TypedIteratorStore) : ToolDefinition =
    { name = "fuzzy_find"
      description = description "fuzzy_find"
      parameters = mkSchema (createObj [ "pattern", box (strProp Params.fuzzyFindPattern); "path", box (strProp Params.fuzzyFindPath); "limit", box (numProp Params.fuzzyFindLimit); "iterator", box (strProp Params.fuzzyFindIterator) ]) [||]
      execute = fun config args ->
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

let fuzzyGrepTool (finderCache: FinderCache) (iteratorStore: VibeFs.Shell.FuzzyIteratorStore.TypedIteratorStore) : ToolDefinition =
    { name = "fuzzy_grep"
      description = description "fuzzy_grep"
      parameters = mkSchema (createObj [ "pattern", box (strProp Params.fuzzyGrepPattern); "path", box (strProp Params.fuzzyGrepPath); "exclude", box (strProp Params.fuzzyGrepExclude); "caseSensitive", box (boolProp Params.fuzzyGrepCaseSensitive); "context", box (numProp Params.fuzzyGrepContext); "limit", box (numProp Params.fuzzyGrepLimit); "iterator", box (strProp Params.fuzzyGrepIterator) ]) [||]
      execute = fun config args ->
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