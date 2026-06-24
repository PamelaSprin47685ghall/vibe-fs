module VibeFs.Tests.FuzzyTests

open VibeFs.Tests.Assert
open VibeFs.Kernel.FuzzyPath
open VibeFs.Kernel.FuzzyQuery
open VibeFs.Kernel.FuzzyFormat
open VibeFs.Shell.FuzzySearch
open VibeFs.Shell.FuzzyIteratorStore
open VibeFs.Kernel
open VibeFs.Shell.FuzzyFinderShell

let grepDetect () =
    equal "plain word" "plain" (detectGrepMode "foo")
    equal "plain sentence" "plain" (detectGrepMode "foo bar")
    equal "dot star regex" "regex" (detectGrepMode "foo.*bar")
    equal "alternation regex" "regex" (detectGrepMode "a|b")
    check "wildcard .* rejected" (checkWildcardOnly ".*" "regex")
    check "wildcard . rejected" (checkWildcardOnly "." "regex")
    check "concrete not wildcard" (not (checkWildcardOnly "getUserById" "plain"))

let iteratorRoundTrip () =
    let store = createTypedIteratorStore 10
    let state : FuzzyFindState = { query = "my query"; pageSize = 30; pageIndex = 2; externalBasePath = None }
    let id = storeFindIterator store "scope" state
    let resumed = consumeFindIterator store id
    check "resume present" resumed.IsSome
    equal "query survives" "my query" resumed.Value.query
    equal "pageIndex survives" 2 resumed.Value.pageIndex
    check "single-use" ((consumeFindIterator store id).IsNone)

let finderConversion () =
    let mockFinder = box {| fileSearch = (fun _ _ -> box {| ok = true; value = box {| items = [||]; totalMatched = 0; totalFiles = 0 |} |}) |}
    let okResult = resultFromRaw (box {| ok = true; value = mockFinder |})
    check "ok → Ok" (match okResult with Ok _ -> true | _ -> false)
    let errResult = resultFromRaw (box {| ok = false; error = "scan failed" |})
    check "err → Error" (match errResult with Error _ -> true | _ -> false)
    equal "err message" "scan failed" (match errResult with Error m -> m | _ -> "")
    let noErr = resultFromRaw (box {| ok = false |})
    equal "undefined error fallback" "createFinder failed" (match noErr with Error m -> m | _ -> "")

let formatFull () =
    let hot : FileAnnotation = { gitStatus = None; totalFrecencyScore = Some 30; accessFrecencyScore = None }
    let findOut = formatFindOutput (Some { items = [ { relativePath = "src/hot.ts"; annotation = Some hot } ]
                                           totalMatched = Some 1; totalFiles = 42 })
    check "find total indexed" (findOut.Contains "(42 total indexed)")
    check "find frecency annotation" (findOut.Contains "VERY often touched file")
    let longLine = System.String('x', 600)
    let gm : GrepMatch =
        { relativePath = "a.ts"; lineNumber = 5; lineContent = longLine
          contextBefore = [ "ctx-before" ]; contextAfter = [ "ctx-after" ]; annotation = None }
    let grepOut = formatGrepOutput (Some { items = [ gm ]; totalMatched = Some 1; regexFallbackError = None })
    check "grep context-before" (grepOut.Contains "4- ctx-before")
    check "grep context-after" (grepOut.Contains "6- ctx-after")
    check "grep long line truncated" (grepOut.Contains "...")

let fuzzyFallbackNotice () =
    let plainMatch = box {| relativePath = "b.ts"; lineNumber = 2; lineContent = "y" |}
    let state : FuzzyGrepState =
        { query = "q"; mode = "plain"; smartCase = true; beforeContext = 5; afterContext = 5
          pageSize = 50; externalBasePath = None }
    // Plain empty → no implicit fallback, returns empty.
    let rawEmpty = box {| ok = true; value = box {| items = [||]; totalMatched = 0; nextCursor = null |} |}
    let r = resolveResult rawEmpty
    check "no implicit fuzzy fallback" (r.matches.Length = 0)
    // Plain with matches → returns the matches.
    let rawPlain = box {| ok = true; value = box {| items = [| plainMatch |]; totalMatched = 1; nextCursor = null |} |}
    let r2 = resolveResult rawPlain
    check "plain matches returned" (r2.matches.Length = 1)

/// find paging uses totalMatched ?? 0 for the next-page decision — so an absent
/// totalMatched yields NO next iterator (mirrors find-output.ts).
let findPagingDefault () =
    let store = createTypedIteratorStore 10
    let opts : SearchOptions = { cwd = "."; scopeId = "scope"; store = Some store; finderCache = FinderCache() }
    let state : FuzzyFindState = { query = "q"; pageSize = 30; pageIndex = 0; externalBasePath = None }
    equal "no totalMatched → no iterator" "" (findNextIterator state store opts 0)
    let id = findNextIterator state store opts 100
    check "many matches → iterator stored" (id <> "")

let emptyIteratorNotRendered () =
    equal "empty iterator omitted" "body" (buildGrepOutput "body" None "")

/// characterization: lock the exact notice-block output of buildGrepOutput for
/// the combined (regex + iterator) and regex-only cases before refactoring.
let grepOutputNotices () =
    let combined = buildGrepOutput "body" (Some "bad regex") "iter1"
    check "combined regex notice in body" (combined.Contains "Invalid regex: bad regex")
    check "combined iterator in front matter" (combined.Contains "iterator: iter1")
    let regexOnly = buildGrepOutput "body" (Some "bad regex") ""
    check "regex-only notice in body" (regexOnly.Contains "Invalid regex: bad regex")
    check "regex-only no iterator key" (not (regexOnly.Contains "iterator:"))

/// totalMatched has three semantics, all guarded here with exact header lines:
///   Some n (n ≠ items.Length) — header uses n verbatim
///   Some 0                   — header is "0 matches" / "0 matching files" (not items.Length)
///   None                     — header falls back to items.Length
let totalMatchedSemantics () =
    let gm : GrepMatch =
        { relativePath = "a.ts"; lineNumber = 1; lineContent = "x"
          contextBefore = []; contextAfter = []; annotation = None }
    let fm : FindMatch = { relativePath = "a.ts"; annotation = None }
    let header (out: string) = out.Split('\n').[0]
    // Some n where n=5, items.Length=1 — header must use 5, not 1.
    let grep5 = formatGrepOutput (Some { items = [ gm ]; totalMatched = Some 5; regexFallbackError = None }) |> header
    equal "grep Some 5 header" "5 matches" grep5
    let find5 = formatFindOutput (Some { items = [ fm ]; totalMatched = Some 5; totalFiles = 2 }) |> header
    equal "find Some 5 header" "5 matching files (2 total indexed)" find5
    // Some 0 — must render 0, not fall back to items.Length=1.
    let grep0 = formatGrepOutput (Some { items = [ gm ]; totalMatched = Some 0; regexFallbackError = None }) |> header
    equal "grep Some 0 header" "0 matches" grep0
    let find0 = formatFindOutput (Some { items = [ fm ]; totalMatched = Some 0; totalFiles = 5 }) |> header
    equal "find Some 0 header" "0 matching files (5 total indexed)" find0
    // None — falls back to items.Length=1 (singular form).
    let grepNone = formatGrepOutput (Some { items = [ gm ]; totalMatched = None; regexFallbackError = None }) |> header
    equal "grep None header" "1 match" grepNone
    let findNone = formatFindOutput (Some { items = [ fm ]; totalMatched = None; totalFiles = 5 }) |> header
    equal "find None header" "1 matching file (5 total indexed)" findNone

/// P2-2: iterator namespaces must come from named module-level constants, not
/// magic strings sprinkled in the call sites.  The refactored FuzzySearch must
/// expose them so callers (and these tests) stop depending on literal "ffi_f"
/// / "ffi_i" duplication.
let iteratorNamespaceConstants () =
    equal "find namespace constant" "ffi_f" VibeFs.Shell.FuzzyIteratorStore.findIteratorNamespace
    equal "grep namespace constant" "ffi_i" VibeFs.Shell.FuzzyIteratorStore.grepIteratorNamespace

/// P2-2: the iterator store must be strongly typed per-state.  Storing a
/// FuzzyFindState under the find namespace and consuming it as a FuzzyGrepState
/// MUST fail (return None / raise) — that's the whole point of replacing the
/// `obj`-based store with one keyed by state type.
let iteratorStoreStronglyTyped () =
    let store = VibeFs.Shell.FuzzyIteratorStore.createTypedIteratorStore 10
    let findState : FuzzyFindState = { query = "q"; pageSize = 30; pageIndex = 0; externalBasePath = None }
    let grepCore : FuzzyGrepState =
        { query = "q"; mode = "plain"; smartCase = true; beforeContext = 0; afterContext = 0
          pageSize = 50; externalBasePath = None }
    let grepState = { core = grepCore; cursor = None }

    let findId = VibeFs.Shell.FuzzyIteratorStore.storeFindIterator store "scope" findState
    check "find id carries scope" (findId.Contains "scope")
    check "find id carries namespace" (findId.Contains VibeFs.Shell.FuzzyIteratorStore.findIteratorNamespace)

    let grepId = VibeFs.Shell.FuzzyIteratorStore.storeGrepIterator store "scope" grepState
    check "grep id carries namespace" (grepId.Contains VibeFs.Shell.FuzzyIteratorStore.grepIteratorNamespace)

    let resumed = VibeFs.Shell.FuzzyIteratorStore.consumeFindIterator store findId
    check "find resume" resumed.IsSome
    check "find single-use after typed consume" ((VibeFs.Shell.FuzzyIteratorStore.consumeFindIterator store findId).IsNone)

    // Cross-namespace consumption is a category error, not a silent miss.
    let crossed = VibeFs.Shell.FuzzyIteratorStore.consumeFindIterator store grepId
    check "cross-namespace consume returns None" crossed.IsNone

/// P2-2: fuzzyFind / fuzzyGrep must share a `runWithFinder` finder-acquisition
/// pipeline that releases external finders even on error paths.  We assert the
/// helper exists and that it dispatches release calls regardless of outcome.
let runWithFinderSharedPipeline () =
    let mutable released = 0
    let fakeFinder =
        { new VibeFs.Shell.FuzzyFinderShell.FinderLike with
            member _.fileSearch(_, _) = box null
            member _.grep(_, _) = box null
            member _.destroy() = released <- released + 1
            member _.isDestroyed = false }
    let outcome =
        VibeFs.Shell.FuzzySearch.runWithFinder
            (Ok fakeFinder)
            (Some "/external/path")
            (fun _ -> { output = "ok"; isError = false })
    equal "outcome propagated" "ok" outcome.output
    equal "external finder released exactly once" 1 released

    let mutable releasedOnError = 0
    let fakeFinder2 =
        { new VibeFs.Shell.FuzzyFinderShell.FinderLike with
            member _.fileSearch(_, _) = box null
            member _.grep(_, _) = box null
            member _.destroy() = releasedOnError <- releasedOnError + 1
            member _.isDestroyed = false }
    let mutable raised = false
    try
        VibeFs.Shell.FuzzySearch.runWithFinder
            (Ok fakeFinder2)
            (Some "/external/path")
            (fun _ -> failwith "boom")
        |> ignore
    with _ -> raised <- true
    check "exception bubbled" raised
    equal "external finder released even on throw" 1 releasedOnError

/// Empty-string iterator must be treated as "absent", not as a stored key.  An
/// LLM/client passing `iterator: ""` (instead of omitting the field) should
/// fall through to the fresh-search branch whenever `pattern` is present.
let emptyIteratorTreatedAsAbsent () =
    let store = VibeFs.Shell.FuzzyIteratorStore.createTypedIteratorStore 10
    let opts : SearchOptions = { cwd = "."; scopeId = "scope"; store = Some store; finderCache = FinderCache() }
    let params' : FuzzyFindParams = { pattern = Some "q"; path = None; limit = None; iterator = Some "" }
    match resolveFindSearchState params' opts with
    | Ok _ -> check "empty iterator falls through to fresh search" true
    | Error msg ->
        check ("empty iterator must not error: " + msg) false
    let storedId =
        VibeFs.Shell.FuzzyIteratorStore.storeFindIterator store "scope"
            { query = "q"; pageSize = 30; pageIndex = 0; externalBasePath = None }
    let resumed : FuzzyFindParams = { pattern = None; path = None; limit = None; iterator = Some storedId }
    match resolveFindSearchState resumed opts with
    | Ok _ -> check "stored iterator resumes" true
    | Error _ -> check "stored iterator resumes" false
    let bogus : FuzzyFindParams = { pattern = None; path = None; limit = None; iterator = Some "nope" }
    match resolveFindSearchState bogus opts with
    | Error _ -> check "unknown iterator still errors" true
    | Ok _ -> check "unknown iterator still errors" false
