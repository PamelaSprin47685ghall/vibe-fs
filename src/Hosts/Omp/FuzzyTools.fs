module Wanxiangshu.Hosts.Omp.FuzzyTools

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Hosts.Omp.Codec
open Wanxiangshu.Hosts.Omp.Schema
open Wanxiangshu.Kernel.Errors.DomainError

module Dyn = Wanxiangshu.Runtime.Dyn

open Wanxiangshu.Runtime.FuzzyFinderShell
open Wanxiangshu.Runtime.FuzzySearch
open Wanxiangshu.Runtime.FuzzyIteratorStore
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Kernel.ToolCatalog.Params
open Wanxiangshu.Kernel.FuzzyQuery
open Wanxiangshu.Hosts.Omp.FuzzyToolParameters

let private scopeId (ctx: obj) =
    let sid = Dyn.str ctx "sessionId"
    if sid <> "" then sid else Dyn.str ctx "workspaceId"

let buildFuzzyQuery (ctx: obj) : Result<string * string, DomainError> =
    let scope = scopeId ctx

    if scope = "" then
        Error(InvalidIntent("fuzzy", "session", "fuzzy operation requires an active session"))
    else
        Ok(scope, Dyn.str ctx "cwd")

let private searchOpts
    (scope: string)
    (cwd: string)
    (finderCache: FinderCache)
    (iteratorStore: TypedIteratorStore)
    : SearchOptions =
    { cwd = cwd
      scopeId = scope
      store = Some iteratorStore
      finderCache = finderCache }

let private formatFuzzyResults (r: SearchOutcome) : ToolResult =
    if r.isError then
        errorResult r.output
    else
        textResult r.output

let executeFuzzySearch
    (decodeFn: obj -> Result<'a, DomainError>)
    (searchFn: 'a -> SearchOptions -> JS.Promise<SearchOutcome>)
    (finderCache: FinderCache)
    (iteratorStore: TypedIteratorStore)
    (params': obj)
    (ctx: obj)
    : JS.Promise<ToolResult> =
    promise {
        match buildFuzzyQuery ctx with
        | Error e -> return errorResult (formatDomainError e)
        | Ok(scope, cwd) ->
            match decodeFn params' with
            | Error e -> return errorResult (formatDomainError e)
            | Ok p ->
                let! r = searchFn p (searchOpts scope cwd finderCache iteratorStore)
                return formatFuzzyResults r
    }

let private registerFuzzyFind (pi: obj) (tb: obj) (finderCache: FinderCache) (iteratorStore: TypedIteratorStore) =
    pi?registerTool (
        createObj
            [ "name", box "fuzzy_find"
              "label", box "Fuzzy Find"
              "description", box (description "fuzzy_find" |> Result.defaultValue "")
              "parameters", fuzzyFindParameters tb
              "execute",
              box (fun (_id: string) (params': obj) (_signal: obj) (_onUpdate: obj) (ctx: obj) ->
                  executeFuzzySearch
                      Wanxiangshu.Runtime.FuzzyToolsCodec.decodeFuzzyFindArgs
                      locateFuzzyMatches
                      finderCache
                      iteratorStore
                      params'
                      ctx) ]
    )

let private registerFuzzyGrep (pi: obj) (tb: obj) (finderCache: FinderCache) (iteratorStore: TypedIteratorStore) =
    pi?registerTool (
        createObj
            [ "name", box "fuzzy_grep"
              "label", box "Fuzzy Grep"
              "description", box (description "fuzzy_grep" |> Result.defaultValue "")
              "parameters", fuzzyGrepParameters tb
              "execute",
              box (fun (_id: string) (params': obj) (_signal: obj) (_onUpdate: obj) (ctx: obj) ->
                  executeFuzzySearch
                      Wanxiangshu.Runtime.FuzzyToolsCodec.decodeFuzzyGrepArgs
                      searchFuzzyContent
                      finderCache
                      iteratorStore
                      params'
                      ctx) ]
    )

let private registerFuzzyContinue (pi: obj) (tb: obj) (finderCache: FinderCache) (iteratorStore: TypedIteratorStore) =
    pi?registerTool (
        createObj
            [ "name", box "fuzzy_continue"
              "label", box "Fuzzy Continue"
              "description", box (description "fuzzy_continue" |> Result.defaultValue "")
              "parameters",
              objectOf [| ("iterator", str fuzzyContinueIterator tb) |] tb
              "execute",
              box (fun (_id: string) (params': obj) (_signal: obj) (_onUpdate: obj) (ctx: obj) ->
                  executeFuzzySearch
                      Wanxiangshu.Runtime.FuzzyToolsCodec.decodeFuzzyContinueArgs
                      paginateFuzzySearch
                      finderCache
                      iteratorStore
                      params'
                      ctx) ]
    )

let registerFuzzyTools (pi: obj) (finderCache: FinderCache) (iteratorStore: TypedIteratorStore) : unit =
    let tb = Dyn.get pi "typebox"
    registerFuzzyFind pi tb finderCache iteratorStore
    registerFuzzyGrep pi tb finderCache iteratorStore
    registerFuzzyContinue pi tb finderCache iteratorStore
