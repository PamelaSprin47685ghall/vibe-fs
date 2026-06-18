module VibeFs.Tests.FuzzyTests

open VibeFs.Tests.Assert
open VibeFs.Kernel.FuzzyQuery
open VibeFs.Kernel.FuzzyFormat
open VibeFs.Kernel.FuzzyGrepDetect
open VibeFs.Shell.IteratorStore
open VibeFs.Kernel
open VibeFs.Shell.FuzzyFinderShell
open VibeFs.Shell.FuzzyCoordinator
open VibeFs.Shell.FuzzyCommands

let grepDetect () =
    equal "plain word" "plain" (detectGrepMode "foo")
    equal "plain sentence" "plain" (detectGrepMode "foo bar")
    equal "dot star regex" "regex" (detectGrepMode "foo.*bar")
    equal "alternation regex" "regex" (detectGrepMode "a|b")
    check "wildcard .* rejected" (checkWildcardOnly ".*" "regex")
    check "wildcard . rejected" (checkWildcardOnly "." "regex")
    check "concrete not wildcard" (not (checkWildcardOnly "getUserById" "plain"))

let iteratorRoundTrip () =
    let store = createIteratorStore 10
    let state : FuzzyFindState = { query = "my query"; pageSize = 30; pageIndex = 2; externalBasePath = None }
    let id = storeIterator<FuzzyFindState> store "scope" "ffi_f" state
    let resumed = consumeIterator<FuzzyFindState> store id
    check "resume present" resumed.IsSome
    equal "query survives" "my query" resumed.Value.query
    equal "pageIndex survives" 2 resumed.Value.pageIndex
    check "single-use" ((consumeIterator<FuzzyFindState> store id).IsNone)

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
          pageSize = 50; externalBasePath = None; cursor = None }
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
    let store = createIteratorStore 10
    let opts : SearchOptions = { cwd = "."; scopeId = "scope"; store = Some store; finderCache = FinderCache() }
    let state : FuzzyFindState = { query = "q"; pageSize = 30; pageIndex = 0; externalBasePath = None }
    // Absent totalMatched → default 0 → no next page iterator.
    equal "no totalMatched → no iterator" "" (findNextIterator state store opts 0)
    // Plenty of matches → iterator stored (non-empty id).
    let id = findNextIterator state store opts 100
    check "many matches → iterator stored" (id <> "")

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
