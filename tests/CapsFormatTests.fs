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

let buildCapitalsContextEmpty () =
    let result = buildCapitalsContext []
    check "empty caps still wrapped in front-matter fences" (result.StartsWith "---\n")
    check "empty caps has closing fence" (result.Contains "\n---")

let buildCapitalsContextSingleFile () =
    let files = [ { filePath = "/p.fs"; label = "Plugin"; content = "let x = 1" } ]
    let result = buildCapitalsContext files
    check "single caps has front-matter opening" (result.StartsWith "---\n")
    check "single caps contains caps field" (result.Contains "caps:")
    check "single caps contains label" (result.Contains "Plugin")
    check "single caps contains content" (result.Contains "let x = 1")
    check "single caps has closing fence" (result.Contains "\n---")

let buildCapitalsContextMultipleFilesPreservesOrder () =
    let files = [
        { filePath = "/a"; label = "A"; content = "ca" }
        { filePath = "/b"; label = "B"; content = "cb" }
    ]
    let result = buildCapitalsContext files
    let aIdx = result.IndexOf "A"
    let bIdx = result.IndexOf "B"
    check "caps preserve list order" (aIdx >= 0 && bIdx >= 0 && aIdx < bIdx)

let buildCapitalsContextLabelAndContentDistinct () =
    let files = [ { filePath = "/p"; label = "my-label"; content = "my-content" } ]
    let result = buildCapitalsContext files
    check "label present in output" (result.Contains "my-label")
    check "content present in output" (result.Contains "my-content")

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
    buildCapitalsContextEmpty ()
    buildCapitalsContextSingleFile ()
    buildCapitalsContextMultipleFilesPreservesOrder ()
    buildCapitalsContextLabelAndContentDistinct ()
    formatReadOutputSingleLine ()
    formatReadOutputMultiLine ()
    formatReadOutputEmptyContent ()
    formatReadOutputClosingTags ()
