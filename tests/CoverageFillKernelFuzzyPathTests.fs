module Wanxiangshu.Tests.CoverageFillKernelFuzzyPathTests

open Fable.Core
open Fable.Core.JsInterop
open System
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.FuzzyPath
open Wanxiangshu.Kernel.WebFetchGuard
open Wanxiangshu.Kernel.ExecutorStrip

// ── Kernel.FuzzyPath ───────────────────────────────────────────────────────

let fpNormalizePathConstraint () =
    let cwd = "/workspace"
    equal "empty→None" None (normalizePathConstraint "" cwd)
    equal "dot→None" None (normalizePathConstraint "." cwd)
    equal "src→Some src/" (Some "src/") (normalizePathConstraint "src" cwd)
    equal "src slash" (Some "src/") (normalizePathConstraint "src/" cwd)
    equal "abs within" (Some "src/") (normalizePathConstraint "/workspace/src" cwd)
    equal "abs outside→None" None (normalizePathConstraint "/other" cwd)
    equal "recursive glob" (Some "src/") (normalizePathConstraint "src/**" cwd)
    equal "glob fs" (Some "src/*.fs") (normalizePathConstraint "src/*.fs" cwd)

let fpBuildQuery () =
    let cwd = "/workspace"
    equal "no path no exclude" "foo" (buildQuery None "foo" [] cwd false)
    equal "path+exclude+pattern" "src/ !node_modules/ foo" (buildQuery (Some "src") "foo" [ "node_modules" ] cwd false)
    equal "external abs" "/ext/file p" (buildQuery (Some "/ext/file") "p" [] cwd true)

let fpResolveSearchPath () =
    let cwd = "/workspace"
    let r0 = resolveFuzzySearchPath None cwd
    check "none: cwd base" (r0.basePath = cwd)
    check "none: no constraint" r0.pathConstraint.IsNone
    check "none: not external" (not r0.external)
    let r1 = resolveFuzzySearchPath (Some "src") cwd
    check "src: cwd base" (r1.basePath = cwd)
    equal "src constraint" (Some "src") r1.pathConstraint
    let r2 = resolveFuzzySearchPath (Some "/ext") cwd
    check "ext: external base" (r2.basePath = "/ext")
    check "ext: no constraint" r2.pathConstraint.IsNone
    check "ext: is external" r2.external
    let r3 = resolveFuzzySearchPath (Some "/ext/file.txt") cwd
    equal "ext-file: base" "/ext" r3.basePath
    equal "ext-file: constraint" (Some "file.txt") r3.pathConstraint

let fpResolveExternalPath () =
    let cwd = "/workspace"
    let (b0, c0) = resolveExternalPath (Some "src") cwd
    check "internal: base none" b0.IsNone
    check "internal: constraint none" c0.IsNone
    let (b1, c1) = resolveExternalPath (Some "/ext") cwd
    equal "ext base" (Some "/ext") b1
    check "ext no constraint" c1.IsNone
    let (b2, c2) = resolveExternalPath (Some "/ext/file.txt") cwd
    equal "ext-file base" (Some "/ext") b2
    equal "ext-file constraint" (Some "file.txt") c2

let fpResolveExternalBasePathForTest () =
    let r1 = resolveExternalBasePathForTest "/ext"
    equal "dir base" "/ext" r1.basePath
    check "dir no constraint" r1.pathConstraint.IsNone
    let r2 = resolveExternalBasePathForTest "/ext/file.txt"
    equal "file base" "/ext" r2.basePath
    equal "file constraint" (Some "file.txt") r2.pathConstraint

// ── Kernel.WebFetchGuard ───────────────────────────────────────────────────

let wfgValidateUrl () =
    match validateFetchUrl "http://example.com" with
    | Ok() -> check "http ok" true
    | Error _ -> check "http ok" false

    match validateFetchUrl "https://example.com/path" with
    | Ok() -> check "https ok" true
    | Error _ -> check "https ok" false

    match validateFetchUrl "http://[::1]" with
    | Error msg -> equal "ipv6 literal blocked" "host not allowed" msg
    | Ok() -> check "ipv6 blocked" false

    match validateFetchUrl "not a url" with
    | Error msg -> equal "invalid url" "invalid URL" msg
    | Ok() -> check "invalid blocked" false

    match validateFetchUrl "ftp://example.com" with
    | Error msg -> equal "ftp unsupported" "unsupported URL scheme: ftp" msg
    | Ok() -> check "ftp blocked" false

    match validateFetchUrl "http://localhost" with
    | Error msg -> equal "localhost blocked" "host not allowed" msg
    | Ok() -> check "localhost blocked" false

    match validateFetchUrl "http://127.0.0.1" with
    | Error msg -> equal "private ipv4 blocked" "host not allowed" msg
    | Ok() -> check "loopback blocked" false

    match validateFetchUrl "http://8.8.8.8" with
    | Ok() -> check "public ip ok" true
    | Error _ -> check "public ip ok" false

// ── Kernel.ExecutorStrip ───────────────────────────────────────────────────

let stripHeadTailPipes () =
    let r = strip "cat file | head -n 5 | tail -n 2"
    equal "head-tail stripped" "cat file" r.script
    equal "head-tail count" 2 r.stripped.Length
    equal "first name" "head" r.stripped.[0].name
    equal "first count" 5 r.stripped.[0].count
    equal "second name" "tail" r.stripped.[1].name
    equal "second count" 2 r.stripped.[1].count

let stripSingleQuotes () =
    let r = strip "echo 'hello world' | head -n 3"
    equal "single-quote preserved" "echo 'hello world'" r.script
    equal "head extracted" 1 r.stripped.Length
    equal "head count" 3 r.stripped.[0].count

let stripDoubleQuotes () =
    let r = strip """echo "pipe|here" | tail -n 1"""
    check "double-quote preserved" (r.script.Contains "\"pipe|here\"")
    equal "tail count" 1 r.stripped.[0].count

let stripComment () =
    let r = strip "cat file | head -n 5 # skip rest"
    equal "comment kept" "cat file # skip rest" r.script
    equal "head extracted" 1 r.stripped.Length

let stripNoPipe () =
    let r = strip "cat file"
    equal "no change" "cat file" r.script
    check "no stripped" (List.isEmpty r.stripped)

let stripUnsupportedCommand () =
    let r = strip "cat file | grep 5"
    equal "unsupported unchanged" "cat file | grep 5" r.script
    check "no stripped" (List.isEmpty r.stripped)

let run () =
    fpNormalizePathConstraint ()
    fpBuildQuery ()
    fpResolveSearchPath ()
    fpResolveExternalPath ()
    fpResolveExternalBasePathForTest ()
    wfgValidateUrl ()
    stripHeadTailPipes ()
    stripSingleQuotes ()
    stripDoubleQuotes ()
    stripComment ()
    stripNoPipe ()
    stripUnsupportedCommand ()
