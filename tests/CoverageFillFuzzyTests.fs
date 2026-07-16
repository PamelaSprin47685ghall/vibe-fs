module Wanxiangshu.Tests.CoverageFillFuzzyTests

open Fable.Core
open Fable.Core.JsInterop
open System
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.FuzzySearchSupport
open Wanxiangshu.Runtime.FuzzySearchFind
open Wanxiangshu.Runtime.FuzzySearchGrep
open Wanxiangshu.Runtime.FuzzyIteratorStore
open Wanxiangshu.Kernel.FuzzyQuery

// ── Shell.FuzzySearchHelpers ───────────────────────────────────────────────

let fshParseExclude () =
    let args1 = createObj [ "exclude", box [| box "node_modules" |] ]
    equal "array exclude" [ "node_modules" ] (parseExcludeField args1)
    let args2 = createObj [ "exclude", box "dist" ]
    equal "string exclude" [ "dist" ] (parseExcludeField args2)
    let args3 = createObj []
    equal "null exclude" [] (parseExcludeField args3)

let fshResolveStore () =
    let opts =
        { cwd = "."
          scopeId = "s"
          store = None
          finderCache = null }

    match resolveStore opts with
    | Error msg -> check "no store error" (msg.Contains "store")
    | Ok _ -> check "no store error" false

let fshRunWithFinder () =
    let errResult = Error "finder failed"
    let r = runWithFinder errResult None (fun _ -> { output = "ok"; isError = false })
    check "error outcome" r.isError
    equal "error output" "finder failed" r.output

let fshItemsOf () =
    let v =
        createObj [ "items", box [| box (createObj [ "a", box 1 ]); box (createObj [ "a", box 2 ]) |] ]

    let items = itemsOf v
    equal "items count" 2 items.Length
    check "nullish→empty" (itemsOf null = [||])
    check "no items→empty" (itemsOf (createObj []) = [||])

let fshStringListOf () =
    let o = createObj [ "tags", box [| box "a"; box "b"; box "c" |] ]
    equal "string list" [ "a"; "b"; "c" ] (stringListOf o "tags")
    equal "missing key" [] (stringListOf o "missing")
    equal "not array" [] (stringListOf (createObj [ "tags", box "not-array" ]) "tags")

let fshAnnotationOf () =
    let item =
        createObj
            [ "gitStatus", box "M"
              "totalFrecencyScore", box 42
              "accessFrecencyScore", box 10 ]

    match annotationOf item with
    | Some a ->
        equal "git" (Some "M") a.gitStatus
        equal "total" (Some 42) a.totalFrecencyScore
    | None -> check "annotation present" false

    let bare = createObj [ "path", box "x" ]
    check "bare no annotation" (annotationOf bare = None)

let fshToFindMatch () =
    let item = createObj [ "relativePath", box "src/a.fs"; "gitStatus", box "M" ]
    let m = toFindMatch item
    equal "path" "src/a.fs" m.relativePath
    check "annotation present" m.annotation.IsSome

let fshToGrepMatch () =
    let item =
        createObj
            [ "relativePath", box "src/a.fs"
              "lineNumber", box 5
              "lineContent", box "fn foo()"
              "contextBefore", box [| box "ctx1" |]
              "contextAfter", box [| box "ctx2" |] ]

    let m = toGrepMatch item
    equal "grep path" "src/a.fs" m.relativePath
    equal "line" 5 m.lineNumber
    equal "content" "fn foo()" m.lineContent
    equal "ctx before" [ "ctx1" ] m.contextBefore
    equal "ctx after" [ "ctx2" ] m.contextAfter

let fshErrorMsg () =
    let withErr = createObj [ "error", box "scan failed" ]
    equal "with error" "scan failed" (errorMsg withErr "fallback")
    let noErr = createObj []
    equal "no error" "fallback" (errorMsg noErr "fallback")
    equal "nullish" "fallback" (errorMsg null "fallback")


let run () =
    fshParseExclude ()
    fshResolveStore ()
    fshRunWithFinder ()
    fshItemsOf ()
    fshStringListOf ()
    fshAnnotationOf ()
    fshToFindMatch ()
    fshToGrepMatch ()
    fshErrorMsg ()
