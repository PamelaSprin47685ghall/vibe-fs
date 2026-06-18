module VibeFs.Shell.FuzzyCommands

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.FuzzyFormat
open VibeFs.Shell.IteratorStore
open VibeFs.Shell.FuzzyFinderShell
open VibeFs.Shell.FuzzyCoordinator

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

type ResolvedGrep = { matches: GrepMatch list; total: int option; regexError: string option; cursor: obj }

let resolveResult (raw: obj) : ResolvedGrep =
    let value = Dyn.get raw "value"
    let (matches, total, regexError, cursor) = typedOf value
    { matches = matches; total = total; regexError = regexError; cursor = cursor }

let private grepNextIterator (state: FuzzyGrepState) (store: obj) (opts: SearchOptions) (cursor: obj) : string =
    if Dyn.isNullish cursor then "" else storeIterator<FuzzyGrepState> store opts.scopeId "ffi_i" { state with cursor = Some cursor }

let buildGrepOutput (body: string) (regexError: string option) (nextIterator: string) : string =
    let regexNotice = regexError |> Option.map (fun error -> sprintf "Invalid regex: %s, used literal match" error)
    let iteratorNotice = sprintf "iterator=\"%s\"" nextIterator
    let notices = (regexNotice |> Option.toList) @ [ iteratorNotice ]
    sprintf "%s\n\n[%s]" body (String.concat ". " notices)

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
