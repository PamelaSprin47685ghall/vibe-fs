module Wanxiangshu.Tests.KernelPromptSpecsHost

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.SubagentPrompts
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Kernel.ReviewPrompts
open Wanxiangshu.Kernel.ReviewPrompts.Instructions

let hostKernel' () =
    let coderIntent =
        { objective = "fix bug"
          background = "user reported failure"
          targets = [ { file = "a.ts"; guide = "fix root cause"; draft = None }; { file = "b.ts"; guide = "align types"; draft = None } ]
          doNotTouch = [| "shared.ts" |] }
    let intent = coderPrompt coderIntent
    check "coder has file" (intent.IndexOf("a.ts") >= 0)
    check "coder has objective" (intent.IndexOf("fix bug") >= 0)
    check "coder has do_not_touch" (intent.IndexOf("shared.ts") >= 0)
    let prompt = meditatorPrompt [ { file = "x.fs"; content = Some "let x = 1" } ] "why?"
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
    check "coder requires intents+tdd+warn_tdd" (coderSpec.requiredFields = [ "intents"; "tdd"; "warn_tdd" ])
    check "coder param doc for intents" (Map.containsKey "intents" coderSpec.paramDocs)
    check "coder param doc for tdd" (Map.containsKey "tdd" coderSpec.paramDocs)

    let investigatorSpec = specOf "investigator"
    check "investigator spec carries description" (investigatorSpec.description.Length > 0)
    check "investigator requires intents only" (investigatorSpec.requiredFields = [ "intents" ])

    let executorSpec = specOf "executor"
    check "executor describes timeout budgets" (executorSpec.description.Contains "timeout")
    check "executor requires mode" (executorSpec.requiredFields |> List.contains "mode")
    check "executor param doc for mode" (Map.containsKey "mode" executorSpec.paramDocs)

    let fetchSpec = specOf "knowledge_graph_fetch"
    check "fetch knowledge graph description mentions snapshot" (fetchSpec.description.Contains "session's knowledge graph snapshot")
    check "fetch knowledge graph requires entity" (fetchSpec.requiredFields = [ "entity" ])

    let submitSpec = specOf "return_bookkeeper"
    check "return bookkeeper description mentions knowledge graph" (submitSpec.description.Contains "knowledge graph")
    check "return bookkeeper requires entries" (submitSpec.requiredFields = [ "entries" ])

    let submitReviewSpec = specOf "submit_review"
    check "submit_review spec carries description" (submitReviewSpec.description.Length > 0)
    check "submit_review param doc for wip" (Map.containsKey "wip" submitReviewSpec.paramDocs)
    let wipDoc =
        match Map.tryFind "wip" submitReviewSpec.paramDocs with
        | Some doc -> doc
        | None -> ""
    let wipLower = wipDoc.ToLowerInvariant()
    check "wip param doc does not mention starting a reviewer" (wipLower.IndexOf("starting a reviewer") < 0)
    check "wip param doc does not mention start the reviewer" (wipLower.IndexOf("start the reviewer") < 0)
    check "wip param doc does not mention skip" (wipLower.IndexOf("skip") < 0)
    check "wip param doc mentions partial or not fully complete"
        (wipLower.IndexOf("partial") >= 0 || wipLower.IndexOf("not fully complete") >= 0)

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
    check "catalog covers knowledge_graph_fetch" (Set.contains "knowledge_graph_fetch" names)
    check "catalog covers return_bookkeeper" (Set.contains "return_bookkeeper" names)
    check "catalog covers submit_review" (Set.contains "submit_review" names)

let hostToolsKnowledgeGraphNames () =
    let names = allToolNames opencode |> Set.ofArray
    check "host tools include knowledge_graph_fetch" (Set.contains "knowledge_graph_fetch" names)
    check "host tools include return_bookkeeper" (Set.contains "return_bookkeeper" names)