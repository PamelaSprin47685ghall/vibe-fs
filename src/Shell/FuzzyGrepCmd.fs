module VibeFs.Shell.FuzzyGrepCmd

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.IteratorStore
open VibeFs.Kernel.FuzzyFormat
open VibeFs.Kernel
open VibeFs.Shell.FuzzyFinderShell
open VibeFs.Shell.FuzzyCoordinator
open VibeFs.Shell.FuzzyRawMapping

let fuzzyMatchNotice = "0 exact matches. Maybe you meant this?"

/// Run a grep (with optional mode override) against a finder and return the raw obj.
let private runGrep (finder: FinderLike) (s: FuzzyGrepState) (modeOverride: string option) : obj =
    let mode = defaultArg modeOverride s.mode
    let sameMode = mode = s.mode
    let opts = box {| mode = mode; smartCase = s.smartCase; maxMatchesPerFile = min s.pageSize 50;
                       pageSize = s.pageSize;
                       cursor = (if sameMode then s.cursor else None);
                       beforeContext = (if sameMode then s.beforeContext else 0);
                       afterContext = (if sameMode then s.afterContext else 0);
                       classifyDefinitions = true |}
    finder.grep(s.query, opts)

let private errorMsg (raw: obj) (fallback: string) : string =
    if Dyn.isNullish (Dyn.get raw "error") then fallback else Dyn.str raw "error"

/// Read typed fields from a raw result object.  totalMatched stays an option so
/// explicit 0 is preserved (TS `??` only falls back for null/undefined).
let private typedOf (result: obj) : GrepMatch list * int option * string option * obj =
    let matches = itemsOf result |> Array.map toGrepMatch |> List.ofArray
    (matches, optInt result "totalMatched", optStr result "regexFallbackError", Dyn.get result "nextCursor")

/// The full resolution of a grep round: typed matches, the (possibly fuzzy-updated)
/// search state, the next cursor, and a notice when fuzzy fallback fired.
type ResolvedGrep =
    { matches: GrepMatch list; total: int option; regexError: string option
      cursor: obj; fuzzyNotice: string option; state: FuzzyGrepState }

/// From a raw grep result (with optional fuzzy fallback), resolve the typed
/// matches.  When fuzzy fallback succeeds, return the notice AND the rewritten
/// search state (mode=fuzzy, context cleared, cursor reset) — matching tryFuzzyFallback.
/// Public so the fuzzy-fallback semantics can be regression-tested with a mock finder.
let resolveResult (finder: FinderLike) (state: FuzzyGrepState) (params': FuzzyGrepParams) (raw: obj)
    : ResolvedGrep =
    let value = Dyn.get raw "value"
    let baseResolved () : ResolvedGrep =
        let (m, t, re, c) = typedOf value
        { matches = m; total = t; regexError = re; cursor = c; fuzzyNotice = None; state = state }
    if not (Array.isEmpty (itemsOf value)) then baseResolved ()
    elif Option.isSome params'.iterator || state.mode = "regex" then baseResolved ()
    else
        let fuzzyRaw = runGrep finder state (Some "fuzzy")
        if Dyn.truthy (Dyn.get fuzzyRaw "ok") && not (Array.isEmpty (itemsOf (Dyn.get fuzzyRaw "value"))) then
            let (m, t, re, _) = typedOf (Dyn.get fuzzyRaw "value")
            let fuzzyState = { state with mode = "fuzzy"; beforeContext = 0; afterContext = 0; cursor = None }
            { matches = m; total = t; regexError = re
              cursor = (Dyn.get (Dyn.get fuzzyRaw "value") "nextCursor")
              fuzzyNotice = Some fuzzyMatchNotice; state = fuzzyState }
        else baseResolved ()

let private grepNextIterator (state: FuzzyGrepState) (store: obj) (opts: SearchOptions) (cursor: obj) : string =
    if Dyn.isNullish cursor then ""
    else storeIterator<FuzzyGrepState> store opts.scopeId "ffi_i" { state with cursor = Some cursor }

/// Pure: build the final grep output — formatted body + regex/iterator notices +
/// optional fuzzy-match banner.  Extracted so the shaping logic is testable.
let buildGrepOutput (body: string) (regexError: string option) (nextIterator: string) (fuzzyNotice: string option) : string =
    let regexNotice = regexError |> Option.map (fun e -> sprintf "Invalid regex: %s, used literal match" e)
    let iteratorNotice = sprintf "iterator=\"%s\"" nextIterator
    let notices = (regexNotice |> Option.toList) @ [ iteratorNotice ]
    let annotated = sprintf "%s\n\n[%s]" body (String.concat ". " notices)
    match fuzzyNotice with Some n -> sprintf "[%s]\n%s" n annotated | None -> annotated

/// Run a fuzzy content search with regex/fuzzy fallback.  Reuses
/// FuzzyFormat.formatGrepOutput; threads the fuzzy-updated state into the
/// pagination iterator so "continue" resumes in the right mode.
let fuzzyGrep (params': FuzzyGrepParams) (opts: SearchOptions) : JS.Promise<SearchOutcome> =
    async {
        let store = resolveStore opts
        match resolveGrepSearchState params' opts with
        | Error msg -> return { output = msg; isError = true }
        | Ok state ->
            let! finderResult = acquireFinder state.externalBasePath opts.cwd |> Async.AwaitPromise
            match finderResult with
            | Error msg -> return { output = msg; isError = true }
            | Ok finder ->
                try
                    let raw = runGrep finder state None
                    if not (Dyn.truthy (Dyn.get raw "ok")) then
                        return { output = errorMsg raw "fuzzy_grep failed"; isError = true }
                    else
                        let r = resolveResult finder state params' raw
                        let body = formatGrepOutput (Some { items = r.matches; totalMatched = r.total; regexFallbackError = r.regexError })
                        let nextIterator = grepNextIterator r.state store opts r.cursor
                        return { output = buildGrepOutput body r.regexError nextIterator r.fuzzyNotice; isError = false }
                finally
                    releaseFinder finder state.externalBasePath
    }
    |> Async.StartAsPromise
