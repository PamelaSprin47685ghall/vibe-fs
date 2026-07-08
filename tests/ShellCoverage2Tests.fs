module Wanxiangshu.Tests.ShellCoverage2Tests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Shell.FuzzyFinderShell
open Wanxiangshu.Shell.FuzzySearchGrep
open Wanxiangshu.Shell.FuzzySearchHelpers
open Wanxiangshu.Shell.FuzzyIteratorStore
open Wanxiangshu.Kernel.FuzzyQuery
open Wanxiangshu.Kernel.FuzzyPath
open Wanxiangshu.Shell.RuntimeScope

module Livelock = Wanxiangshu.Shell.LivelockGuard
module Dyn = Wanxiangshu.Shell.Dyn

// ── FuzzyFinderShell.resultFromRaw ──────────────────────────────────────────

let resultFromRawOk () =
    let fakeObj =
        createObj
            [ "fileSearch", box (fun () -> ())
              "grep", box (fun () -> ())
              "destroy", box (fun () -> ())
              "isDestroyed", box false ]

    let raw = createObj [ "ok", box true; "value", box fakeObj ]

    match resultFromRaw raw with
    | Ok _ -> check "resultFromRaw ok=true → Ok" true
    | Error _ -> check "resultFromRaw ok=true → Ok" false

let resultFromRawErrMsg () =
    let raw = createObj [ "ok", box false; "error", box "boom" ]

    match resultFromRaw raw with
    | Error msg -> equal "resultFromRaw err msg" "boom" msg
    | Ok _ -> check "resultFromRaw err msg" false

let resultFromRawErrNull () =
    let raw = createObj [ "ok", box false; "error", box null ]

    match resultFromRaw raw with
    | Error msg -> equal "resultFromRaw err null" "createFinder failed" msg
    | Ok _ -> check "resultFromRaw err null" false

let resultFromRawErrMissing () =
    let raw = createObj [ "ok", box false ]

    match resultFromRaw raw with
    | Error msg -> equal "resultFromRaw err missing" "createFinder failed" msg
    | Ok _ -> check "resultFromRaw err missing" false

// ── FinderCache ──────────────────────────────────────────────────────────────

let finderCacheDestroyMissing () =
    let cache = FinderCache()
    cache.Destroy "/no/such/path" |> ignore
    check "destroy missing path no throw" true

let finderCacheDestroyAll () =
    let cache = FinderCache()
    cache.DestroyAll() |> ignore
    check "destroyAll no throw" true

// ── resolveGrepIteratorState ─────────────────────────────────────────────────

let grepStateEmptyPattern () =
    let store = createTypedIteratorStore 100

    let opts: SearchOptions =
        { cwd = "/tmp"
          scopeId = "s1"
          store = Some store
          finderCache = FinderCache() }

    let params': FuzzyGrepParams =
        { pattern = []
          path = None
          exclude = []
          searchIgnored = None
          caseSensitive = None
          context = None
          limit = None
          iterator = None }

    match resolveGrepIteratorState params' opts with
    | Error msg -> equal "empty pattern error" "pattern is required on the first call" msg
    | Ok _ -> check "empty pattern → Error" false

let grepStateWildcardOnly () =
    let store = createTypedIteratorStore 100

    let opts: SearchOptions =
        { cwd = "/tmp"
          scopeId = "s1"
          store = Some store
          finderCache = FinderCache() }

    let params': FuzzyGrepParams =
        { pattern = [ "*" ]
          path = None
          exclude = []
          searchIgnored = None
          caseSensitive = None
          context = None
          limit = None
          iterator = None }

    match resolveGrepIteratorState params' opts with
    | Error msg -> check "wildcard error mentions matches everything" (msg.Contains "matches everything")
    | Ok _ -> check "wildcard only → Error" false

let grepStateNoStore () =
    let opts: SearchOptions =
        { cwd = "/tmp"
          scopeId = "s1"
          store = None
          finderCache = FinderCache() }

    let params': FuzzyGrepParams =
        { pattern = [ "foo" ]
          path = None
          exclude = []
          searchIgnored = None
          caseSensitive = None
          context = None
          limit = None
          iterator = None }

    match resolveGrepIteratorState params' opts with
    | Error msg ->
        equal
            "no store error"
            "FuzzySearch requires SearchOptions.store; inject RuntimeScope.IteratorStore from the tool registration path"
            msg
    | Ok _ -> check "no store → Error" false

let grepStateValid () =
    let store = createTypedIteratorStore 100

    let opts: SearchOptions =
        { cwd = "/tmp"
          scopeId = "s1"
          store = Some store
          finderCache = FinderCache() }

    let params': FuzzyGrepParams =
        { pattern = [ "let" ]
          path = None
          exclude = []
          searchIgnored = None
          caseSensitive = Some false
          context = Some 2
          limit = Some 30
          iterator = None }

    match resolveGrepIteratorState params' opts with
    | Ok state ->
        equal "query present" true (state.core.query <> "")
        equal "mode" "plain" state.core.mode
        equal "smartCase" true state.core.smartCase
        equal "pageSize" 30 state.core.pageSize
        check "cursor None" (state.cursor = None)
    | Error msg -> check "valid → Ok" false

// ── resolveResult ────────────────────────────────────────────────────────────

let resolveResultFields () =
    let item1 =
        createObj
            [ "relativePath", box "a.fs"
              "lineNumber", box 1
              "lineContent", box "let x = 1"
              "contextBefore", box ([||]: obj array)
              "contextAfter", box ([||]: obj array) ]

    let item2 =
        createObj
            [ "relativePath", box "b.fs"
              "lineNumber", box 3
              "lineContent", box "let y = 2"
              "contextBefore", box ([||]: obj array)
              "contextAfter", box ([||]: obj array) ]

    let value =
        createObj
            [ "items", box [| item1; item2 |]
              "totalMatched", box 2
              "regexFallbackError", box null
              "nextCursor", box "iter-1" ]

    let raw = createObj [ "value", box value ]
    let resolved = resolveResult raw
    equal "matches count" 2 resolved.matches.Length
    equal "total" (Some 2) resolved.total
    equal "regexError" None resolved.regexError
    equal "cursor" "iter-1" (string resolved.cursor)

// ── FuzzySearchHelpers ───────────────────────────────────────────────────────

let optStrSome () =
    let o = createObj [ "key", box "hello" ]
    equal "optStr Some" (Some "hello") (optStr o "key")

let optStrNone () =
    let o = createObj []
    equal "optStr None" None (optStr o "missing")

let optIntSome () =
    let o = createObj [ "n", box 42 ]
    equal "optInt Some" (Some 42) (optInt o "n")

let optIntNone () =
    let o = createObj []
    equal "optInt None" None (optInt o "n")

let stringListOfArray () =
    let o = createObj [ "v", box [| "a"; "b" |] ]
    equal "stringListOf array" [ "a"; "b" ] (stringListOf o "v")

let stringListOfEmpty () =
    let o = createObj []
    equal "stringListOf empty" [] (stringListOf o "v")

let itemsOfNormal () =
    let arr = [| createObj [ "path", box "x" ] |]
    let value = createObj [ "items", box arr ]
    equal "itemsOf length" 1 ((itemsOf value).Length)

let itemsOfEmpty () =
    let value = createObj []
    equal "itemsOf no key empty" 0 ((itemsOf value).Length)

let errorMsgWithError () =
    let raw = createObj [ "error", box "fail" ]
    equal "errorMsg with error" "fail" (errorMsg raw "fallback")

let errorMsgNoError () =
    let raw = createObj []
    equal "errorMsg no error" "fallback" (errorMsg raw "fallback")

let resolveStoreSome () =
    let store = createTypedIteratorStore 10

    let opts: SearchOptions =
        { cwd = "/"
          scopeId = "g"
          store = Some store
          finderCache = FinderCache() }

    match resolveStore opts with
    | Ok s -> check "resolveStore Some ok" true
    | Error _ -> check "resolveStore Some ok" false

let resolveStoreNone () =
    let opts: SearchOptions =
        { cwd = "/"
          scopeId = "g"
          store = None
          finderCache = FinderCache() }

    match resolveStore opts with
    | Error _ -> check "resolveStore None → Error" true
    | Ok _ -> check "resolveStore None → Error" false

// ── LivelockGuard ─────────────────────────────────────────────────────────────

let livelockGuardFirstCall () =
    let testScope = RuntimeScope()
    check "first call not blocked" (not (Livelock.check testScope "s1" "c" "a" "o"))

let livelockGuardSameIncrement () =
    let testScope = RuntimeScope()
    check "same tool" (not (Livelock.check testScope "s9" "c" "a" "o"))
    check "repeat counts" (not (Livelock.check testScope "s9" "c" "a" "o"))

let livelockGuardBreach () =
    let testScope = RuntimeScope()
    check "1st" (not (Livelock.check testScope "s2" "c" "a" "o"))
    check "2nd" (not (Livelock.check testScope "s2" "c" "a" "o"))
    check "3rd breach" (Livelock.check testScope "s2" "c" "a" "o")

let livelockGuardDifferentResets () =
    let testScope = RuntimeScope()
    check "s3 baseline" (not (Livelock.check testScope "s3" "c" "a" "o"))
    check "s3 repeat" (not (Livelock.check testScope "s3" "c" "a" "o"))
    check "s3 different output breaks" (not (Livelock.check testScope "s3" "c" "a" "x"))

let run () =
    resultFromRawOk ()
    resultFromRawErrMsg ()
    resultFromRawErrNull ()
    resultFromRawErrMissing ()
    finderCacheDestroyMissing ()
    finderCacheDestroyAll ()
    grepStateEmptyPattern ()
    grepStateWildcardOnly ()
    grepStateNoStore ()
    grepStateValid ()
    resolveResultFields ()
    optStrSome ()
    optStrNone ()
    optIntSome ()
    optIntNone ()
    stringListOfArray ()
    stringListOfEmpty ()
    itemsOfNormal ()
    itemsOfEmpty ()
    errorMsgWithError ()
    errorMsgNoError ()
    resolveStoreSome ()
    resolveStoreNone ()
    livelockGuardFirstCall ()
    livelockGuardSameIncrement ()
    livelockGuardBreach ()
    livelockGuardDifferentResets ()
