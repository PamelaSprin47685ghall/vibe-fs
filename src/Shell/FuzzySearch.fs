module VibeFs.Shell.FuzzySearch

open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Fuzzy
open VibeFs.Shell.FuzzyFinderShell

type private Store(maxIterators: int) =
    let iterators = Dictionary<string, obj>()
    let mutable counter = 0
    member _.Iterators = iterators
    member _.Counter
        with get () = counter
        and set value = counter <- value
    member val MaxIterators = maxIterators with get

let createIteratorStore (maxIterators: int) : obj =
    let cap = if maxIterators > 0 then maxIterators else 200
    Store(cap) |> box

let globalIteratorStore : obj = createIteratorStore 200

let storeIterator<'t> (store: obj) (scopeId: string) (namespace': string) (value: 't) : string =
    let s = unbox<Store> store
    s.Counter <- s.Counter + 1
    let id =
        if scopeId = "global" then
            namespace' + string s.Counter
        else
            scopeId + ":" + namespace' + ":" + string s.Counter
    s.Iterators.[id] <- box value
    if s.Iterators.Count > s.MaxIterators then
        let first = Seq.head s.Iterators.Keys
        s.Iterators.Remove(first) |> ignore
    id

let consumeIterator<'t> (store: obj) (id: string) : 't option =
    let s = unbox<Store> store
    match s.Iterators.TryGetValue(id) with
    | true, v ->
        s.Iterators.Remove(id) |> ignore
        Some(unbox<'t> v)
    | false, _ -> None

let clearIteratorScope (store: obj) (scopeId: string) : unit =
    let s = unbox<Store> store
    let prefix = scopeId + ":"
    let keys = s.Iterators.Keys |> Seq.filter (fun k -> k.StartsWith(prefix)) |> Seq.toArray
    for key in keys do
        s.Iterators.Remove(key) |> ignore

let clearIteratorStore (store: obj) : unit =
    let s = unbox<Store> store
    s.Iterators.Clear()
    s.Counter <- 0

type SearchOptions =
    { cwd: string
      scopeId: string
      store: obj option
      finderCache: FinderCache }

let resolveStore (opts: SearchOptions) = defaultArg opts.store globalIteratorStore

let resolveFindSearchState (params': FuzzyFindParams) (opts: SearchOptions)
    : Result<FuzzyFindState, string> =
    let store = resolveStore opts
    match params'.iterator with
    | Some it ->
        match consumeIterator<FuzzyFindState> store it with
        | Some state -> Ok state
        | None -> Error $"fuzzy_find iterator error: unknown, expired, or already consumed iterator \"{it}\""
    | None ->
        match params'.pattern with
        | None | Some "" -> Error "pattern is required on the first call"
        | Some pattern ->
            let searchPath = resolveFuzzySearchPath params'.path opts.cwd
            let externalBasePath = if searchPath.external then Some searchPath.basePath else None
            Ok { query = buildQuery searchPath.pathConstraint pattern [] searchPath.basePath searchPath.external
                 pageSize = defaultArg params'.limit 30
                 pageIndex = 0
                 externalBasePath = externalBasePath }

let resolveGrepSearchState (params': FuzzyGrepParams) (opts: SearchOptions)
    : Result<FuzzyGrepState, string> =
    let store = resolveStore opts
    match params'.iterator with
    | Some it ->
        match consumeIterator<FuzzyGrepState> store it with
        | Some state -> Ok state
        | None -> Error $"fuzzy_grep iterator error: unknown, expired, or already consumed iterator \"{it}\""
    | None ->
        match params'.pattern with
        | None | Some "" -> Error "pattern is required on the first call"
        | Some pattern ->
            let searchPath = resolveFuzzySearchPath params'.path opts.cwd
            let externalBasePath = if searchPath.external then Some searchPath.basePath else None
            let mode = detectGrepMode pattern
            if checkWildcardOnly pattern mode then
                Error $"Pattern '{pattern}' matches everything - fuzzy_grep needs a concrete substring or identifier."
            else
                Ok { query = buildQuery searchPath.pathConstraint pattern params'.exclude searchPath.basePath searchPath.external
                     mode = mode
                     smartCase = defaultArg params'.caseSensitive false |> not
                     beforeContext = defaultArg params'.context 0
                     afterContext = defaultArg params'.context 0
                     pageSize = defaultArg params'.limit 50
                     externalBasePath = externalBasePath
                     cursor = None }

let acquireFinderFromOptions externalBasePath (opts: SearchOptions) =
    match externalBasePath with
    | Some basep -> createFinder basep
    | None -> opts.finderCache.Get opts.cwd

let releaseFinder (finder: FinderLike) externalBasePath =
    match externalBasePath with
    | Some _ -> finder.destroy()
    | None -> ()

let optStr (o: obj) (key: string) : string option =
    let value = Dyn.get o key
    if Dyn.isNullish value then None else Some(string value)

let optInt (o: obj) (key: string) : int option =
    let value = Dyn.get o key
    if Dyn.isNullish value then None else Some(unbox<int> value)

let itemsOf (value: obj) : obj array =
    let items = Dyn.get value "items"
    if Dyn.isNullish items || not (Dyn.isArray items) then [||] else items :?> obj array

let stringListOf (o: obj) (key: string) : string list =
    let value = Dyn.get o key
    if Dyn.isNullish value || not (Dyn.isArray value) then [] else (value :?> obj array) |> Array.map string |> List.ofArray

let annotationOf (item: obj) : FileAnnotation option =
    let git = optStr item "gitStatus"
    let total = optInt item "totalFrecencyScore"
    let access = optInt item "accessFrecencyScore"
    if git.IsSome || total.IsSome || access.IsSome then Some { gitStatus = git; totalFrecencyScore = total; accessFrecencyScore = access } else None

let toFindMatch (item: obj) : FindMatch = { relativePath = Dyn.str item "relativePath"; annotation = annotationOf item }

let toGrepMatch (item: obj) : GrepMatch =
    { relativePath = Dyn.str item "relativePath"
      lineNumber = optInt item "lineNumber" |> Option.defaultValue 0
      lineContent = Dyn.str item "lineContent"
      contextBefore = stringListOf item "contextBefore"
      contextAfter = stringListOf item "contextAfter"
      annotation = annotationOf item }

let private errorMsg (raw: obj) (fallback: string) : string = if Dyn.isNullish (Dyn.get raw "error") then fallback else Dyn.str raw "error"

let findNextIterator (state: FuzzyFindState) (store: obj) (opts: SearchOptions) (totalForPaging: int) : string =
    let nextPageIndex = state.pageIndex + 1
    if totalForPaging > nextPageIndex * state.pageSize then
        let nextState : FuzzyFindState = { query = state.query; pageSize = state.pageSize; pageIndex = nextPageIndex; externalBasePath = state.externalBasePath }
        storeIterator<FuzzyFindState> store opts.scopeId "ffi_f" nextState
    else ""

let fuzzyFind (params': FuzzyFindParams) (opts: SearchOptions) : JS.Promise<SearchOutcome> =
    async {
        let store = resolveStore opts
        match resolveFindSearchState params' opts with
        | Error msg -> return { output = msg; isError = true }
        | Ok state ->
            let! finderResult = acquireFinderFromOptions state.externalBasePath opts |> Async.AwaitPromise
            match finderResult with
            | Error msg -> return { output = msg; isError = true }
            | Ok finder ->
                try
                    let raw = finder.fileSearch(state.query, box {| pageIndex = state.pageIndex; pageSize = state.pageSize |})
                    if not (Dyn.truthy (Dyn.get raw "ok")) then return { output = errorMsg raw "fuzzy_find failed"; isError = true }
                    else
                        let value = Dyn.get raw "value"
                        let matches = itemsOf value |> Array.map toFindMatch |> List.ofArray
                        let totalOpt = optInt value "totalMatched"
                        let totalForPaging = totalOpt |> Option.defaultValue 0
                        let totalFiles = optInt value "totalFiles" |> Option.defaultValue 0
                        let body = formatFindOutput (Some { items = matches; totalMatched = totalOpt; totalFiles = totalFiles })
                        return { output = sprintf "%s\n\n[iterator=\"%s\"]" body (findNextIterator state store opts totalForPaging); isError = false }
                finally
                    releaseFinder finder state.externalBasePath
    }
    |> Async.StartAsPromise

let private runGrep (finder: FinderLike) (state: FuzzyGrepState) (modeOverride: string option) : obj =
    let mode = defaultArg modeOverride state.mode
    let opts = box {| mode = mode; smartCase = state.smartCase; maxMatchesPerFile = min state.pageSize 50; pageSize = state.pageSize; cursor = state.cursor; beforeContext = state.beforeContext; afterContext = state.afterContext; classifyDefinitions = true |}
    finder.grep(state.query, opts)

let private typedOf (result: obj) : GrepMatch list * int option * string option * obj =
    let matches = itemsOf result |> Array.map toGrepMatch |> List.ofArray
    (matches, optInt result "totalMatched", optStr result "regexFallbackError", Dyn.get result "nextCursor")

let resolveResult (raw: obj) : ResolvedGrep =
    let value = Dyn.get raw "value"
    let (matches, total, regexError, cursor) = typedOf value
    { matches = matches; total = total; regexError = regexError; cursor = cursor }

let private grepNextIterator (state: FuzzyGrepState) (store: obj) (opts: SearchOptions) (cursor: obj) : string =
    if Dyn.isNullish cursor then "" else storeIterator<FuzzyGrepState> store opts.scopeId "ffi_i" { state with cursor = Some cursor }

let fuzzyGrep (params': FuzzyGrepParams) (opts: SearchOptions) : JS.Promise<SearchOutcome> =
    async {
        let store = resolveStore opts
        match resolveGrepSearchState params' opts with
        | Error msg -> return { output = msg; isError = true }
        | Ok state ->
            let! finderResult = acquireFinderFromOptions state.externalBasePath opts |> Async.AwaitPromise
            match finderResult with
            | Error msg -> return { output = msg; isError = true }
            | Ok finder ->
                try
                    let raw = runGrep finder state None
                    if not (Dyn.truthy (Dyn.get raw "ok")) then return { output = errorMsg raw "fuzzy_grep failed"; isError = true }
                    else
                        let resolved = resolveResult raw
                        let body = formatGrepOutput (Some { items = resolved.matches; totalMatched = resolved.total; regexFallbackError = resolved.regexError })
                        let nextIterator = grepNextIterator state store opts resolved.cursor
                        return { output = buildGrepOutput body resolved.regexError nextIterator; isError = false }
                finally
                    releaseFinder finder state.externalBasePath
    }
    |> Async.StartAsPromise
