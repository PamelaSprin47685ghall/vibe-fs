module VibeFs.Shell.FuzzyFindCmd

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Shell.IteratorStore
open VibeFs.Kernel.FuzzyFormat
open VibeFs.Kernel
open VibeFs.Shell.FuzzyFinderShell
open VibeFs.Shell.FuzzyCoordinator
open VibeFs.Shell.FuzzyRawMapping

/// The error message from a failed finder call, or a fallback when `error` is absent.
let private errorMsg (raw: obj) (fallback: string) : string =
    if Dyn.isNullish (Dyn.get raw "error") then fallback else Dyn.str raw "error"

/// Store a next-page iterator for find pagination, or "" when there is no next
/// page.  Public so the pagination decision (using the paging total, default 0)
/// can be regression-tested.
let findNextIterator (state: FuzzyFindState) (store: obj) (opts: SearchOptions) (totalForPaging: int) : string =
    let nextPageIndex = state.pageIndex + 1
    if totalForPaging > nextPageIndex * state.pageSize then
        let nextState : FuzzyFindState =
            { query = state.query; pageSize = state.pageSize
              pageIndex = nextPageIndex; externalBasePath = state.externalBasePath }
        storeIterator<FuzzyFindState> store opts.scopeId "ffi_f" nextState
    else ""

/// Run a fuzzy file search and format the result with a pagination iterator.
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
                    if not (Dyn.truthy (Dyn.get raw "ok")) then
                        return { output = errorMsg raw "fuzzy_find failed"; isError = true }
                    else
                        let value = Dyn.get raw "value"
                        let matches = itemsOf value |> Array.map toFindMatch |> List.ofArray
                        // totalMatched is passed as an option so explicit 0 is preserved
                        // (TS `??` only falls back for null/undefined).  Paging uses ?? 0.
                        let totalOpt = optInt value "totalMatched"
                        let totalForPaging = totalOpt |> Option.defaultValue 0
                        let totalFiles = optInt value "totalFiles" |> Option.defaultValue 0
                        let body = formatFindOutput (Some { items = matches; totalMatched = totalOpt; totalFiles = totalFiles })
                        return { output = sprintf "%s\n\n[iterator=\"%s\"]" body (findNextIterator state store opts totalForPaging); isError = false }
                finally
                    releaseFinder finder state.externalBasePath
    }
    |> Async.StartAsPromise
