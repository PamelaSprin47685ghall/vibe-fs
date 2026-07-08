module Wanxiangshu.Tests.DedupTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.CapsFormat
open Wanxiangshu.Kernel.Dedup
open Wanxiangshu.Kernel.MessageDedup
open Wanxiangshu.Kernel.ToolOutputInfo

let isNoChangeOutputTrue () =
    check "noChangeEnvelope is recognised" (isNoChangeOutput (noChangeEnvelope ()))

let isNoChangeOutputFalse () =
    check "plain text is not no-change" (not (isNoChangeOutput "hello world"))
    check "empty string is not no-change" (not (isNoChangeOutput ""))

let deduplicateFirstSeen () =
    let r = deduplicate [] "hello"
    equal "first-seen output unchanged" "hello" r.output
    check "first-seen appended to seenOutputs" (r.seenOutputs = [ "hello" ])

let deduplicateSeenVerbatim () =
    let r = deduplicate [ "hello" ] "hello"
    equal "verbatim repeat returns noChangeEnvelope" (noChangeEnvelope ()) r.output
    check "verbatim repeat keeps seenOutputs" (r.seenOutputs = [ "hello" ])

let deduplicateSubstringOfSeen () =
    let r = deduplicate [ "hello world" ] "hello"
    equal "substring repeat returns noChangeEnvelope" (noChangeEnvelope ()) r.output
    check "substring repeat keeps seenOutputs" (r.seenOutputs = [ "hello world" ])

let deduplicateEmptyOutput () =
    let r = deduplicate [] ""
    // empty output short-circuits: not appended to seen, output stays empty
    check "empty output not appended" (r.seenOutputs = [])
    check "empty output returned verbatim" (r.output = "")

let processDedupFirstCall () =
    let state = createDedupState ()
    let payload = { path = "a.ts"; content = "unique" }

    match processDedup state payload with
    | NewContent p, _ ->
        equal "content path matches" "a.ts" p.path
        equal "content body matches" "unique" p.content
    | AlreadySeen, _ -> failwith "first call must be NewContent"

let processDedupRepeat () =
    let payload = { path = "a.ts"; content = "same" }
    let v1, s1 = processDedup (createDedupState ()) payload

    check
        "first verdict NewContent"
        (match v1 with
         | NewContent _ -> true
         | AlreadySeen -> false)

    let v2, _ = processDedup s1 payload

    check
        "second verdict AlreadySeen"
        (match v2 with
         | AlreadySeen -> true
         | NewContent _ -> false)

let dedupForPathIsolated () =
    let mutable m = Map.empty
    let p1 = { path = "a.ts"; content = "alpha" }
    let m1, v1 = dedupForPath m p1
    m <- m1

    check
        "first path NewContent"
        (match v1 with
         | NewContent _ -> true
         | AlreadySeen -> false)

    check "a.ts registered" (Map.containsKey "a.ts" m)
    let p2 = { path = "b.ts"; content = "beta" }
    let m2, v2 = dedupForPath m p2
    m <- m2

    check
        "second path NewContent"
        (match v2 with
         | NewContent _ -> true
         | AlreadySeen -> false)

    check "both paths registered" (Map.count m = 2)
    let _, v3 = dedupForPath m { path = "a.ts"; content = "alpha" }

    check
        "repeat on a.ts AlreadySeen"
        (match v3 with
         | AlreadySeen -> true
         | NewContent _ -> false)

let foldDedupBasic () =
    let payloads =
        [ { path = "x.ts"; content = "a" }
          { path = "x.ts"; content = "a" }
          { path = "y.ts"; content = "b" } ]

    let _, (outputs, replaced) = foldDedup Map.empty payloads
    // AlreadySeen for second x.ts/a means only "a" (x.ts first) and "b" (y.ts) appear
    equal "outputs length = 2" 2 outputs.Length
    equal "outputs[0]" "a" outputs.[0]
    equal "outputs[1]" "b" outputs.[1]
    check "replaced[0] false" (not replaced.[0])
    check "replaced[1] true" replaced.[1]
    check "replaced[2] false" (not replaced.[2])

let collectReadOutputsByPathGroups () =
    let payloads =
        [ { path = "a.ts"; content = "A1" }
          { path = "b.ts"; content = "B1" }
          { path = "a.ts"; content = "A2" } ]

    let m = collectReadOutputsByPath payloads
    equal "a.ts count" 2 (m.["a.ts"] |> List.length)
    equal "a.ts[0]" "A1" m.["a.ts"].[0]
    equal "a.ts[1]" "A2" m.["a.ts"].[1]
    equal "b.ts count" 1 (m.["b.ts"] |> List.length)

let collectReadOutputsExtractsContents () =
    let payloads =
        [ { path = "z.ts"; content = "line1" }; { path = "z.ts"; content = "line2" } ]

    let contents = collectReadOutputs payloads
    equal "contents length" 2 contents.Length
    equal "contents[0]" "line1" contents.[0]

let readFingerprintMatchesAcrossFormats () =
    let caps = formatReadOutput "src/A.fs" "alpha\nbeta\ngamma" 1 None

    let native =
        // Mirror Shell.FileSys.readFileWithLineNumbers: 6-wide pipe, no wrapper.
        [ "     1|alpha"; "     2|beta"; "     3|gamma" ] |> String.concat "\n"

    let fpCaps = readFingerprint caps
    let fpNative = readFingerprint native

    match fpCaps, fpNative with
    | Some a, Some b -> equal "caps vs native fingerprint equal" a b
    | _ -> failwith "expected both fingerprints Some"

let readFingerprintIgnoresFooterAndWrapper () =
    let caps = formatReadOutput "src/A.fs" "alpha\nbeta" 1 None
    // Synthesize a FileSys-shape output with different footer wrapper.
    let opencodeStyle =
        "<file>\n     1|alpha\n     2|beta\n</file>\n<status>Showing lines 1-2 of 2</status>"

    let fpCaps = readFingerprint caps
    let fpNative = readFingerprint opencodeStyle

    match fpCaps, fpNative with
    | Some a, Some b -> equal "caps vs wrapped native fingerprint equal" a b
    | _ -> failwith "expected both fingerprints Some"

let readFingerprintRejectsDirectoryOutput () =
    let dirListing =
        String.concat
            "\n"
            [ "drwxr-xr-x  4096 Mar 01 src/"
              "-rw-r--r--  1024 Mar 01 README.md"
              ""
              "(2 entries)" ]

    check "directory listing yields no fingerprint" (readFingerprint dirListing).IsNone

let readFingerprintSubstringNoFalsePositive () =
    // Two reads with overlapping but distinct line ranges → different fingerprints.
    let caps1 = formatReadOutput "src/A.fs" "alpha\nbeta\ngamma\ndelta" 1 None
    let caps2 = formatReadOutput "src/A.fs" "beta\ngamma\ndelta\nepsilon" 2 None

    match readFingerprint caps1, readFingerprint caps2 with
    | Some a, Some b -> check "different content yields different fingerprints" (a <> b)
    | _ -> failwith "expected both fingerprints Some"

let deduplicateFingerprintMatchAcrossFormats () =
    let caps = formatReadOutput "src/A.fs" "alpha\nbeta\ngamma" 1 None
    let native = [ "     1|alpha"; "     2|beta"; "     3|gamma" ] |> String.concat "\n"
    let r1 = deduplicate [] caps
    equal "first-seen output unchanged" caps r1.output
    let r2 = deduplicate r1.seenOutputs native
    equal "native repeat returns noChangeEnvelope" (noChangeEnvelope ()) r2.output

let deduplicateFingerprintMatchAcrossLimits () =
    // Same file, different limits → overlapping content → different fingerprints,
    // so smaller window is NOT considered a duplicate of larger (different lines).
    let small = formatReadOutput "src/A.fs" "alpha\nbeta" 1 None
    let large = formatReadOutput "src/A.fs" "alpha\nbeta\ngamma" 1 None
    let r1 = deduplicate [] large
    let r2 = deduplicate r1.seenOutputs small
    // Different fingerprints → r2 must NOT collapse small into noChange.
    check "smaller overlapping window treated as new" (not (isNoChangeOutput r2.output))

let deduplicateFingerprintMatchAcrossFooterVariants () =
    let caps = formatReadOutput "src/A.fs" "alpha\nbeta" 1 None
    // Different footer wrapper, same line content.
    let altFooter =
        "<output>\n     1|alpha\n     2|beta\n</output>\n<status>Output capped at 2 lines</status>"

    let r1 = deduplicate [] caps
    let r2 = deduplicate r1.seenOutputs altFooter
    equal "alt-footer repeat returns noChangeEnvelope" (noChangeEnvelope ()) r2.output

let deduplicateLargeSubstringOfSeenDoesNotMatch () =
    let largeSeen = String.init 2500 (fun _ -> "a") + " b"
    let largeOutput = String.init 2100 (fun _ -> "a")
    let r = deduplicate [ largeSeen ] largeOutput
    check "large substring should not be deduplicated" (r.output = largeOutput)

let run () =
    isNoChangeOutputTrue ()
    isNoChangeOutputFalse ()
    deduplicateFirstSeen ()
    deduplicateSeenVerbatim ()
    deduplicateSubstringOfSeen ()
    deduplicateEmptyOutput ()
    processDedupFirstCall ()
    processDedupRepeat ()
    dedupForPathIsolated ()
    foldDedupBasic ()
    collectReadOutputsByPathGroups ()
    collectReadOutputsExtractsContents ()
    readFingerprintMatchesAcrossFormats ()
    readFingerprintIgnoresFooterAndWrapper ()
    readFingerprintRejectsDirectoryOutput ()
    readFingerprintSubstringNoFalsePositive ()
    deduplicateFingerprintMatchAcrossFormats ()
    deduplicateFingerprintMatchAcrossLimits ()
    deduplicateFingerprintMatchAcrossFooterVariants ()
    deduplicateLargeSubstringOfSeenDoesNotMatch ()
