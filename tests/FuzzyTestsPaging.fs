module Wanxiangshu.Tests.FuzzyTestsPaging

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.FuzzyPath
open Wanxiangshu.Kernel.FuzzyQuery
open Wanxiangshu.Kernel.FuzzyFormat
open Wanxiangshu.Shell.FuzzySearch
open Wanxiangshu.Shell.FuzzyIteratorStore
open Wanxiangshu.Kernel
open Wanxiangshu.Shell.FuzzyFinderShell

let findPagingDefault () =
    let store = createTypedIteratorStore 10
    let opts : SearchOptions = { cwd = "."; scopeId = "scope"; store = Some store; finderCache = FinderCache() }
    let state : FuzzyFindState = { query = "q"; pageSize = 30; pageIndex = 0; externalBasePath = None }
    equal "no totalMatched → no iterator" "" (findNextIterator state store opts 0)
    let id = findNextIterator state store opts 100
    check "many matches → iterator stored" (id <> "")

let emptyIteratorNotRendered () =
    equal "empty iterator omitted" "body" (buildGrepOutput "body" None "")

let grepOutputNotices () =
    let combined = buildGrepOutput "body" (Some "bad regex") "iter1"
    check "combined regex notice in body" (combined.Contains "Invalid regex: bad regex")
    check "combined iterator in front matter" (combined.Contains "iterator: iter1")
    let regexOnly = buildGrepOutput "body" (Some "bad regex") ""
    check "regex-only notice in body" (regexOnly.Contains "Invalid regex: bad regex")
    check "regex-only no iterator key" (not (regexOnly.Contains "iterator:"))

let totalMatchedSemantics () =
    let gm : GrepMatch =
        { relativePath = "a.ts"; lineNumber = 1; lineContent = "x"
          contextBefore = []; contextAfter = []; annotation = None }
    let fm : FindMatch = { relativePath = "a.ts"; annotation = None }
    let header (out: string) = out.Split('\n').[0]
    let grep5 = formatGrepOutput (Some { items = [ gm ]; totalMatched = Some 5; regexFallbackError = None }) |> header
    equal "grep Some 5 header" "5 matches" grep5
    let find5 = formatFindOutput (Some { items = [ fm ]; totalMatched = Some 5; totalFiles = 2 }) |> header
    equal "find Some 5 header" "5 matching files (2 total indexed)" find5
    let grep0 = formatGrepOutput (Some { items = [ gm ]; totalMatched = Some 0; regexFallbackError = None }) |> header
    equal "grep Some 0 header" "0 matches" grep0
    let find0 = formatFindOutput (Some { items = [ fm ]; totalMatched = Some 0; totalFiles = 5 }) |> header
    equal "find Some 0 header" "0 matching files (5 total indexed)" find0
    let grepNone = formatGrepOutput (Some { items = [ gm ]; totalMatched = None; regexFallbackError = None }) |> header
    equal "grep None header" "1 match" grepNone
    let findNone = formatFindOutput (Some { items = [ fm ]; totalMatched = None; totalFiles = 5 }) |> header
    equal "find None header" "1 matching file (5 total indexed)" findNone

let iteratorNamespaceConstants () =
    equal "find namespace constant" "ffi_f" Wanxiangshu.Shell.FuzzyIteratorStore.findIteratorNamespace
    equal "grep namespace constant" "ffi_i" Wanxiangshu.Shell.FuzzyIteratorStore.grepIteratorNamespace

let iteratorStoreStronglyTyped () =
    let store = Wanxiangshu.Shell.FuzzyIteratorStore.createTypedIteratorStore 10
    let findState : FuzzyFindState = { query = "q"; pageSize = 30; pageIndex = 0; externalBasePath = None }
    let grepCore : FuzzyGrepState =
        { query = "q"; mode = "plain"; smartCase = true; beforeContext = 0; afterContext = 0
          pageSize = 50; externalBasePath = None }
    let grepState = { core = grepCore; cursor = None }
    let findId = Wanxiangshu.Shell.FuzzyIteratorStore.storeFindIterator store "scope" findState
    check "find id carries scope" (findId.Contains "scope")
    check "find id carries namespace" (findId.Contains Wanxiangshu.Shell.FuzzyIteratorStore.findIteratorNamespace)
    let grepId = Wanxiangshu.Shell.FuzzyIteratorStore.storeGrepIterator store "scope" grepState
    check "grep id carries namespace" (grepId.Contains Wanxiangshu.Shell.FuzzyIteratorStore.grepIteratorNamespace)
    let resumed = Wanxiangshu.Shell.FuzzyIteratorStore.consumeFindIterator store findId
    check "find resume" resumed.IsSome
    check "find single-use after typed consume" ((Wanxiangshu.Shell.FuzzyIteratorStore.consumeFindIterator store findId).IsNone)
    let crossed = Wanxiangshu.Shell.FuzzyIteratorStore.consumeFindIterator store grepId
    check "cross-namespace consume returns None" crossed.IsNone

let runWithFinderSharedPipeline () =
    let mutable released = 0
    let fakeFinder =
        { new Wanxiangshu.Shell.FuzzyFinderShell.FinderLike with
            member _.fileSearch(_, _) = box null
            member _.grep(_, _) = box null
            member _.destroy() = released <- released + 1
            member _.isDestroyed = false }
    let outcome =
        Wanxiangshu.Shell.FuzzySearch.runWithFinder
            (Ok fakeFinder)
            (Some "/external/path")
            (fun _ -> { output = "ok"; isError = false })
    equal "outcome propagated" "ok" outcome.output
    equal "external finder released exactly once" 1 released
    let mutable releasedOnError = 0
    let fakeFinder2 =
        { new Wanxiangshu.Shell.FuzzyFinderShell.FinderLike with
            member _.fileSearch(_, _) = box null
            member _.grep(_, _) = box null
            member _.destroy() = releasedOnError <- releasedOnError + 1
            member _.isDestroyed = false }
    let mutable raised = false
    try
        Wanxiangshu.Shell.FuzzySearch.runWithFinder
            (Ok fakeFinder2)
            (Some "/external/path")
            (fun _ -> failwith "boom")
        |> ignore
    with _ -> raised <- true
    check "exception bubbled" raised
    equal "external finder released even on throw" 1 releasedOnError

let resolveStoreRequiresInjection () =
    let opts : SearchOptions = { cwd = "."; scopeId = "scope"; store = None; finderCache = FinderCache() }
    match resolveStore opts with
    | Error msg -> check "missing store message" (msg.Contains "SearchOptions.store")
    | Ok _ -> check "None store must not fall back to global default" false

let emptyIteratorTreatedAsAbsent () =
    let store = Wanxiangshu.Shell.FuzzyIteratorStore.createTypedIteratorStore 10
    let opts : SearchOptions = { cwd = "."; scopeId = "scope"; store = Some store; finderCache = FinderCache() }
    let params' : FuzzyFindParams = { pattern = [ "q" ]; path = None; limit = None; iterator = Some "" }
    match resolveFindSearchState params' opts with
    | Ok _ -> check "empty iterator falls through to fresh search" true
    | Error msg -> check ("empty iterator must not error: " + msg) false
    let storedId =
        Wanxiangshu.Shell.FuzzyIteratorStore.storeFindIterator store "scope"
            { query = "q"; pageSize = 30; pageIndex = 0; externalBasePath = None }
    let resumed : FuzzyFindParams = { pattern = []; path = None; limit = None; iterator = Some storedId }
    match resolveFindSearchState resumed opts with
    | Ok _ -> check "stored iterator resumes" true
    | Error _ -> check "stored iterator resumes" false
    let bogus : FuzzyFindParams = { pattern = []; path = None; limit = None; iterator = Some "nope" }
    match resolveFindSearchState bogus opts with
    | Error _ -> check "unknown iterator still errors" true
    | Ok _ -> check "unknown iterator still errors" false