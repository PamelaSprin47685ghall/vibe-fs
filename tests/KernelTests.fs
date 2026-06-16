module VibeFs.Tests.KernelTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Kernel.Dedup
open VibeFs.Kernel.HeadTail
open VibeFs.Kernel.IpAllowlist
open VibeFs.Kernel.Lru
open VibeFs.Kernel.ExcludedDirs
open VibeFs.Kernel.HostKernel
open VibeFs.Kernel.Prompts
open VibeFs.Kernel.JsBoundary
open VibeFs.Kernel.DomainError

let headTail' () =
    let r = headTail "hello" 2 2
    check "headTail" (r = "he...lo")

let dedup' () =
    let s = createDedupState ()
    let r1 = deduplicate s.seenContents "same"
    let r2 = deduplicate r1.seenOutputs "same"
    check "dedup first" (r1.output = "same")
    check "dedup second" (r2.output = dedupMarker)

let lru' () =
    let cache = create 2
    let c1 = set cache "a" 1
    let c2 = set c1 "b" 2
    let c3 = set c2 "c" 3
    check "lru evicts" (Map.count c3.cache = 2)
    check "lru has b" (Map.containsKey "b" c3.cache)
    check "lru has c" (Map.containsKey "c" c3.cache)

let ipAllowlist' () =
    let r1 = isIpAllowed "127.0.0.1"
    let r2 = isIpAllowed "10.0.0.1"
    check "loopback blocked" (not r1)
    check "private blocked" (not r2)

let ipStrict () =
    let r = isIpAllowed "8.8.8.8"
    check "public allowed" r

let excludedDirs' () =
    let r = isExcludedDir "node_modules"
    check "node_modules excluded" r

let jsBoundary' () =
    let r = parseJsBoundary "42"
    check "parse int" (r = Some 42)
    let s = parseJsBoundary "x"
    check "parse fail" (s.IsNone)
    let arr = parseJsBoundaryArray [| box 1; box "2" |]
    check "parse array" (arr = [| 1; 2 |])
    let obj = parseJsBoundaryObj (createObj [ "a", box 1 ])
    check "parse obj" (Map.find "a" obj = 1)
    check "abort message classified" (translateJsError (createObj [ "message", box "Aborted" ]) = MessageAborted)

let hostKernel' () =
    let intent = formatCoderUserPrompt "fix bug" [ "a.ts"; "b.ts" ]
    check "coder has file" (intent.IndexOf("a.ts") >= 0)
    let prompt = buildMeditatorPrompt [ { file = "x.fs"; content = Some "let x = 1" } ] "why?"
    check "meditator has question" (prompt.IndexOf("why?") >= 0)
    check "meditator has content" (prompt.IndexOf("let x = 1") >= 0)
    check "meditator read-only" (prompt.IndexOf("READ-ONLY") >= 0)
    let readerPrompt = formatReaderUserPrompt "find auth"
    check "reader has intent" (readerPrompt.IndexOf("find auth") >= 0)
    check "reader read-only" (readerPrompt.IndexOf("READ-ONLY") >= 0)
