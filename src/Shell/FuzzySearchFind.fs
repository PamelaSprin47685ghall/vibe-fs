module Wanxiangshu.Shell.FuzzySearchFind

open Fable.Core
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Kernel.FuzzyPath
open Wanxiangshu.Kernel.FuzzyQuery
open Wanxiangshu.Kernel.FuzzyFormat
open Wanxiangshu.Shell.FuzzyIteratorStore
open Wanxiangshu.Shell.FuzzyFinderShell
open Wanxiangshu.Shell.FuzzySearchHelpers

let resolveFindSearchState (params': FuzzyFindParams) (opts: SearchOptions)
    : Result<FuzzyFindState, string> =
    match resolveStore opts with
    | Error msg -> Error msg
    | Ok store ->
    resolveIteratorBranch store params'.iterator consumeFindIterator "fuzzy_find" (fun () ->
        match params'.pattern with
        | None | Some "" -> Error "pattern is required on the first call"
        | Some pattern ->
            let searchPath = resolveFuzzySearchPath params'.path opts.cwd
            let externalBasePath = if searchPath.external then Some searchPath.basePath else None
            Ok { query = buildQuery searchPath.pathConstraint pattern [] searchPath.basePath searchPath.external
                 pageSize = defaultArg params'.limit 30
                 pageIndex = 0
                 externalBasePath = externalBasePath })

let findNextIterator (state: FuzzyFindState) (store: TypedIteratorStore) (opts: SearchOptions) (totalForPaging: int) : string =
    let nextPageIndex = state.pageIndex + 1
    if totalForPaging > nextPageIndex * state.pageSize then
        let nextState : FuzzyFindState = { state with pageIndex = nextPageIndex }
        storeFindIterator store opts.scopeId nextState
    else ""

let private runFind (state: FuzzyFindState) (store: TypedIteratorStore) (opts: SearchOptions) (finder: FinderLike) : SearchOutcome =
    let raw = finder.fileSearch(state.query, box {| pageIndex = state.pageIndex; pageSize = state.pageSize |})
    if not (Dyn.truthy (Dyn.get raw "ok")) then { output = errorMsg raw "fuzzy_find failed"; isError = true }
    else
        let value = Dyn.get raw "value"
        let matches = itemsOf value |> Array.map toFindMatch |> List.ofArray
        let totalOpt = optInt value "totalMatched"
        let totalFiles = optInt value "totalFiles" |> Option.defaultValue 0
        let totalForPaging =
            match totalOpt with
            | Some total -> total
            | None -> if matches.Length >= state.pageSize then (state.pageIndex + 2) * state.pageSize else 0
        let body = formatFindOutput (Some { items = matches; totalMatched = totalOpt; totalFiles = totalFiles })
        let nextIterator = findNextIterator state store opts totalForPaging
        let output =
            if nextIterator = "" then body
            else Wanxiangshu.Kernel.ToolOutputInfo.withIterator body nextIterator
        { output = output; isError = false }

let fuzzyFind (params': FuzzyFindParams) (opts: SearchOptions) : JS.Promise<SearchOutcome> =
    promise {
        match resolveStore opts with
        | Error msg -> return { output = msg; isError = true }
        | Ok store ->
            match resolveFindSearchState params' opts with
            | Error msg -> return { output = msg; isError = true }
            | Ok state ->
                let! finderResult = acquireFinderFromOptions state.externalBasePath opts
                return runWithFinder finderResult state.externalBasePath (runFind state store opts)
    }
