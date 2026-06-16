module VibeFs.Shell.FuzzyGrepCmd

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Shell.IteratorStore
open VibeFs.Kernel.FuzzyFormat
open VibeFs.Kernel
open VibeFs.Shell.FuzzyFinderShell
open VibeFs.Shell.FuzzyCoordinator
open VibeFs.Shell.FuzzyRawMapping

/// Run a grep (with optional mode override) against a finder and return the raw obj.
let private runGrep (finder: FinderLike) (s: FuzzyGrepState) (modeOverride: string option) : obj =
    let mode = defaultArg modeOverride s.mode
    let opts = box {| mode = mode; smartCase = s.smartCase; maxMatchesPerFile = min s.pageSize 50;
                       pageSize = s.pageSize;
                       cursor = s.cursor;
                       beforeContext = s.beforeContext;
                       afterContext = s.afterContext;
                       classifyDefinitions = true |}
    finder.grep(s.query, opts)

let private errorMsg (raw: obj) (fallback: string) : string =
    if Dyn.isNullish (Dyn.get raw "error") then fallback else Dyn.str raw "error"

/// Read typed fields from a raw result object.  totalMatched stays an option so
/// explicit 0 is preserved (TS `??` only falls back for null/undefined).
let private typedOf (result: obj) : GrepMatch list * int option * string option * obj =
    let matches = itemsOf result |> Array.map toGrepMatch |> List.ofArray
    (matches, optInt result "totalMatched", optStr result "regexFallbackError", Dyn.get result "nextCursor")

/// The full resolution of a grep round: typed matches, the next cursor, and
/// optional regex error.
type ResolvedGrep =
    { matches: GrepMatch list; total: int option; regexError: string option; cursor: obj }

/// Resolve the typed matches from a raw grep result — no implicit fallback.
let resolveResult (raw: obj) : ResolvedGrep =
    let value = Dyn.get raw "value"
    let (m, t, re, c) = typedOf value
    { matches = m; total = t; regexError = re; cursor = c }

let private grepNextIterator (state: FuzzyGrepState) (store: obj) (opts: SearchOptions) (cursor: obj) : string =
    if Dyn.isNullish cursor then ""
    else storeIterator<FuzzyGrepState> store opts.scopeId "ffi_i" { state with cursor = Some cursor }

/// Pure: build the final grep output — formatted body + regex/iterator notices.
let buildGrepOutput (body: string) (regexError: string option) (nextIterator: string) : string =
    let regexNotice = regexError |> Option.map (fun e -> sprintf "Invalid regex: %s, used literal match" e)
    let iteratorNotice = sprintf "iterator=\"%s\"" nextIterator
    let notices = (regexNotice |> Option.toList) @ [ iteratorNotice ]
    sprintf "%s\n\n[%s]" body (String.concat ". " notices)

/// Run a fuzzy content search.  No implicit fuzzy fallback — if regex mode
/// yields no results, the output reflects that and the LLM may retry with
/// mode=fuzzy explicitly.
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
                    if not (Dyn.truthy (Dyn.get raw "ok")) then
                        return { output = errorMsg raw "fuzzy_grep failed"; isError = true }
                    else
                        let r = resolveResult raw
                        let body = formatGrepOutput (Some { items = r.matches; totalMatched = r.total; regexFallbackError = r.regexError })
                        let nextIterator = grepNextIterator state store opts r.cursor
                        return { output = buildGrepOutput body r.regexError nextIterator; isError = false }
                finally
                    releaseFinder finder state.externalBasePath
    }
    |> Async.StartAsPromise
