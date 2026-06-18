module VibeFs.MuxPlugin.MuxTools.SearchTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Mux.Contract
open VibeFs.MuxPlugin.MuxTools.Shared
open VibeFs.Opencode.Core
open VibeFs.Shell.FuzzySearch

let fuzzyFindTool (finderCache: FinderCache) : ToolDefinition =
    { name = "fuzzy-find"
      description = VibeFs.Opencode.Core.fuzzyFind
      parameters = mkSchema (createObj [ "pattern", box (strProp Params.fuzzyFindPattern); "path", box (strProp Params.fuzzyFindPath); "limit", box (numProp Params.fuzzyFindLimit); "iterator", box (strProp Params.fuzzyFindIterator) ]) [||]
      execute = fun config args ->
          let scopeId = Dyn.str config "workspaceId"
          if scopeId = "" then resolveStr "fuzzy-find requires workspaceId"
          else
              let p : FuzzyFindParams = { pattern = strField args "pattern"; path = strField args "path"; limit = optInt args "limit"; iterator = strField args "iterator" }
              let o : SearchOptions = { cwd = Dyn.str config "cwd"; scopeId = scopeId; store = None; finderCache = finderCache }
              async {
                  let! r = VibeFs.Shell.FuzzySearch.fuzzyFind p o |> Async.AwaitPromise
                  return r.output
              } |> Async.StartAsPromise
      condition = None }

let fuzzyGrepTool (finderCache: FinderCache) : ToolDefinition =
    { name = "fuzzy-grep"
      description = VibeFs.Opencode.Core.fuzzyGrep
      parameters = mkSchema (createObj [ "pattern", box (strProp Params.fuzzyGrepPattern); "path", box (strProp Params.fuzzyGrepPath); "exclude", box (strProp Params.fuzzyGrepExclude); "caseSensitive", box (boolProp Params.fuzzyGrepCaseSensitive); "context", box (numProp Params.fuzzyGrepContext); "limit", box (numProp Params.fuzzyGrepLimit); "iterator", box (strProp Params.fuzzyGrepIterator) ]) [||]
      execute = fun config args ->
          let scopeId = Dyn.str config "workspaceId"
          if scopeId = "" then resolveStr "fuzzy-grep requires workspaceId"
          else
              let p : FuzzyGrepParams = { pattern = strField args "pattern"; path = strField args "path"; exclude = parseExcludeField args; caseSensitive = optBool args "caseSensitive"; context = optInt args "context"; limit = optInt args "limit"; iterator = strField args "iterator" }
              let o : SearchOptions = { cwd = Dyn.str config "cwd"; scopeId = scopeId; store = None; finderCache = finderCache }
              async {
                  let! r = VibeFs.Shell.FuzzySearch.fuzzyGrep p o |> Async.AwaitPromise
                  return r.output
              } |> Async.StartAsPromise
      condition = None }
