module Wanxiangshu.Tests.FuzzyToolsCodecTests

open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Runtime.FuzzyToolsCodec

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
        createObj [ "pattern", box [| "foo" |]; "path", box "src/"; "limit", box 10 ]

    let p = okFind args
    check "find pattern" (p.pattern = [ "foo" ])
    check "find path" (p.path = Some "src/")
    check "find limit" (p.limit = Some 10)

let decodeFindMissingPatternErrors () =
    let args = createObj [ "path", box "lib" ]

    match decodeFuzzyFindArgs args with
    | Error(InvalidIntent("fuzzy_find", "pattern", msg)) when msg.Contains("pattern is required") ->
        check "find missing pattern error" true
    | _ -> check "find missing pattern error" false

let decodeFindLimitBelowOneErrors () =
    let args = createObj [ "pattern", box [| "x" |]; "limit", box 0 ]

    match decodeFuzzyFindArgs args with
    | Error(InvalidIntent("fuzzy_find", "limit", "must be >= 1")) -> check "find limit zero" true
    | _ -> check "find limit zero" false

let decodeGrepOkWithExcludeArray () =
    let args =
        createObj
            [ "pattern", box [| "needle" |]
              "exclude", box [| "test/"; "*.min.js" |]
              "searchIgnored", box true
              "caseSensitive", box true
              "context", box 2
              "limit", box 25 ]

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
    | Error(InvalidIntent("fuzzy_grep", "pattern", msg)) when msg.Contains("pattern is required") ->
        check "grep missing pattern error" true
    | _ -> check "grep missing pattern error" false

let decodeGrepLimitBelowOneErrors () =
    let args = createObj [ "pattern", box [| "x" |]; "limit", box -1 ]

    match decodeFuzzyGrepArgs args with
    | Error(InvalidIntent("fuzzy_grep", "limit", "must be >= 1")) -> check "grep limit negative" true
    | _ -> check "grep limit negative" false

let decodeContinueOk () =
    let args = createObj [ "iterator", box "ffi_f_1" ]

    match decodeFuzzyContinueArgs args with
    | Ok p -> check "continue iterator" (p.iterator = "ffi_f_1")
    | Error _ -> failwith "decodeFuzzyContinueArgs expected Ok"

let decodeContinueMissingIteratorErrors () =
    let args = createObj []

    match decodeFuzzyContinueArgs args with
    | Error(InvalidIntent("fuzzy_continue", "iterator", _)) -> check "continue missing iterator error" true
    | _ -> check "continue missing iterator error" false

let decodeGrepPatternArray () =
    let args = createObj [ "pattern", box [| "word1"; "word2" |] ]
    let p = okGrep args
    check "grep array pattern length" (p.pattern.Length = 2)
    check "grep array pattern first" (p.pattern.[0] = "word1")
    check "grep array pattern second" (p.pattern.[1] = "word2")

let decodeFindPatternArray () =
    let args = createObj [ "pattern", box [| "foo"; "bar"; "baz" |] ]
    let p = okFind args
    check "find array pattern length" (p.pattern.Length = 3)
    check "find array pattern first" (p.pattern.[0] = "foo")
    check "find array pattern second" (p.pattern.[1] = "bar")
    check "find array pattern third" (p.pattern.[2] = "baz")

let decodeGrepPatternScalarStringSucceeds () =
    let args = createObj [ "pattern", box "single_string" ]
    let p = okGrep args
    check "grep pattern scalar string succeeds" (p.pattern = [ "single_string" ])

let decodeFindPatternScalarStringSucceeds () =
    let args = createObj [ "pattern", box "single_string" ]
    let p = okFind args
    check "find pattern scalar string succeeds" (p.pattern = [ "single_string" ])

let decodeGrepPatternJsonArraySucceeds () =
    let args = createObj [ "pattern", box "[ \"word1\", \"word2\" ]" ]
    let p = okGrep args
    check "grep json array pattern length" (p.pattern.Length = 2)
    check "grep json array pattern first" (p.pattern.[0] = "word1")
    check "grep json array pattern second" (p.pattern.[1] = "word2")

let decodeFindPatternJsonArraySucceeds () =
    let args = createObj [ "pattern", box "[ \"foo\", \"bar\" ]" ]
    let p = okFind args
    check "find json array pattern length" (p.pattern.Length = 2)
    check "find json array pattern first" (p.pattern.[0] = "foo")
    check "find json array pattern second" (p.pattern.[1] = "bar")

let run () =
    decodeFindOkFull ()
    decodeFindMissingPatternErrors ()
    decodeFindLimitBelowOneErrors ()
    decodeGrepOkWithExcludeArray ()
    decodeGrepExcludeScalar ()
    decodeGrepMissingPatternErrors ()
    decodeGrepLimitBelowOneErrors ()
    decodeContinueOk ()
    decodeContinueMissingIteratorErrors ()
    decodeGrepPatternArray ()
    decodeFindPatternArray ()
    decodeGrepPatternScalarStringSucceeds ()
    decodeFindPatternScalarStringSucceeds ()
    decodeGrepPatternJsonArraySucceeds ()
    decodeFindPatternJsonArraySucceeds ()
