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
open Wanxiangshu.Kernel.FuzzyQuery

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
              "description", box fuzzyFindDescriptionOmp
              "parameters",
              objectOf
                  [| ("pattern",
                      strArray
                          """Plain fuzzy file path text to search for. Pass a real JSON array of strings for parallel search; never pass a stringified JSON string. Correct: ["src","build"]. Wrong: "[\"src\",\"build\"]" (a string, not an array)."""
                          tb)
                     ("path", opt "Initial optional path constraint to narrow search scope" tb str)
                     ("limit", opt "Maximum number of results to return per call (default: 30)" tb num) |]
                  tb
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
              "description", box fuzzyGrepDescriptionOmp
              "parameters",
              objectOf
                  [| ("pattern",
                      strArray
                          """Search pattern. Pass a real JSON array of strings for parallel search; never pass a stringified JSON string. Required on the first call. Correct: ["StateMachine","EventLog"]. Wrong: "[\"StateMachine","EventLog"]" (a string, not an array)."""
                          tb)
                     ("path", opt "Initial path constraint." tb str)
                     ("exclude",
                      optional
                          (union
                              [| str "Initial exclude paths (e.g. 'test/,*.min.js')" tb
                                 strArray "Initial exclude path or glob" tb |]
                              tb)
                          tb)
                     ("caseSensitive", opt "Case-sensitivity override (smart-case by default)." tb bool_)
                     ("searchIgnored",
                      opt
                          "Search git-ignored files such as node_modules by adding the fff git:ignored constraint."
                          tb
                          bool_)
                     ("context", opt "Number of context lines before and after each match" tb num)
                     ("limit", opt "Maximum number of matches to return per call." tb num) |]
                  tb
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
              "description",
              box "Continue a previously running fuzzy_find or fuzzy_grep session. Returns the next page of results."
              "parameters",
              objectOf [| ("iterator", str "Opaque single-use iterator from a previous search result." tb) |] tb
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
