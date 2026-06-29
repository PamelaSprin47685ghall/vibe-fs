module Wanxiangshu.Tests.CapsFormatTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.CapsFormat

let stableFingerprintEmptyList () =
    let h (s: string) = s
    equal "empty list fingerprint is empty string" "" (stableFingerprint h [])

let stableFingerprintSingleFile () =
    let h (s: string) = s
    let files = [ { filePath = "/abs/path/a.fs"; label = "A"; content = "alpha\nbeta" } ]
    let fp = stableFingerprint h files
    check "single file fingerprint non-empty" (fp <> "")
    equal "single file fingerprint identity returns concatenated input" "/abs/path/a.fs\u0000alpha\nbeta\u0000" fp

let stableFingerprintTwoFilesConcatenationOrder () =
    let h (s: string) = s
    let files = [
        { filePath = "/a.fs"; label = "A"; content = "c1" }
        { filePath = "/b.fs"; label = "B"; content = "c2" }
    ]
    let fp = stableFingerprint h files
    equal "two files fingerprint ordered" "/a.fs\u0000c1\u0000/b.fs\u0000c2\u0000" fp

let stableFingerprintDeterministicAcrossCalls () =
    let h (s: string) = s
    let files = [ { filePath = "/x"; label = "X"; content = "v" } ]
    equal "deterministic across calls" (stableFingerprint h files) (stableFingerprint h files)

let stableFingerprintContentChangeAltersResult () =
    let h (s: string) = s
    let f1 = { filePath = "/x"; label = "X"; content = "v1" }
    let f2 = { filePath = "/x"; label = "X"; content = "v2" }
    check "content change alters fingerprint" ((stableFingerprint h [f1]) <> (stableFingerprint h [f2]))

let stableFingerprintFilePathChangeAltersResult () =
    let h (s: string) = s
    let f1 = { filePath = "/a"; label = "X"; content = "v" }
    let f2 = { filePath = "/b"; label = "X"; content = "v" }
    check "file path change alters fingerprint" ((stableFingerprint h [f1]) <> (stableFingerprint h [f2]))

let formatReadOutputSingleLine () =
    let result = formatReadOutput "/path/to/file.txt" "hello" 1
    check "single line output starts with path tag" (result.StartsWith "<path>/path/to/file.txt</path>")
    check "single line output contains type tag" (result.Contains "<type>file</type>")
    check "single line output contains content tag" (result.Contains "<content>")
    check "single line output has line number" (result.Contains "1: hello")
    check "single line output has end-of-file footer" (result.Contains "(End of file - total 1 lines)")

let formatReadOutputMultiLine () =
    let content = "line1\nline2\nline3"
    let result = formatReadOutput "/f" content 1
    check "multi line has 1: line1" (result.Contains "1: line1")
    check "multi line has 2: line2" (result.Contains "2: line2")
    check "multi line has 3: line3" (result.Contains "3: line3")
    check "multi line footer shows 3 lines" (result.Contains "(End of file - total 3 lines)")

let formatReadOutputEmptyContent () =
    let result = formatReadOutput "/f" "" 1
    check "empty content still has content tag" (result.Contains "<content>")
    check "empty content footer shows 1 line (empty string splits to 1 element)" (result.Contains "(End of file - total 1 lines)")

let formatReadOutputClosingTags () =
    let result = formatReadOutput "/f" "x" 1
    check "closing content tag" (result.Contains "</content>")

let run () =
    stableFingerprintEmptyList ()
    stableFingerprintSingleFile ()
    stableFingerprintTwoFilesConcatenationOrder ()
    stableFingerprintDeterministicAcrossCalls ()
    stableFingerprintContentChangeAltersResult ()
    stableFingerprintFilePathChangeAltersResult ()
    formatReadOutputSingleLine ()
    formatReadOutputMultiLine ()
    formatReadOutputEmptyContent ()
    formatReadOutputClosingTags ()
