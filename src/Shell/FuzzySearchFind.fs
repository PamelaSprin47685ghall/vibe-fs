module Wanxiangshu.Shell.FuzzySearchFind

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Kernel.FuzzyPath
open Wanxiangshu.Kernel.FuzzyQuery
open Wanxiangshu.Kernel.FuzzyFormat
open Wanxiangshu.Shell.FuzzyIteratorStore
open Wanxiangshu.Shell.FuzzyFinderShell
open Wanxiangshu.Shell.FuzzySearchHelpers

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

let private runFind
    (state: FuzzyFindState)
    (store: TypedIteratorStore)
    (opts: SearchOptions)
    (finder: FinderLike)
    : SearchOutcome =
    let raw =
        finder.fileSearch (
            state.query,
            box
                {| pageIndex = state.pageIndex
                   pageSize = state.pageSize |}
        )

    if not (Dyn.truthy (Dyn.get raw "ok")) then
        { output = errorMsg raw "fuzzy_find failed"
          isError = true }
    else
        let value = Dyn.get raw "value"
        let matches = itemsOf value |> Array.map toFindMatch |> List.ofArray
        let totalOpt = optInt value "totalMatched"
        let totalFiles = optInt value "totalFiles" |> Option.defaultValue 0

        let totalForPaging =
            match totalOpt with
            | Some total -> total
            | None ->
                if matches.Length >= state.pageSize then
                    (state.pageIndex + 2) * state.pageSize
                else
                    0

        let body =
            formatFindOutput (
                Some
                    { items = matches
                      totalMatched = totalOpt
                      totalFiles = totalFiles }
            )

        let nextIterator = findNextIterator state store opts totalForPaging

        let output =
            if nextIterator = "" then
                body
            else
                Wanxiangshu.Kernel.ToolOutputInfo.withIterator body nextIterator

        { output = output; isError = false }

let private fuzzyFindSingle (params': FuzzyFindParams) (opts: SearchOptions) : JS.Promise<SearchOutcome> =
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

let private fuzzyFindMulti
    (patterns: string list)
    (params': FuzzyFindParams)
    (opts: SearchOptions)
    : JS.Promise<SearchOutcome> =
    promise {
        let searchPath = resolveFuzzySearchPath params'.path opts.cwd

        let externalBasePath =
            if searchPath.external then
                Some searchPath.basePath
            else
                None

        let! finderResult = opts.finderCache.Get searchPath.basePath

        match finderResult with
        | Error msg -> return { output = msg; isError = true }
        | Ok finder ->
            try
                let runOne pat =
                    promise {
                        match resolveFindSearchStateForPattern pat params' opts with
                        | Error msg -> return (pat, { output = msg; isError = true })
                        | Ok state ->
                            let raw =
                                finder.fileSearch (
                                    state.query,
                                    box
                                        {| pageIndex = 0
                                           pageSize = state.pageSize |}
                                )

                            if not (Dyn.truthy (Dyn.get raw "ok")) then
                                return
                                    (pat,
                                     { output = errorMsg raw "fuzzy_find failed"
                                       isError = true })
                            else
                                let value = Dyn.get raw "value"
                                let matches = itemsOf value |> Array.map toFindMatch |> List.ofArray
                                let totalOpt = optInt value "totalMatched"
                                let totalFiles = optInt value "totalFiles" |> Option.defaultValue 0

                                let result =
                                    Some
                                        { items = matches
                                          totalMatched = totalOpt
                                          totalFiles = totalFiles }

                                return
                                    (pat,
                                     { output = formatFindOutput result
                                       isError = false })
                    }

                let promises = patterns |> List.map runOne |> List.toArray
                let! outcomes = Promise.all promises

                let body =
                    outcomes
                    |> Array.map (fun (pat, r) -> $"## pattern: \"{pat}\"\n{r.output}")
                    |> Array.toList
                    |> String.concat "\n\n"

                return { output = body; isError = false }
            finally
                if externalBasePath.IsSome then
                    opts.finderCache.Destroy searchPath.basePath |> ignore
    }

let fuzzyFind (params': FuzzyFindParams) (opts: SearchOptions) : JS.Promise<SearchOutcome> =
    match params'.pattern with
    | [ _ ]
    | [] -> fuzzyFindSingle params' opts
    | multi -> fuzzyFindMulti multi params' opts

let fuzzyFindContinue (state: FuzzyFindState) (store: TypedIteratorStore) (opts: SearchOptions) : JS.Promise<SearchOutcome> =
    promise {
        let! finderResult = acquireFinderFromOptions state.externalBasePath opts
        return runWithFinder finderResult state.externalBasePath (runFind state store opts)
    }
