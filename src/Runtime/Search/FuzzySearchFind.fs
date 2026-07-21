module Wanxiangshu.Runtime.FuzzySearchFind

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Kernel.FuzzyPath
open Wanxiangshu.Kernel.FuzzyQuery
open Wanxiangshu.Kernel.FuzzyFormat
open Wanxiangshu.Runtime.FuzzyIteratorStore
open Wanxiangshu.Runtime.FuzzyFinderShell
open Wanxiangshu.Runtime.FuzzySearchSupport
open Wanxiangshu.Runtime.FuzzySearchFindHelper

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
        let body, result = processRawFindResponse value

        let totalForPaging =
            match result.totalMatched with
            | Some total -> total
            | None ->
                if result.items.Length >= state.pageSize then
                    (state.pageIndex + 2) * state.pageSize
                else
                    0

        let nextIterator = findNextIterator state store opts totalForPaging

        let output =
            if nextIterator = "" then
                body
            else
                Wanxiangshu.Runtime.ToolOutputInfo.withIterator body nextIterator

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

let private runFindForPattern
    (finder: FinderLike)
    (params': FuzzyFindParams)
    (opts: SearchOptions)
    (pat: string)
    : JS.Promise<string * SearchOutcome> =
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
                let body, _ = processRawFindResponse value

                return (pat, { output = body; isError = false })
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
                let promises =
                    patterns |> List.map (runFindForPattern finder params' opts) |> List.toArray

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

let locateFuzzyMatches (params': FuzzyFindParams) (opts: SearchOptions) : JS.Promise<SearchOutcome> =
    match params'.pattern with
    | [ _ ]
    | [] -> fuzzyFindSingle params' opts
    | multi -> fuzzyFindMulti multi params' opts

let paginateFuzzyFindMatches
    (state: FuzzyFindState)
    (store: TypedIteratorStore)
    (opts: SearchOptions)
    : JS.Promise<SearchOutcome> =
    promise {
        let! finderResult = acquireFinderFromOptions state.externalBasePath opts
        return runWithFinder finderResult state.externalBasePath (runFind state store opts)
    }
