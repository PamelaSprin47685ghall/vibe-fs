module Wanxiangshu.Runtime.FuzzySearchFindHelper

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Kernel.FuzzyPath
open Wanxiangshu.Kernel.FuzzyQuery
open Wanxiangshu.Kernel.FuzzyFormat
open Wanxiangshu.Runtime.FuzzyIteratorStore
open Wanxiangshu.Runtime.FuzzyFinderShell
open Wanxiangshu.Runtime.FuzzySearchSupport

let resolveFindSearchStateForPattern
    (pattern: string)
    (params': FuzzyFindParams)
    (opts: SearchOptions)
    : Result<FuzzyFindState, string> =
    if pattern.Trim() = "" then
        Error "pattern is required on the first call"
    else
        let searchPath = resolveFuzzySearchPath params'.path opts.cwd

        let externalBasePath =
            if searchPath.external then
                Some searchPath.basePath
            else
                None

        Ok
            { query = buildQuery searchPath.pathConstraint pattern [] searchPath.basePath searchPath.external
              pageSize = defaultArg params'.limit 30
              pageIndex = 0
              externalBasePath = externalBasePath }

let resolveFindSearchState (params': FuzzyFindParams) (opts: SearchOptions) : Result<FuzzyFindState, string> =
    match params'.pattern with
    | [] -> Error "pattern is required on the first call"
    | first :: _ -> resolveFindSearchStateForPattern first params' opts

let findNextIterator
    (state: FuzzyFindState)
    (store: TypedIteratorStore)
    (opts: SearchOptions)
    (totalForPaging: int)
    : string =
    let nextPageIndex = state.pageIndex + 1

    if totalForPaging > nextPageIndex * state.pageSize then
        let nextState: FuzzyFindState = { state with pageIndex = nextPageIndex }
        storeFindIterator store opts.scopeId nextState
    else
        ""

let processRawFindResponse (value: obj) : string * FindResult =
    let matches = itemsOf value |> Array.map toFindMatch |> List.ofArray
    let totalOpt = optInt value "totalMatched"
    let totalFiles = optInt value "totalFiles" |> Option.defaultValue 0

    let result =
        Some
            { items = matches
              totalMatched = totalOpt
              totalFiles = totalFiles }

    let output = formatFindOutput result

    output,
    { items = matches
      totalMatched = totalOpt
      totalFiles = totalFiles }
