module VibeFs.Shell.FuzzySearch

open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Fuzzy
open VibeFs.Shell.FuzzyFinderShell

let findIteratorNamespace = "ffi_f"
let grepIteratorNamespace = "ffi_i"

type TypedIteratorStore =
    private
        { findIterators: Dictionary<string, FuzzyFindState>
          grepIterators: Dictionary<string, FuzzyGrepState>
          mutable counter: int
          maxIterators: int }

let createTypedIteratorStore (maxIterators: int) : TypedIteratorStore =
    { findIterators = Dictionary<string, FuzzyFindState>()
      grepIterators = Dictionary<string, FuzzyGrepState>()
      counter = 0
      maxIterators = if maxIterators > 0 then maxIterators else 200 }

let private nextId (store: TypedIteratorStore) (scopeId: string) (namespace': string) : string =
    store.counter <- store.counter + 1
    if scopeId = "global" then namespace' + string store.counter
    else scopeId + ":" + namespace' + ":" + string store.counter

let private trim<'t> (dict: Dictionary<string, 't>) (max: int) : unit =
    if dict.Count > max then
        let first = Seq.head dict.Keys
        dict.Remove(first) |> ignore

let storeFindIterator (store: TypedIteratorStore) (scopeId: string) (state: FuzzyFindState) : string =
    let id = nextId store scopeId findIteratorNamespace
    store.findIterators.[id] <- state
    trim store.findIterators store.maxIterators
    id

let storeGrepIterator (store: TypedIteratorStore) (scopeId: string) (state: FuzzyGrepState) : string =
    let id = nextId store scopeId grepIteratorNamespace
    store.grepIterators.[id] <- state
    trim store.grepIterators store.maxIterators
    id

let consumeFindIterator (store: TypedIteratorStore) (id: string) : FuzzyFindState option =
    match store.findIterators.TryGetValue id with
    | true, state -> store.findIterators.Remove id |> ignore; Some state
    | false, _ -> None

let consumeGrepIterator (store: TypedIteratorStore) (id: string) : FuzzyGrepState option =
    match store.grepIterators.TryGetValue id with
    | true, state -> store.grepIterators.Remove id |> ignore; Some state
    | false, _ -> None

let clearTypedIteratorScope (store: TypedIteratorStore) (scopeId: string) : unit =
    let prefix = scopeId + ":"
    let dropFrom (dict: Dictionary<string, _>) =
        let keys = dict.Keys |> Seq.filter (fun k -> k.StartsWith prefix) |> Seq.toArray
        for key in keys do dict.Remove key |> ignore
    dropFrom store.findIterators
    dropFrom store.grepIterators

let clearTypedIteratorStore (store: TypedIteratorStore) : unit =
    store.findIterators.Clear()
    store.grepIterators.Clear()
    store.counter <- 0

// ─── Compatibility wrappers ─────────────────────────────────────────────────
// The original API stored arbitrary values keyed by (scopeId, namespace).  The
// typed store only holds the two state types fuzzy_find/fuzzy_grep need; these
// wrappers route legacy calls to the appropriate typed bucket by namespace.

let createIteratorStore (maxIterators: int) : obj =
    box (createTypedIteratorStore maxIterators)

let globalIteratorStore : obj = createIteratorStore 200

let storeIterator<'t> (store: obj) (scopeId: string) (namespace': string) (value: 't) : string =
    let typed = unbox<TypedIteratorStore> store
    if namespace' = findIteratorNamespace then
        storeFindIterator typed scopeId (unbox<FuzzyFindState> (box value))
    elif namespace' = grepIteratorNamespace then
        storeGrepIterator typed scopeId (unbox<FuzzyGrepState> (box value))
    else
        failwithf "Unknown iterator namespace: %s" namespace'

let consumeIterator<'t> (store: obj) (id: string) : 't option =
    let typed = unbox<TypedIteratorStore> store
    match consumeFindIterator typed id with
    | Some state -> Some (unbox<'t> (box state))
    | None ->
        match consumeGrepIterator typed id with
        | Some state -> Some (unbox<'t> (box state))
        | None -> None

let clearIteratorScope (store: obj) (scopeId: string) : unit =
    clearTypedIteratorScope (unbox<TypedIteratorStore> store) scopeId

let clearIteratorStore (store: obj) : unit =
    clearTypedIteratorStore (unbox<TypedIteratorStore> store)

// ─── Search options & state resolution ──────────────────────────────────────

type SearchOptions =
    { cwd: string
      scopeId: string
      store: obj option
      finderCache: FinderCache }

let resolveStore (opts: SearchOptions) : TypedIteratorStore =
    match opts.store with
    | Some s -> unbox<TypedIteratorStore> s
    | None -> unbox<TypedIteratorStore> globalIteratorStore

let resolveFindSearchState (params': FuzzyFindParams) (opts: SearchOptions)
    : Result<FuzzyFindState, string> =
    let store = resolveStore opts
    match params'.iterator with
    | Some it ->
        match consumeFindIterator store it with
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
        match consumeGrepIterator store it with
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

// ─── Finder acquisition / release ───────────────────────────────────────────

let acquireFinderFromOptions externalBasePath (opts: SearchOptions) =
    match externalBasePath with
    | Some basep -> createFinder basep
    | None -> opts.finderCache.Get opts.cwd

let releaseFinder (finder: FinderLike) externalBasePath =
    match externalBasePath with
    | Some _ -> finder.destroy()
    | None -> ()

/// Run `body` against an already-acquired finder, releasing external finders
/// even when `body` throws.  External basePath = caller-owned finder we must
/// destroy; cached basePath = pooled finder we must NOT destroy.
let runWithFinder
    (finderResult: Result<FinderLike, string>)
    (externalBasePath: string option)
    (body: FinderLike -> SearchOutcome)
    : SearchOutcome =
    match finderResult with
    | Error msg -> { output = msg; isError = true }
    | Ok finder ->
        try body finder
        finally releaseFinder finder externalBasePath

// ─── JSON helpers ───────────────────────────────────────────────────────────

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

let private errorMsg (raw: obj) (fallback: string) : string =
    if Dyn.isNullish (Dyn.get raw "error") then fallback else Dyn.str raw "error"

// ─── Find pipeline ──────────────────────────────────────────────────────────

let findNextIterator (state: FuzzyFindState) (store: obj) (opts: SearchOptions) (totalForPaging: int) : string =
    let nextPageIndex = state.pageIndex + 1
    if totalForPaging > nextPageIndex * state.pageSize then
        let nextState : FuzzyFindState = { state with pageIndex = nextPageIndex }
        storeFindIterator (unbox<TypedIteratorStore> store) opts.scopeId nextState
    else ""

let private runFind (state: FuzzyFindState) (store: TypedIteratorStore) (opts: SearchOptions) (finder: FinderLike) : SearchOutcome =
    let raw = finder.fileSearch(state.query, box {| pageIndex = state.pageIndex; pageSize = state.pageSize |})
    if not (Dyn.truthy (Dyn.get raw "ok")) then { output = errorMsg raw "fuzzy_find failed"; isError = true }
    else
        let value = Dyn.get raw "value"
        let matches = itemsOf value |> Array.map toFindMatch |> List.ofArray
        let totalOpt = optInt value "totalMatched"
        let totalForPaging = totalOpt |> Option.defaultValue 0
        let totalFiles = optInt value "totalFiles" |> Option.defaultValue 0
        let body = formatFindOutput (Some { items = matches; totalMatched = totalOpt; totalFiles = totalFiles })
        { output = sprintf "%s\n\n[iterator=\"%s\"]" body (findNextIterator state (box store) opts totalForPaging); isError = false }

let fuzzyFind (params': FuzzyFindParams) (opts: SearchOptions) : JS.Promise<SearchOutcome> =
    async {
        let store = resolveStore opts
        match resolveFindSearchState params' opts with
        | Error msg -> return { output = msg; isError = true }
        | Ok state ->
            let! finderResult = acquireFinderFromOptions state.externalBasePath opts |> Async.AwaitPromise
            return runWithFinder finderResult state.externalBasePath (runFind state store opts)
    }
    |> Async.StartAsPromise

// ─── Grep pipeline ──────────────────────────────────────────────────────────

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

let private grepNextIterator (state: FuzzyGrepState) (store: TypedIteratorStore) (opts: SearchOptions) (cursor: obj) : string =
    if Dyn.isNullish cursor then ""
    else storeGrepIterator store opts.scopeId { state with cursor = Some cursor }

let private runGrepWithFinder (state: FuzzyGrepState) (store: TypedIteratorStore) (opts: SearchOptions) (finder: FinderLike) : SearchOutcome =
    let raw = runGrep finder state None
    if not (Dyn.truthy (Dyn.get raw "ok")) then { output = errorMsg raw "fuzzy_grep failed"; isError = true }
    else
        let resolved = resolveResult raw
        let body = formatGrepOutput (Some { items = resolved.matches; totalMatched = resolved.total; regexFallbackError = resolved.regexError })
        let nextIterator = grepNextIterator state store opts resolved.cursor
        { output = buildGrepOutput body resolved.regexError nextIterator; isError = false }

let fuzzyGrep (params': FuzzyGrepParams) (opts: SearchOptions) : JS.Promise<SearchOutcome> =
    async {
        let store = resolveStore opts
        match resolveGrepSearchState params' opts with
        | Error msg -> return { output = msg; isError = true }
        | Ok state ->
            let! finderResult = acquireFinderFromOptions state.externalBasePath opts |> Async.AwaitPromise
            return runWithFinder finderResult state.externalBasePath (runGrepWithFinder state store opts)
    }
    |> Async.StartAsPromise
