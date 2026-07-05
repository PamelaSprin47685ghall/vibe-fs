module Wanxiangshu.Shell.FuzzySearchGrep

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Kernel.FuzzyPath
open Wanxiangshu.Kernel.FuzzyQuery
open Wanxiangshu.Kernel.FuzzyFormat
open Wanxiangshu.Shell.FuzzyIteratorStore
open Wanxiangshu.Shell.FuzzyFinderShell
open Wanxiangshu.Shell.FuzzySearchHelpers

let resolveGrepIteratorStateForPattern (pattern: string) (params': FuzzyGrepParams) (opts: SearchOptions)
    : Result<GrepIteratorState, string> =
    if pattern.Trim() = "" then Error "pattern is required on the first call"
    else
        let searchPath = resolveFuzzySearchPath params'.path opts.cwd
        let externalBasePath = if searchPath.external then Some searchPath.basePath else None
        let mode = detectGrepMode pattern
        if checkWildcardOnly pattern mode then
            Error $"Pattern '{pattern}' matches everything - fuzzy_grep needs a concrete substring or identifier."
        else
            let query = buildQuery searchPath.pathConstraint pattern params'.exclude searchPath.basePath searchPath.external
            let query = if defaultArg params'.searchIgnored false then "git:ignored " + query else query
            Ok { core =
                    { query = query
                      mode = mode
                      smartCase = defaultArg params'.caseSensitive false |> not
                      beforeContext = defaultArg params'.context 0
                      afterContext = defaultArg params'.context 0
                      pageSize = defaultArg params'.limit 50
                      externalBasePath = externalBasePath }
                 cursor = None }

let resolveGrepIteratorState (params': FuzzyGrepParams) (opts: SearchOptions)
    : Result<GrepIteratorState, string> =
    match resolveStore opts with
    | Error msg -> Error msg
    | Ok store ->
    resolveIteratorBranch store params'.iterator consumeGrepIterator "fuzzy_grep" (fun () ->
        match params'.pattern with
        | [] -> Error "pattern is required on the first call"
        | first :: _ -> resolveGrepIteratorStateForPattern first params' opts)

let private runGrep (finder: FinderLike) (state: FuzzyGrepState) (cursor: obj option) (modeOverride: string option) : obj =
    let mode = defaultArg modeOverride state.mode
    let opts = box {| mode = mode; smartCase = state.smartCase; maxMatchesPerFile = state.pageSize; pageSize = state.pageSize; cursor = cursor; beforeContext = state.beforeContext; afterContext = state.afterContext; classifyDefinitions = true |}
    finder.grep(state.query, opts)

let private typedOf (result: obj) : GrepMatch list * int option * string option * obj =
    let matches = itemsOf result |> Array.map toGrepMatch |> List.ofArray
    (matches, optInt result "totalMatched", optStr result "regexFallbackError", Dyn.get result "nextCursor")

let resolveResult (raw: obj) : ResolvedGrep =
    let value = Dyn.get raw "value"
    let (matches, total, regexError, cursor) = typedOf value
    { matches = matches; total = total; regexError = regexError; cursor = cursor }

let private grepNextIterator (state: FuzzyGrepState) (store: TypedIteratorStore) (opts: SearchOptions) (cursor: obj) : string =
    if Dyn.isNullish cursor then ""
    else storeGrepIterator store opts.scopeId { core = state; cursor = Some cursor }

let private runGrepWithFinder (state: FuzzyGrepState) (cursor: obj option) (store: TypedIteratorStore) (opts: SearchOptions) (finder: FinderLike) : SearchOutcome =
    let raw = runGrep finder state cursor None
    if not (Dyn.truthy (Dyn.get raw "ok")) then { output = errorMsg raw "fuzzy_grep failed"; isError = true }
    else
        let resolved = resolveResult raw
        let body = formatGrepOutput (Some { items = resolved.matches; totalMatched = resolved.total; regexFallbackError = resolved.regexError })
        let nextIterator = grepNextIterator state store opts resolved.cursor
        { output = buildGrepOutput body resolved.regexError nextIterator; isError = false }

let private fuzzyGrepSingle (params': FuzzyGrepParams) (opts: SearchOptions) : JS.Promise<SearchOutcome> =
    promise {
        match resolveStore opts with
        | Error msg -> return { output = msg; isError = true }
        | Ok store ->
            match resolveGrepIteratorState params' opts with
            | Error msg -> return { output = msg; isError = true }
            | Ok iteratorState ->
                let! finderResult = acquireFinderFromOptions iteratorState.core.externalBasePath opts
                return runWithFinder finderResult iteratorState.core.externalBasePath (runGrepWithFinder iteratorState.core iteratorState.cursor store opts)
    }

let private fuzzyGrepMulti (patterns: string list) (params': FuzzyGrepParams) (opts: SearchOptions) : JS.Promise<SearchOutcome> =
    promise {
        let searchPath = resolveFuzzySearchPath params'.path opts.cwd
        let externalBasePath = if searchPath.external then Some searchPath.basePath else None
        let! finderResult = opts.finderCache.Get searchPath.basePath
        match finderResult with
        | Error msg -> return { output = msg; isError = true }
        | Ok finder ->
            try
                let runOne pat =
                    promise {
                        try
                            match resolveGrepIteratorStateForPattern pat params' opts with
                            | Error msg -> return (pat, { output = msg; isError = true }, None)
                            | Ok state ->
                                let raw = runGrep finder state.core None None
                                if not (Dyn.truthy (Dyn.get raw "ok")) then
                                    return (pat, { output = errorMsg raw "fuzzy_grep failed"; isError = true }, None)
                                else
                                    let resolved = resolveResult raw
                                    let body = formatGrepOutput (Some { items = resolved.matches; totalMatched = resolved.total; regexFallbackError = resolved.regexError })
                                    return (pat, { output = buildGrepOutput body resolved.regexError ""; isError = false }, resolved.regexError)
                        with ex ->
                            return (pat, { output = ex.Message; isError = true }, None)
                    }
                let promises = patterns |> List.map runOne |> List.toArray
                let! outcomes = Promise.all promises
                let body =
                    outcomes
                    |> Array.map (fun (pat, r, _) ->
                        $"## pattern: \"{pat}\"\n{r.output}")
                    |> Array.toList
                    |> String.concat "\n\n"
                let anyError = outcomes |> Array.exists (fun (_, r, _) -> r.isError)
                return { output = body; isError = anyError }
            finally
                if externalBasePath.IsSome then
                    opts.finderCache.Destroy searchPath.basePath |> ignore
    }

let fuzzyGrep (params': FuzzyGrepParams) (opts: SearchOptions) : JS.Promise<SearchOutcome> =
    match params'.pattern with
    | [ single ] -> fuzzyGrepSingle params' opts
    | [] -> fuzzyGrepSingle params' opts
    | multi -> fuzzyGrepMulti multi params' opts
