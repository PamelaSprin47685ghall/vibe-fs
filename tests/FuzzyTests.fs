module Wanxiangshu.Tests.FuzzyTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.FuzzyPath
open Wanxiangshu.Kernel.FuzzyQuery
open Wanxiangshu.Kernel.FuzzyFormat
open Wanxiangshu.Shell.FuzzySearch
open Wanxiangshu.Shell.FuzzyIteratorStore
open Wanxiangshu.Kernel
open Wanxiangshu.Shell.FuzzyFinderShell
open Wanxiangshu.Shell
open Fable.Core
open Fable.Core.JsInterop

[<Emit("process.env[$0]")>]
let private getEnv (key: string) : string = jsNative

[<Emit("process.env[$0] = $1")>]
let private setEnv (key: string) (value: string) : unit = jsNative

let grepDetect () =
    equal "plain word" "plain" (detectGrepMode "foo")
    equal "plain sentence" "plain" (detectGrepMode "foo bar")
    equal "dot star regex" "regex" (detectGrepMode "foo.*bar")
    equal "alternation regex" "regex" (detectGrepMode "a|b")
    check "wildcard .* declined" (checkWildcardOnly ".*" "regex")
    check "wildcard . declined" (checkWildcardOnly "." "regex")
    check "concrete not wildcard" (not (checkWildcardOnly "getUserById" "plain"))

let iteratorRoundTrip () =
    let store = createTypedIteratorStore 10

    let state: FuzzyFindState =
        { query = "my query"
          pageSize = 30
          pageIndex = 2
          externalBasePath = None }

    let id = storeFindIterator store "scope" state
    let resumed = consumeFindIterator store id
    check "resume present" resumed.IsSome
    equal "query survives" "my query" resumed.Value.query
    equal "pageIndex survives" 2 resumed.Value.pageIndex
    check "single-use" ((consumeFindIterator store id).IsNone)

let finderConversion () =
    let mockFinder =
        box
            {| fileSearch =
                (fun _ _ ->
                    box
                        {| ok = true
                           value =
                            box
                                {| items = [||]
                                   totalMatched = 0
                                   totalFiles = 0 |} |}) |}

    let okResult = resultFromRaw (box {| ok = true; value = mockFinder |})

    check
        "ok → Ok"
        (match okResult with
         | Ok _ -> true
         | _ -> false)

    let errResult = resultFromRaw (box {| ok = false; error = "scan failed" |})

    check
        "err → Error"
        (match errResult with
         | Error _ -> true
         | _ -> false)

    equal
        "err message"
        "scan failed"
        (match errResult with
         | Error m -> m
         | _ -> "")

    let noErr = resultFromRaw (box {| ok = false |})

    equal
        "undefined error fallback"
        "createFinder failed"
        (match noErr with
         | Error m -> m
         | _ -> "")

let formatFull () =
    let hot: FileAnnotation =
        { gitStatus = None
          totalFrecencyScore = Some 30
          accessFrecencyScore = None }

    let findOut =
        formatFindOutput (
            Some
                { items =
                    [ { relativePath = "src/hot.ts"
                        annotation = Some hot } ]
                  totalMatched = Some 1
                  totalFiles = 42 }
        )

    check "find total indexed" (findOut.Contains "(42 total indexed)")
    check "find frecency annotation" (findOut.Contains "VERY often touched file")
    let longLine = System.String('x', 600)

    let gm: GrepMatch =
        { relativePath = "a.ts"
          lineNumber = 5
          lineContent = longLine
          contextBefore = [ "ctx-before" ]
          contextAfter = [ "ctx-after" ]
          annotation = None }

    let grepOut =
        formatGrepOutput (
            Some
                { items = [ gm ]
                  totalMatched = Some 1
                  regexFallbackError = None }
        )

    check "grep context-before" (grepOut.Contains "4- ctx-before")
    check "grep context-after" (grepOut.Contains "6- ctx-after")
    check "grep long line truncated" (grepOut.Contains "...")

let fuzzyFallbackNotice () =
    let plainMatch =
        box
            {| relativePath = "b.ts"
               lineNumber = 2
               lineContent = "y" |}

    let state: FuzzyGrepState =
        { query = "q"
          mode = "plain"
          smartCase = true
          beforeContext = 5
          afterContext = 5
          pageSize = 50
          externalBasePath = None }

    let rawEmpty =
        box
            {| ok = true
               value =
                box
                    {| items = [||]
                       totalMatched = 0
                       nextCursor = null |} |}

    let r = resolveResult rawEmpty
    check "no implicit fuzzy fallback" (r.matches.Length = 0)

    let rawPlain =
        box
            {| ok = true
               value =
                box
                    {| items = [| plainMatch |]
                       totalMatched = 1
                       nextCursor = null |} |}

    let r2 = resolveResult rawPlain
    check "plain matches returned" (r2.matches.Length = 1)

let scanTimeoutConfigurable () =
    let orig = getEnv "WANXIANGSHU_SCAN_TIMEOUT"

    try
        setEnv "WANXIANGSHU_SCAN_TIMEOUT" "5000"
        equal "valid env timeout" 5000 (getScanTimeout ())
        setEnv "WANXIANGSHU_SCAN_TIMEOUT" "not-a-number"
        equal "invalid env falls back to default" 15000 (getScanTimeout ())
        setEnv "WANXIANGSHU_SCAN_TIMEOUT" ""
        equal "empty env falls back to default" 15000 (getScanTimeout ())
    finally
        setEnv "WANXIANGSHU_SCAN_TIMEOUT" orig

let iteratorCounterUniqueness () =
    let store = createTypedIteratorStore 2000

    let ids =
        [ for _ in 1..1000 ->
              storeFindIterator
                  store
                  "scope"
                  { query = "q"
                    pageSize = 30
                    pageIndex = 0
                    externalBasePath = None } ]

    let distinct = ids |> List.distinct |> List.length
    equal "1000 iterator ids are unique" 1000 distinct

let run () =
    grepDetect ()
    iteratorRoundTrip ()
    finderConversion ()
    formatFull ()
    fuzzyFallbackNotice ()
    scanTimeoutConfigurable ()
    iteratorCounterUniqueness ()
