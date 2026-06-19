module VibeFs.Tests.KernelTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Kernel.Dedup
open VibeFs.Kernel.Executor
open VibeFs.Shell.WorkspaceFiles
open VibeFs.Kernel.Prompts
open VibeFs.Kernel.SubagentIntents
open VibeFs.Kernel.Domain
open VibeFs.Kernel.Message

let headTail' () =
    let r = headTail "hello" 2 2
    check "headTail" (r = "he...lo")

let dedup' () =
    let s = createDedupState ()
    let r1 = deduplicate s.seenContents "same"
    let r2 = deduplicate r1.seenOutputs "same"
    check "dedup first" (r1.output = "same")
    check "dedup second" (r2.output = dedupMarker)

let jsBoundary' () =
    check "abort message classified" (translateJsError (createObj [ "message", box "Aborted" ]) = VibeFs.Kernel.Domain.MessageAborted)
    let text = readAssistantText [| box {| ``type`` = "message"; message = box {| role = "assistant"; content = [| box {| ``type`` = "text"; text = "hello" |} |] |} |} |] None
    check "assistant text read" (text = Some "hello")

let hostKernel' () =
    let coderIntent =
        { objective = "fix bug"
          background = "user reported failure"
          targets = [ { file = "a.ts"; guide = "fix root cause"; draft = None }; { file = "b.ts"; guide = "align types"; draft = None } ]
          doNotTouch = [| "shared.ts" |] }
    let intent = formatCoderUserPrompt coderIntent
    check "coder has file" (intent.IndexOf("a.ts") >= 0)
    check "coder has objective" (intent.IndexOf("fix bug") >= 0)
    check "coder has do_not_touch" (intent.IndexOf("shared.ts") >= 0)
    let prompt = buildMeditatorPrompt [ { file = "x.fs"; content = Some "let x = 1" } ] "why?"
    check "meditator has question" (prompt.IndexOf("why?") >= 0)
    check "meditator has content" (prompt.IndexOf("let x = 1") >= 0)
    check "meditator read-only" (prompt.IndexOf("READ-ONLY") >= 0)
    let inv =
        { objective = "find auth"
          background = "need entry points"
          questions = [| "Where is auth configured?" |]
          entries = [||] }
    let investigatorPrompt = formatInvestigatorUserPrompt inv
    check "investigator has objective" (investigatorPrompt.IndexOf("find auth") >= 0)
    check "investigator read-only" (investigatorPrompt.IndexOf("READ-ONLY") >= 0)
