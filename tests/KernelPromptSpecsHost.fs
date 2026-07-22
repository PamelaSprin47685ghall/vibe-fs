module Wanxiangshu.Tests.KernelPromptSpecsHost

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.SubagentPrompts
open Wanxiangshu.Runtime.SubagentSummarizerPrompts
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Runtime.ReviewPrompts
open Wanxiangshu.Runtime.ReviewPrompts.Instructions

let private specOf (name: string) =
    match Wanxiangshu.Kernel.ToolCatalog.specOf name with
    | Ok s -> s
    | Error e -> failwith e

let hostKernel' () =
    let coderIntent =
        { objective = "fix bug"
          background = "user reported failure"
          targets =
            [ { file = "a.ts"
                guide = "fix root cause"
                draft = None }
              { file = "b.ts"
                guide = "align types"
                draft = None } ]
          doNotTouch = [| "shared.ts" |] }

    let intent = coderPrompt coderIntent
    check "coder has file" (intent.IndexOf("a.ts") >= 0)
    check "coder has objective" (intent.IndexOf("fix bug") >= 0)
    check "coder has do_not_touch" (intent.IndexOf("shared.ts") >= 0)

    let dummyEntry: Wanxiangshu.Kernel.Methodology.Schema.MethodologyEntry =
        { methodologyId = "test_methodology"
          shortDefinition = "test def"
          triggerWhen = "test trigger"
          noteDescription = "test note desc"
          meditatorRole = "test role"
          outputSections = [] }

    let prompt =
        renderMeditatorIntent dummyEntry "why?" "my background" "note detail"

    check "meditator has question" (prompt.Contains "why?")
    check "meditator no-tools constraint" (prompt.Contains "NO_TOOLS")
    check "meditator methodology structured id" (prompt.Contains "methodology_id" || prompt.Contains "test_methodology")
    check "meditator no METHODOLOGY_ID prose" (not (prompt.Contains "METHODOLOGY_ID:"))

    let inv =
        { objective = "find auth"
          background = "need entry points"
          questions = [| "Where is auth configured?" |]
          entries = [||] }

    let inspectorPromptText = inspectorPrompt inv
    check "inspector has objective" (inspectorPromptText.Contains "find auth")
    check "inspector agent role codebase search" (inspectorPromptText.Contains "Codebase Search" || inspectorPromptText.Contains "read-only")

let toolCatalogCentralized () =
    let coderSpec = specOf "coder"
    check "coder spec carries description" (coderSpec.description.Length > 0)
    check "coder description names intents" (coderSpec.description.Contains "intents")

    check "coder requiredFields are intents+tdd" (coderSpec.requiredFields = [ "intents"; "tdd" ])
    check "coder param doc for intents" (Map.containsKey "intents" coderSpec.paramDocs)
    check "coder param doc for tdd" (Map.containsKey "tdd" coderSpec.paramDocs)

    let inspectorSpec = specOf "inspector"
    check "inspector spec carries description" (inspectorSpec.description.Length > 0)
    check "inspector requires intents only" (inspectorSpec.requiredFields = [ "intents" ])

    let executorSpec = specOf "executor"
    check "executor describes timeout budgets" (executorSpec.description.Contains "timeout")

    check "executor does not require mode" (not (executorSpec.requiredFields |> List.contains "mode"))

    check "executor does not require mode" (not (executorSpec.requiredFields |> List.contains "mode"))

    check "executor param doc no mode" (not (Map.containsKey "mode" executorSpec.paramDocs))

    let writeSpec = specOf "write"
    check "write spec carries description" (writeSpec.description.Length > 0)

    check "write requiredFields are file_path+content" (writeSpec.requiredFields = [ "file_path"; "content" ])

    let swapSpec = specOf "swap"

    check
        "swap describes structure-preserving refactoring"
        (swapSpec.description.Contains "structure-preserving refactoring")

    check "swap describes semantic blocks" (swapSpec.description.Contains "semantic blocks")
    check "swap avoids rewriting contents" (swapSpec.description.Contains "without rewriting their contents")

    let ack = formatWipAcknowledgment "Progress recorded"
    check "wip acknowledgment is structured" (ack.Contains "review_progress" || ack.Contains "review_mode")
    check "wip acknowledgment does not say No reviewer" (ack.ToLowerInvariant().IndexOf("no reviewer") < 0)
    check "wip acknowledgment does not mention starting a reviewer" (ack.ToLowerInvariant().IndexOf("starting a reviewer") < 0)

    let allSpecs = all
    let names = allSpecs |> List.map (fun spec -> spec.name) |> Set.ofList
    check "catalog covers coder" (Set.contains "coder" names)
    check "catalog covers inspector" (Set.contains "inspector" names)
    check "catalog covers browser" (Set.contains "browser" names)
    check "catalog covers executor" (Set.contains "executor" names)
    check "catalog covers submit_review" (Set.contains "submit_review" names)
