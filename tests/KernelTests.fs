module VibeFs.Tests.KernelTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Kernel.Dedup
open VibeFs.Kernel.HeadTail
open VibeFs.Shell.Caps
open VibeFs.Kernel.Prompts
open VibeFs.Kernel.JsBoundary
open VibeFs.Kernel.MessageDecoder

let headTail' () =
    let r = headTail "hello" 2 2
    check "headTail" (r = "he...lo")

let dedup' () =
    let s = createDedupState ()
    let r1 = deduplicate s.seenContents "same"
    let r2 = deduplicate r1.seenOutputs "same"
    check "dedup first" (r1.output = "same")
    check "dedup second" (r2.output = dedupMarker)

let excludedDirs' () =
    let r = isExcludedDir "node_modules"
    check "node_modules excluded" r

let jsBoundary' () =
    check "abort message classified" (translateJsError (createObj [ "message", box "Aborted" ]) = VibeFs.Kernel.JsBoundary.MessageAborted)
    let text = readAssistantText [| box {| ``type`` = "message"; message = box {| role = "assistant"; content = [| box {| ``type`` = "text"; text = "hello" |} |] |} |} |] None
    check "assistant text read" (text = Some "hello")

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
