module Wanxiangshu.Tests.FuzzyTestsPaging

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.FuzzyPath
open Wanxiangshu.Kernel.FuzzyQuery
open Wanxiangshu.Runtime.ToolOutputInfo
open Wanxiangshu.Kernel.FuzzyFormat
open Wanxiangshu.Runtime.FuzzySearch
open Wanxiangshu.Runtime.FuzzyIteratorStore
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime.FuzzyFinderShell

let findPagingDefault () =
    let store = createTypedIteratorStore 10

    let opts: SearchOptions =
        { cwd = "."
          scopeId = "scope"
          store = Some store
          finderCache = FinderCache() }

    let state: FuzzyFindState =
        { query = "q"
          pageSize = 30
          pageIndex = 0
          externalBasePath = None }

    equal "no totalMatched → no iterator" "" (findNextIterator state store opts 0)
    let id = findNextIterator state store opts 100
    check "many matches → iterator stored" (id <> "")

let emptyIteratorNotRendered () =
    equal "empty iterator omitted" "body" (buildGrepBody "body" None)

let grepOutputNotices () =
    let bodyOnly = buildGrepBody "body" (Some "bad regex")
    let combined = Wanxiangshu.Runtime.ToolOutputInfo.withIterator bodyOnly "iter1"
    check "combined regex notice in body" (combined.Contains "Invalid regex: bad regex")
    check "combined iterator in front matter" (combined.Contains "iterator: iter1")
    let regexOnly = buildGrepBody "body" (Some "bad regex")
    check "regex-only notice in body" (regexOnly.Contains "Invalid regex: bad regex")
    check "regex-only no iterator key" (not (regexOnly.Contains "iterator:"))

let totalMatchedSemantics () =
    let gm: GrepMatch =
        { relativePath = "a.ts"
          lineNumber = 1
          lineContent = "x"
          contextBefore = []
          contextAfter = []
          annotation = None }

    let fm: FindMatch =
        { relativePath = "a.ts"
          annotation = None }

    let header (out: string) = out.Split('\n').[0]

    let grep5 =
        formatGrepOutput (
            Some
                { items = [ gm ]
                  totalMatched = Some 5
                  regexFallbackError = None }
        )
        |> header

    equal "grep Some 5 header" "5 matches" grep5

    let find5 =
        formatFindOutput (
            Some
                { items = [ fm ]
                  totalMatched = Some 5
                  totalFiles = 2 }
        )
        |> header

    equal "find Some 5 header" "5 matching files (2 total indexed)" find5

    let grep0 =
        formatGrepOutput (
            Some
                { items = [ gm ]
                  totalMatched = Some 0
                  regexFallbackError = None }
        )
        |> header

    equal "grep Some 0 header" "0 matches" grep0

    let find0 =
        formatFindOutput (
            Some
                { items = [ fm ]
                  totalMatched = Some 0
                  totalFiles = 5 }
        )
        |> header

    equal "find Some 0 header" "0 matching files (5 total indexed)" find0

    let grepNone =
        formatGrepOutput (
            Some
                { items = [ gm ]
                  totalMatched = None
                  regexFallbackError = None }
        )
        |> header

    equal "grep None header" "1 match" grepNone

    let findNone =
        formatFindOutput (
            Some
                { items = [ fm ]
                  totalMatched = None
                  totalFiles = 5 }
        )
        |> header

    equal "find None header" "1 matching file (5 total indexed)" findNone

let iteratorNamespaceConstants () =
    equal "find namespace constant" "ffi_f" Wanxiangshu.Runtime.FuzzyIteratorStore.findIteratorNamespace
    equal "grep namespace constant" "ffi_i" Wanxiangshu.Runtime.FuzzyIteratorStore.grepIteratorNamespace

let iteratorStoreStronglyTyped () =
    let store = Wanxiangshu.Runtime.FuzzyIteratorStore.createTypedIteratorStore 10

    let findState: FuzzyFindState =
        { query = "q"
          pageSize = 30
          pageIndex = 0
          externalBasePath = None }

    let grepCore: FuzzyGrepState =
        { query = "q"
          mode = "plain"
          smartCase = true
          beforeContext = 0
          afterContext = 0
          pageSize = 50
          externalBasePath = None }

    let grepState = { core = grepCore; cursor = None }

    let findId =
        Wanxiangshu.Runtime.FuzzyIteratorStore.storeFindIterator store "scope" findState

    check "find id carries scope" (findId.Contains "scope")
    check "find id carries namespace" (findId.Contains Wanxiangshu.Runtime.FuzzyIteratorStore.findIteratorNamespace)

    let grepId =
        Wanxiangshu.Runtime.FuzzyIteratorStore.storeGrepIterator store "scope" grepState

    check "grep id carries namespace" (grepId.Contains Wanxiangshu.Runtime.FuzzyIteratorStore.grepIteratorNamespace)

    let resumed =
        Wanxiangshu.Runtime.FuzzyIteratorStore.consumeFindIterator store findId

    check "find resume" resumed.IsSome

    check
        "find single-use after typed consume"
        ((Wanxiangshu.Runtime.FuzzyIteratorStore.consumeFindIterator store findId).IsNone)

    let crossed =
        Wanxiangshu.Runtime.FuzzyIteratorStore.consumeFindIterator store grepId

    check "cross-namespace consume returns None" crossed.IsNone

let runWithFinderSharedPipeline () =
    let mutable released = 0

    let fakeFinder =
        { new Wanxiangshu.Runtime.FuzzyFinderShell.FinderLike with
            member _.fileSearch(_, _) = box null
            member _.grep(_, _) = box null
            member _.destroy() = released <- released + 1
            member _.isDestroyed = false }

    let outcome =
        Wanxiangshu.Runtime.FuzzySearch.runWithFinder (Ok fakeFinder) (Some "/external/path") (fun _ ->
            { output = "ok"; isError = false })

    equal "outcome propagated" "ok" outcome.output
    equal "external finder released exactly once" 1 released
    let mutable releasedOnError = 0

    let fakeFinder2 =
        { new Wanxiangshu.Runtime.FuzzyFinderShell.FinderLike with
            member _.fileSearch(_, _) = box null
            member _.grep(_, _) = box null
            member _.destroy() = releasedOnError <- releasedOnError + 1
            member _.isDestroyed = false }

    let mutable raised = false

    try
        Wanxiangshu.Runtime.FuzzySearch.runWithFinder (Ok fakeFinder2) (Some "/external/path") (fun _ ->
            failwith "boom")
        |> ignore
    with _ ->
        raised <- true

    check "exception bubbled" raised
    equal "external finder released even on throw" 1 releasedOnError

let resolveStoreRequiresInjection () =
    let opts: SearchOptions =
        { cwd = "."
          scopeId = "scope"
          store = None
          finderCache = FinderCache() }

    match resolveStore opts with
    | Error msg -> check "missing store message" (msg.Contains "SearchOptions.store")
    | Ok _ -> check "None store must not fall back to global default" false

let emptyIteratorTreatedAsAbsent () =
    let store = Wanxiangshu.Runtime.FuzzyIteratorStore.createTypedIteratorStore 10

    let opts: SearchOptions =
        { cwd = "."
          scopeId = "scope"
          store = Some store
          finderCache = FinderCache() }

    let params': FuzzyFindParams =
        { pattern = [ "q" ]
          path = None
          limit = None }

    match resolveFindSearchState params' opts with
    | Ok _ -> check "fresh search without iterator succeeds" true
    | Error msg -> check ("fresh search must not error: " + msg) false

    let storedId =
        Wanxiangshu.Runtime.FuzzyIteratorStore.storeFindIterator
            store
            "scope"
            { query = "q"
              pageSize = 30
              pageIndex = 0
              externalBasePath = None }

    match Wanxiangshu.Runtime.FuzzyIteratorStore.consumeFindIterator store storedId with
    | Some _ -> check "stored iterator resumes" true
    | None -> check "stored iterator resumes" false

    match Wanxiangshu.Runtime.FuzzyIteratorStore.consumeFindIterator store "nope" with
    | None -> check "unknown iterator returns None" true
    | Some _ -> check "unknown iterator returns None" false
