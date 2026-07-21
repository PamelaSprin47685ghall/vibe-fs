module Wanxiangshu.Runtime.FuzzySearchGrep

open Fable.Core
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Kernel.FuzzyPath
open Wanxiangshu.Kernel.FuzzyQuery
open Wanxiangshu.Kernel.FuzzyFormat
open Wanxiangshu.Runtime.FuzzyIteratorStore
open Wanxiangshu.Runtime.FuzzyFinderShell
open Wanxiangshu.Runtime.FuzzySearchSupport
open Wanxiangshu.Runtime.FuzzyGrepTypes
open Wanxiangshu.Runtime.FuzzySearchGrepMatch

let private runGrepWithFinder
    (state: FuzzyGrepState)
    (cursor: obj option)
    (store: TypedIteratorStore)
    (opts: SearchOptions)
    (finder: FinderLike)
    : SearchOutcome =
    let raw = runGrep finder state cursor None

    if not (Dyn.truthy (Dyn.get raw "ok")) then
        { output = errorMsg raw "fuzzy_grep failed"
          isError = true }
    else
        let resolved = resolveResult raw

        let body =
            formatGrepOutput (
                Some
                    { items = resolved.matches
                      totalMatched = resolved.total
                      regexFallbackError = resolved.regexError }
            )

        let nextIterator = grepNextIterator state store opts resolved.cursor

        { output =
            (let b = buildGrepBody body resolved.regexError in

             if nextIterator = "" then
                 b
             else
                 Wanxiangshu.Runtime.ToolOutputInfo.withIterator b nextIterator)
          isError = false }

let private fuzzyGrepSingle (params': FuzzyGrepParams) (opts: SearchOptions) : JS.Promise<SearchOutcome> =
    promise {
        match resolveStore opts with
        | Error msg -> return { output = msg; isError = true }
        | Ok store ->
            match resolveGrepIteratorState params' opts with
            | Error msg -> return { output = msg; isError = true }
            | Ok iteratorState ->
                let! finderResult = acquireFinderFromOptions iteratorState.core.externalBasePath opts

                return
                    runWithFinder
                        finderResult
                        iteratorState.core.externalBasePath
                        (runGrepWithFinder iteratorState.core iteratorState.cursor store opts)
    }

let private fuzzyGrepMulti
    (patterns: string list)
    (params': FuzzyGrepParams)
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
            let cleanup () =
                promise {
                    if externalBasePath.IsSome then
                        do! opts.finderCache.Destroy searchPath.basePath
                }

            let! res =
                promise {
                    try
                        let runOne pat = runGrepPattern finder params' opts pat

                        let promises = patterns |> List.map runOne |> List.toArray
                        let! outcomes = Promise.all promises

                        let body =
                            outcomes
                            |> Array.map (fun (pat, r, _) -> $"## pattern: \"{pat}\"\n{r.output}")
                            |> Array.toList
                            |> String.concat "\n\n"

                        let anyError = outcomes |> Array.exists (fun (_, r, _) -> r.isError)
                        return { output = body; isError = anyError }
                    with ex ->
                        do! cleanup ()
                        return raise ex
                }

            do! cleanup ()
            return res
    }

let searchFuzzyContent (params': FuzzyGrepParams) (opts: SearchOptions) : JS.Promise<SearchOutcome> =
    match params'.pattern with
    | [ _ ]
    | [] -> fuzzyGrepSingle params' opts
    | multi -> fuzzyGrepMulti multi params' opts

let paginateFuzzyGrepContent
    (iteratorState: GrepIteratorState)
    (store: TypedIteratorStore)
    (opts: SearchOptions)
    : JS.Promise<SearchOutcome> =
    promise {
        let! finderResult = acquireFinderFromOptions iteratorState.core.externalBasePath opts

        return
            runWithFinder
                finderResult
                iteratorState.core.externalBasePath
                (runGrepWithFinder iteratorState.core iteratorState.cursor store opts)
    }
