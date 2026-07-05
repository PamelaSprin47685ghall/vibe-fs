module Wanxiangshu.Tests.FuzzyToolsCodecTests

open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Shell.FuzzyToolsCodec

let private okFind args =
    match decodeFuzzyFindArgs args with
    | Ok p -> p
    | Error _ -> failwith "decodeFuzzyFindArgs expected Ok"

let private okGrep args =
    match decodeFuzzyGrepArgs args with
    | Ok p -> p
    | Error _ -> failwith "decodeFuzzyGrepArgs expected Ok"

let decodeFindOkFull () =
    let args =
        createObj [
            "pattern", box [| "foo" |]
            "path", box "src/"
            "limit", box 10
            "iterator", box "it-1"
        ]
    let p = okFind args
    check "find pattern" (p.pattern = [ "foo" ])
    check "find path" (p.path = Some "src/")
    check "find limit" (p.limit = Some 10)
    check "find iterator" (p.iterator = Some "it-1")

let decodeFindMissingPatternErrors () =
    let args = createObj [ "path", box "lib" ]
    match decodeFuzzyFindArgs args with
    | Error (InvalidIntent ("fuzzy_find", "pattern", "pattern is required on the first call")) ->
        check "find missing pattern error" true
    | _ -> check "find missing pattern error" false

let decodeFindLimitBelowOneErrors () =
    let args = createObj [ "pattern", box [| "x" |]; "limit", box 0 ]
    match decodeFuzzyFindArgs args with
    | Error (InvalidIntent ("fuzzy_find", "limit", "must be >= 1")) -> check "find limit zero" true
    | _ -> check "find limit zero" false

let decodeFindIteratorOnlyResume () =
    let args = createObj [ "iterator", box "resume-id" ]
    let p = okFind args
    check "find resume pattern absent" (p.pattern = [])
    check "find resume iterator" (p.iterator = Some "resume-id")

let decodeGrepOkWithExcludeArray () =
    let args =
        createObj [
            "pattern", box [| "needle" |]
            "exclude", box [| "test/"; "*.min.js" |]
            "searchIgnored", box true
            "caseSensitive", box true
            "context", box 2
            "limit", box 25
        ]
    let p = okGrep args
    check "grep pattern" (p.pattern = [ "needle" ])
    check "grep exclude len" (p.exclude.Length = 2)
    check "grep exclude head" (p.exclude.[0] = "test/")
    check "grep searchIgnored" (p.searchIgnored = Some true)
    check "grep caseSensitive" (p.caseSensitive = Some true)
    check "grep context" (p.context = Some 2)
    check "grep limit" (p.limit = Some 25)

let decodeGrepExcludeScalar () =
    let args = createObj [ "pattern", box [| "x" |]; "exclude", box "vendor/" ]
    let p = okGrep args
    check "grep scalar exclude" (p.exclude = [ "vendor/" ])

let decodeGrepMissingPatternErrors () =
    let args = createObj [ "path", box "src" ]
    match decodeFuzzyGrepArgs args with
    | Error (InvalidIntent ("fuzzy_grep", "pattern", "pattern is required on the first call")) ->
        check "grep missing pattern error" true
    | _ -> check "grep missing pattern error" false

let decodeGrepLimitBelowOneErrors () =
    let args = createObj [ "pattern", box [| "x" |]; "limit", box -1 ]
    match decodeFuzzyGrepArgs args with
    | Error (InvalidIntent ("fuzzy_grep", "limit", "must be >= 1")) -> check "grep limit negative" true
    | _ -> check "grep limit negative" false

let decodeGrepIteratorOnlyResume () =
    let args = createObj [ "iterator", box "g-iter" ]
    let p = okGrep args
    check "grep missing pattern" (p.pattern = [])
    check "grep iterator resume" (p.iterator = Some "g-iter")
    check "grep exclude empty" (p.exclude = [])

let decodeGrepPatternArray () =
    let args =
        createObj [
            "pattern", box [| "word1"; "word2" |]
        ]
    let p = okGrep args
    check "grep array pattern length" (p.pattern.Length = 2)
    check "grep array pattern first" (p.pattern.[0] = "word1")
    check "grep array pattern second" (p.pattern.[1] = "word2")

let decodeFindPatternArray () =
    let args =
        createObj [
            "pattern", box [| "foo"; "bar"; "baz" |]
        ]
    let p = okFind args
    check "find array pattern length" (p.pattern.Length = 3)
    check "find array pattern first" (p.pattern.[0] = "foo")
    check "find array pattern second" (p.pattern.[1] = "bar")
    check "find array pattern third" (p.pattern.[2] = "baz")

let decodeGrepPatternScalarStringFails () =
    let args = createObj [ "pattern", box "single_string" ]
    match decodeFuzzyGrepArgs args with
    | Error (InvalidIntent ("fuzzy_grep", "pattern", "pattern must be an array of strings")) ->
        check "grep pattern scalar string fails" true
    | _ -> check "grep pattern scalar string fails" false

let decodeFindPatternScalarStringFails () =
    let args = createObj [ "pattern", box "single_string" ]
    match decodeFuzzyFindArgs args with
    | Error (InvalidIntent ("fuzzy_find", "pattern", "pattern must be an array of strings")) ->
        check "find pattern scalar string fails" true
    | _ -> check "find pattern scalar string fails" false

let run () =
    decodeFindOkFull ()
    decodeFindMissingPatternErrors ()
    decodeFindLimitBelowOneErrors ()
    decodeFindIteratorOnlyResume ()
    decodeGrepOkWithExcludeArray ()
    decodeGrepExcludeScalar ()
    decodeGrepMissingPatternErrors ()
    decodeGrepLimitBelowOneErrors ()
    decodeGrepIteratorOnlyResume ()
    decodeGrepPatternArray ()
    decodeFindPatternArray ()
    decodeGrepPatternScalarStringFails ()
    decodeFindPatternScalarStringFails ()
