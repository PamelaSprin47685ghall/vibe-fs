module Wanxiangshu.Tests.DedupTests

open Wanxiangshu.Tests.Assert
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
    // empty output has .Length=0 so else branch fires: seenOutputs = [] @ [""]
    check "empty output recorded" (r.seenOutputs = [ "" ])

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
    check "first verdict NewContent" (match v1 with NewContent _ -> true | AlreadySeen -> false)
    let v2, _ = processDedup s1 payload
    check "second verdict AlreadySeen" (match v2 with AlreadySeen -> true | NewContent _ -> false)

let dedupForPathIsolated () =
    let mutable m = Map.empty
    let p1 = { path = "a.ts"; content = "alpha" }
    let m1, v1 = dedupForPath m p1
    m <- m1
    check "first path NewContent" (match v1 with NewContent _ -> true | AlreadySeen -> false)
    check "a.ts registered" (Map.containsKey "a.ts" m)
    let p2 = { path = "b.ts"; content = "beta" }
    let m2, v2 = dedupForPath m p2
    m <- m2
    check "second path NewContent" (match v2 with NewContent _ -> true | AlreadySeen -> false)
    check "both paths registered" (Map.count m = 2)
    let _, v3 = dedupForPath m { path = "a.ts"; content = "alpha" }
    check "repeat on a.ts AlreadySeen" (match v3 with AlreadySeen -> true | NewContent _ -> false)

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
    let payloads = [ { path = "z.ts"; content = "line1" }; { path = "z.ts"; content = "line2" } ]
    let contents = collectReadOutputs payloads
    equal "contents length" 2 contents.Length
    equal "contents[0]" "line1" contents.[0]

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
