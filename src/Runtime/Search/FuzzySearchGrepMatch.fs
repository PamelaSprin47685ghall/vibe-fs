module Wanxiangshu.Runtime.FuzzySearchGrepMatch

open Fable.Core
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Kernel.FuzzyPath
open Wanxiangshu.Kernel.FuzzyQuery
open Wanxiangshu.Kernel.FuzzyFormat
open Wanxiangshu.Runtime.FuzzyIteratorStore
open Wanxiangshu.Runtime.FuzzyFinderShell
open Wanxiangshu.Runtime.FuzzySearchSupport
open Wanxiangshu.Runtime.FuzzyGrepTypes

let resolveGrepIteratorStateForPattern
    (pattern: string)
    (params': FuzzyGrepParams)
    (opts: SearchOptions)
    : Result<GrepIteratorState, string> =
    if pattern.Trim() = "" then
        Error "pattern is required on the first call"
    else
        let searchPath = resolveFuzzySearchPath params'.path opts.cwd

        let externalBasePath =
            if searchPath.external then
                Some searchPath.basePath
            else
                None

        let mode = detectGrepMode pattern

        if checkWildcardOnly pattern mode then
            Error $"Pattern '{pattern}' matches everything - fuzzy_grep needs a concrete substring or identifier."
        else
            let query =
                buildQuery searchPath.pathConstraint pattern params'.exclude searchPath.basePath searchPath.external

            let query =
                if defaultArg params'.searchIgnored false then
                    "git:ignored " + query
                else
                    query

            Ok
                { core =
                    { query = query
                      mode = mode
                      smartCase = defaultArg params'.caseSensitive false |> not
                      beforeContext = defaultArg params'.context 0
                      afterContext = defaultArg params'.context 0
                      pageSize = defaultArg params'.limit 50
                      externalBasePath = externalBasePath }
                  cursor = None }

let resolveGrepIteratorState (params': FuzzyGrepParams) (opts: SearchOptions) : Result<GrepIteratorState, string> =
    match params'.pattern with
    | [] -> Error "pattern is required on the first call"
    | first :: _ -> resolveGrepIteratorStateForPattern first params' opts

let runGrep (finder: FinderLike) (state: FuzzyGrepState) (cursor: obj option) (modeOverride: string option) : obj =
    let mode = defaultArg modeOverride state.mode

    let opts =
        box
            {| mode = mode
               smartCase = state.smartCase
               maxMatchesPerFile = state.pageSize
               pageSize = state.pageSize
               cursor = cursor
               beforeContext = state.beforeContext
               afterContext = state.afterContext
               classifyDefinitions = true |}

    finder.grep (state.query, opts)

let private typedOf (result: obj) : GrepMatch list * int option * string option * obj =
    let matches = itemsOf result |> Array.map toGrepMatch |> List.ofArray
    (matches, optInt result "totalMatched", optStr result "regexFallbackError", Dyn.get result "nextCursor")

let resolveResult (raw: obj) : ResolvedGrep =
    let value = Dyn.get raw "value"
    let (matches, total, regexError, cursor) = typedOf value

    { matches = matches
      total = total
      regexError = regexError
      cursor = cursor }

let grepNextIterator (state: FuzzyGrepState) (store: TypedIteratorStore) (opts: SearchOptions) (cursor: obj) : string =
    if Dyn.isNullish cursor then
        ""
    else
        storeGrepIterator store opts.scopeId { core = state; cursor = Some cursor }
