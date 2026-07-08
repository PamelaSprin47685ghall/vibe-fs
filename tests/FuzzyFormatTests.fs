module Wanxiangshu.Tests.FuzzyFormatTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.FuzzyFormat

let truncateLineTests () =
    let maxLen = 20
    // short unchanged
    equal "short" "hi" (truncateLine "hi" maxLen)
    equal "exact" "01234567890123456789" (truncateLine "01234567890123456789" maxLen)
    // long truncated with "..."
    let long = "012345678901234567890123456789"
    equal "long-truncated" "01234567890123456789..." (truncateLine long maxLen)
    // whitespace trimmed
    equal "trimmed" "hello world" (truncateLine "  hello world  " maxLen)

let fileAnnotationTests () =
    // None -> ""
    equal "none" "" (fileAnnotation None)
    // clean gitStatus -> ""
    equal
        "clean"
        ""
        (fileAnnotation (
            Some
                { gitStatus = Some "clean"
                  totalFrecencyScore = None
                  accessFrecencyScore = None }
        ))
    // unknown gitStatus -> ""
    equal
        "unknown"
        ""
        (fileAnnotation (
            Some
                { gitStatus = Some "unknown"
                  totalFrecencyScore = None
                  accessFrecencyScore = None }
        ))
    // empty gitStatus -> ""
    equal
        "empty-git"
        ""
        (fileAnnotation (
            Some
                { gitStatus = Some ""
                  totalFrecencyScore = None
                  accessFrecencyScore = None }
        ))
    // modified gitStatus -> "  [modified in git]"
    equal
        "modified"
        "  [modified in git]"
        (fileAnnotation (
            Some
                { gitStatus = Some "modified"
                  totalFrecencyScore = None
                  accessFrecencyScore = None }
        ))
    // hot frecency (>=25)
    equal
        "hot"
        "  [VERY often touched file]"
        (fileAnnotation (
            Some
                { gitStatus = None
                  totalFrecencyScore = Some 25
                  accessFrecencyScore = None }
        ))
    // warm frecency (>=20, <25)
    equal
        "warm"
        "  [often touched file]"
        (fileAnnotation (
            Some
                { gitStatus = None
                  totalFrecencyScore = Some 20
                  accessFrecencyScore = None }
        ))
    // cold frecency (<20)
    equal
        "cold"
        ""
        (fileAnnotation (
            Some
                { gitStatus = None
                  totalFrecencyScore = Some 10
                  accessFrecencyScore = None }
        ))
    // accessFrecency fallback when total is None
    equal
        "access-hot"
        "  [VERY often touched file]"
        (fileAnnotation (
            Some
                { gitStatus = None
                  totalFrecencyScore = None
                  accessFrecencyScore = Some 30 }
        ))

    equal
        "access-warm"
        "  [often touched file]"
        (fileAnnotation (
            Some
                { gitStatus = None
                  totalFrecencyScore = None
                  accessFrecencyScore = Some 22 }
        ))
    // gitStatus non-skipped takes priority over frecency
    equal
        "git-priority-over-frecency"
        "  [staged in git]"
        (fileAnnotation (
            Some
                { gitStatus = Some "staged"
                  totalFrecencyScore = Some 30
                  accessFrecencyScore = None }
        ))

let formatGrepOutputTests () =
    // None -> "No matches found"
    equal "none" "No matches found" (formatGrepOutput None)
    // empty items -> "No matches found"
    equal
        "empty-items"
        "No matches found"
        (formatGrepOutput (
            Some
                { items = []
                  totalMatched = None
                  regexFallbackError = None }
        ))
    // single match: "1 match", path, line
    let single =
        [ { relativePath = "src/App.fs"
            lineNumber = 42
            lineContent = "let x = 1"
            contextBefore = []
            contextAfter = []
            annotation = None } ]

    let out1 =
        formatGrepOutput (
            Some
                { items = single
                  totalMatched = Some 1
                  regexFallbackError = None }
        )

    check "single-starts-1-match" (out1.StartsWith "1 match")
    check "single-contains-path" (out1.Contains "src/App.fs")
    check "single-contains-line" (out1.Contains "42: let x = 1")
    // multiple matches from different files include blank separator
    let multi =
        [ { relativePath = "src/App.fs"
            lineNumber = 10
            lineContent = "lineA"
            contextBefore = []
            contextAfter = []
            annotation = None }
          { relativePath = "src/App.fs"
            lineNumber = 20
            lineContent = "lineB"
            contextBefore = []
            contextAfter = []
            annotation = None }
          { relativePath = "src/B.fs"
            lineNumber = 5
            lineContent = "lineC"
            contextBefore = []
            contextAfter = []
            annotation = None } ]

    let outM =
        formatGrepOutput (
            Some
                { items = multi
                  totalMatched = Some 3
                  regexFallbackError = None }
        )

    check "multi-3-matches" (outM.Contains "3 matches")
    check "multi-blank-separator" (outM.Contains "\n\n")

let formatFindOutputTests () =
    // None -> "No matching files found"
    equal "none" "No matching files found" (formatFindOutput None)
    // empty -> same
    equal
        "empty"
        "No matching files found"
        (formatFindOutput (
            Some
                { items = []
                  totalMatched = None
                  totalFiles = 100 }
        ))
    // multiple items -> header + list
    let items =
        [ { relativePath = "src/A.fs"
            annotation = None }
          { relativePath = "src/B.fs"
            annotation = None } ]

    let out =
        formatFindOutput (
            Some
                { items = items
                  totalMatched = Some 2
                  totalFiles = 100 }
        )

    check "find-header" (out.StartsWith "2 matching files (100 total indexed)")
    check "find-a" (out.Contains "src/A.fs")
    check "find-b" (out.Contains "src/B.fs")
    // single file uses singular
    let out1 =
        formatFindOutput (
            Some
                { items = [ items.[0] ]
                  totalMatched = Some 1
                  totalFiles = 50 }
        )

    check "find-singular" (out1.StartsWith "1 matching file (50 total indexed)")

let run () =
    truncateLineTests ()
    fileAnnotationTests ()
    formatGrepOutputTests ()
    formatFindOutputTests ()
