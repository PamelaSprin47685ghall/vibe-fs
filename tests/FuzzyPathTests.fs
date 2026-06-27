module Wanxiangshu.Tests.FuzzyPathTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.FuzzyPath

let cwd = "/workspace"

let normalizePathConstraintTests () =
    // empty / whitespace -> None
    equal "empty" None (normalizePathConstraint "" cwd)
    equal "whitespace" None (normalizePathConstraint "   " cwd)
    // "." / "./" -> None
    equal "dot" None (normalizePathConstraint "." cwd)
    equal "dot-slash" None (normalizePathConstraint "./" cwd)
    // "src" -> "src/"
    equal "src" (Some "src/") (normalizePathConstraint "src" cwd)
    equal "src-slash" (Some "src/") (normalizePathConstraint "src/" cwd)
    // "src/Program.fs" kept as-is
    equal "src-file" (Some "src/Program.fs") (normalizePathConstraint "src/Program.fs" cwd)
    // absolute within cwd
    equal "abs-within" (Some "src/") (normalizePathConstraint "/workspace/src" cwd)
    // absolute outside -> None
    equal "abs-outside" None (normalizePathConstraint "/other" cwd)
    // recursive glob "src/**" -> "src/" (dir-only, no file suffix)
    equal "recursive-star" (Some "src/") (normalizePathConstraint "src/**" cwd)
    // recursive glob "src/**/*.fs" -> "src/**/*.fs" (file suffix, keep full glob)
    equal "recursive-star-fs" (Some "src/**/*.fs") (normalizePathConstraint "src/**/*.fs" cwd)
    // glob "src/*.fs" -> "src/*.fs"
    equal "glob-fs" (Some "src/*.fs") (normalizePathConstraint "src/*.fs" cwd)
    // "../src" -> "../src/"
    equal "parent-dir" (Some "../src/") (normalizePathConstraint "../src" cwd)

let normalizeExcludesTests () =
    // ["node_modules"] -> ["!node_modules/"]
    equal "single-token" ["!node_modules/"] (normalizeExcludes ["node_modules"] cwd)
    // ["a, b"] splits by comma+whitespace
    equal "comma-split" ["!a/"; "!b/"] (normalizeExcludes ["a, b"] cwd)
    // ["!../secret"] -> ["!../secret/"]
    equal "negated-outside" ["!../secret/"] (normalizeExcludes ["!../secret"] cwd)

let buildQueryTests () =
    // None path + no excludes -> just pattern
    equal "no-path-no-excludes" "foo" (buildQuery None "foo" [] cwd false)
    // path + exclude + pattern -> concatenated
    equal "path-exclude-pattern"
        "src/ !node_modules/ foo"
        (buildQuery (Some "src") "foo" ["node_modules"] cwd false)

let resolveFuzzySearchPathTests () =
    // None -> cwd base, no constraint
    let r0 = resolveFuzzySearchPath None cwd
    check "none-base-is-cwd" (r0.basePath = cwd)
    check "none-no-constraint" r0.pathConstraint.IsNone
    check "none-not-external" (not r0.external)
    // Some "src" -> cwd base, constraint "src"
    let r1 = resolveFuzzySearchPath (Some "src") cwd
    check "src-base-is-cwd" (r1.basePath = cwd)
    equal "src-constraint" (Some "src") r1.pathConstraint
    check "src-not-external" (not r1.external)
    // Some "/ext" -> external base "/ext", no constraint
    let r2 = resolveFuzzySearchPath (Some "/ext") cwd
    check "ext-base" (r2.basePath = "/ext")
    check "ext-no-constraint" r2.pathConstraint.IsNone
    check "ext-is-external" r2.external
    // Some "/ext/file.txt" -> external base "/ext", constraint "file.txt"
    let r3 = resolveFuzzySearchPath (Some "/ext/file.txt") cwd
    check "ext-file-base" (r3.basePath = "/ext")
    equal "ext-file-constraint" (Some "file.txt") r3.pathConstraint
    check "ext-file-is-external" r3.external

let resolveExternalPathTests () =
    // internal path -> None, None
    let (b0, c0) = resolveExternalPath (Some "src") cwd
    check "internal-base-none" b0.IsNone
    check "internal-constraint-none" c0.IsNone
    // Some "/ext" -> Some "/ext", None
    let (b1, c1) = resolveExternalPath (Some "/ext") cwd
    equal "ext-base" (Some "/ext") b1
    check "ext-no-constraint" c1.IsNone
    // Some "/ext/file.txt" -> Some "/ext", Some "file.txt"
    let (b2, c2) = resolveExternalPath (Some "/ext/file.txt") cwd
    equal "ext-file-base" (Some "/ext") b2
    equal "ext-file-constraint" (Some "file.txt") c2

let resolveExternalBasePathForTestTests () =
    let r1 = resolveExternalBasePathForTest "/ext"
    equal "dir-base" "/ext" r1.basePath
    check "dir-no-constraint" r1.pathConstraint.IsNone
    let r2 = resolveExternalBasePathForTest "/ext/file.txt"
    equal "file-base" "/ext" r2.basePath
    equal "file-constraint" (Some "file.txt") r2.pathConstraint

let run () =
    normalizePathConstraintTests ()
    normalizeExcludesTests ()
    buildQueryTests ()
    resolveFuzzySearchPathTests ()
    resolveExternalPathTests ()
    resolveExternalBasePathForTestTests ()
