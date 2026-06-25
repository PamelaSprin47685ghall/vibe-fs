module VibeFs.Shell.FuzzySearch

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Shell.Dyn
open VibeFs.Kernel.FuzzyPath
open VibeFs.Kernel.FuzzyQuery
open VibeFs.Kernel.FuzzyFormat
open VibeFs.Shell.FuzzyFinderShell
open VibeFs.Shell.FuzzyIteratorStore

let parseExcludeField (args: obj) : string list =
    let v = Dyn.get args "exclude"
    if Dyn.isNullish v then []
    elif Dyn.isArray v then v :?> obj array |> Array.map string |> List.ofArray
    else [ string v ]

type ResolvedGrep = { matches: GrepMatch list; total: int option; regexError: string option; cursor: obj }

// ─── Search options & state resolution ──────────────────────────────────────

type SearchOptions =
    { cwd: string
      scopeId: string
      store: TypedIteratorStore option
      finderCache: FinderCache }

let resolveStore (opts: SearchOptions) : Result<TypedIteratorStore, string> =
    match opts.store with
    | Some s -> Ok s
    | None ->
        Error "FuzzySearch requires SearchOptions.store; inject RuntimeScope.IteratorStore from the tool registration path"

let private iteratorError toolName it = $"{toolName} iterator error: unknown, expired, or already consumed iterator \"{it}\""

let private resolveIteratorBranch (store: TypedIteratorStore) iterator consume toolName onFresh =
    match iterator with
    | Some it when it <> "" -> match consume store it with Some s -> Ok s | None -> Error (iteratorError toolName it)
    | _ -> onFresh ()

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

let resolveGrepIteratorState (params': FuzzyGrepParams) (opts: SearchOptions)
    : Result<GrepIteratorState, string> =
    match resolveStore opts with
    | Error msg -> Error msg
    | Ok store ->
    resolveIteratorBranch store params'.iterator consumeGrepIterator "fuzzy_grep" (fun () ->
        match params'.pattern with
        | None | Some "" -> Error "pattern is required on the first call"
        | Some pattern ->
            let searchPath = resolveFuzzySearchPath params'.path opts.cwd
            let externalBasePath = if searchPath.external then Some searchPath.basePath else None
            let mode = detectGrepMode pattern
            if checkWildcardOnly pattern mode then
                Error $"Pattern '{pattern}' matches everything - fuzzy_grep needs a concrete substring or identifier."
            else
                Ok { core =
                        { query = buildQuery searchPath.pathConstraint pattern params'.exclude searchPath.basePath searchPath.external
                          mode = mode
                          smartCase = defaultArg params'.caseSensitive false |> not
                          beforeContext = defaultArg params'.context 0
                          afterContext = defaultArg params'.context 0
                          pageSize = defaultArg params'.limit 50
                          externalBasePath = externalBasePath }
                     cursor = None })

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
        let totalForPaging = totalOpt |> Option.defaultValue 0
        let totalFiles = optInt value "totalFiles" |> Option.defaultValue 0
        let body = formatFindOutput (Some { items = matches; totalMatched = totalOpt; totalFiles = totalFiles })
        let nextIterator = findNextIterator state store opts totalForPaging
        let output =
            if nextIterator = "" then body
            else VibeFs.Kernel.ToolOutputInfo.withIterator body nextIterator
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

// ─── Grep pipeline ──────────────────────────────────────────────────────────

let private runGrep (finder: FinderLike) (state: FuzzyGrepState) (cursor: obj option) (modeOverride: string option) : obj =
    let mode = defaultArg modeOverride state.mode
    let opts = box {| mode = mode; smartCase = state.smartCase; maxMatchesPerFile = min state.pageSize 50; pageSize = state.pageSize; cursor = cursor; beforeContext = state.beforeContext; afterContext = state.afterContext; classifyDefinitions = true |}
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

let fuzzyGrep (params': FuzzyGrepParams) (opts: SearchOptions) : JS.Promise<SearchOutcome> =
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
