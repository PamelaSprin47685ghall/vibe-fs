module Wanxiangshu.Tests.KernelPromptSpecsHost

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.SubagentPrompts
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Kernel.ReviewPrompts
open Wanxiangshu.Kernel.ReviewPrompts.Instructions

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

    let prompt =
        meditatorPrompt
            [ { file = "x.fs"
                content = Some "let x = 1" } ]
            "why?"

    check "meditator has question" (prompt.IndexOf("why?") >= 0)
    check "meditator has content" (prompt.IndexOf("let x = 1") >= 0)
    check "meditator read-only" (prompt.IndexOf("READ-ONLY") >= 0)

    let inv =
        { objective = "find auth"
          background = "need entry points"
          questions = [| "Where is auth configured?" |]
          entries = [||] }

    let investigatorPromptText = investigatorPrompt inv
    check "investigator has objective" (investigatorPromptText.IndexOf("find auth") >= 0)
    check "investigator read-only" (investigatorPromptText.IndexOf("READ-ONLY") >= 0)

let toolCatalogCentralized () =
    let coderSpec = specOf "coder"
    check "coder spec carries description" (coderSpec.description.Length > 0)
    check "coder description names intents" (coderSpec.description.Contains "intents")
    check "coder requiredFields no warn_tdd" (not (List.contains "warn_tdd" coderSpec.requiredFields))
    check "coder requiredFields are intents+tdd" (coderSpec.requiredFields = [ "intents"; "tdd" ])
    check "coder paramDoc no warn_tdd" (not (Map.containsKey "warn_tdd" coderSpec.paramDocs))
    check "coder paramDoc no warn key" (not (Map.containsKey "warn" coderSpec.paramDocs))
    check "coder param doc for intents" (Map.containsKey "intents" coderSpec.paramDocs)
    check "coder param doc for tdd" (Map.containsKey "tdd" coderSpec.paramDocs)

    let investigatorSpec = specOf "investigator"
    check "investigator spec carries description" (investigatorSpec.description.Length > 0)
    check "investigator requires intents only" (investigatorSpec.requiredFields = [ "intents" ])

    let executorSpec = specOf "executor"
    check "executor describes timeout budgets" (executorSpec.description.Contains "timeout")
    check "executor requiredFields no warn_tdd" (not (List.contains "warn_tdd" executorSpec.requiredFields))
    check "executor requiredFields no warn" (not (List.contains "warn" executorSpec.requiredFields))
    check "executor requires mode" (executorSpec.requiredFields |> List.contains "mode")
    check "executor paramDoc no warn_tdd" (not (Map.containsKey "warn_tdd" executorSpec.paramDocs))
    check "executor paramDoc no warn" (not (Map.containsKey "warn" executorSpec.paramDocs))
    check "executor param doc for mode" (Map.containsKey "mode" executorSpec.paramDocs)

    let writeSpec = specOf "write"
    check "write spec carries description" (writeSpec.description.Length > 0)
    check "write requiredFields no warn_tdd" (not (List.contains "warn_tdd" writeSpec.requiredFields))
    check "write requiredFields are file_path+content" (writeSpec.requiredFields = [ "file_path"; "content" ])
    check "write paramDoc no warn_tdd" (not (Map.containsKey "warn_tdd" writeSpec.paramDocs))
    check "write paramDoc no warn" (not (Map.containsKey "warn" writeSpec.paramDocs))

    let ackLower = submitReviewWipAcknowledgment.ToLowerInvariant()
    check "wip acknowledgment does not say No reviewer" (ackLower.IndexOf("no reviewer") < 0)
    check "wip acknowledgment does not mention wip set to false" (ackLower.IndexOf("wip set to false") < 0)
    check "wip acknowledgment does not mention starting a reviewer" (ackLower.IndexOf("starting a reviewer") < 0)

    let allSpecs = all
    let names = allSpecs |> List.map (fun spec -> spec.name) |> Set.ofList
    check "catalog covers coder" (Set.contains "coder" names)
    check "catalog covers investigator" (Set.contains "investigator" names)
    check "catalog covers meditator" (Set.contains "meditator" names)
    check "catalog covers browser" (Set.contains "browser" names)
    check "catalog covers executor" (Set.contains "executor" names)
    check "catalog covers submit_review" (Set.contains "submit_review" names)
