module Wanxiangshu.Tests.SubagentPromptsTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Kernel.SubagentPrompts

let private sampleCoderIntent: CoderIntent =
    { objective = "Add auth"
      background = "Need JWT"
      targets = [ { file = "src/Auth.fs"; guide = "Add login"; draft = None } ]
      doNotTouch = [| "tests/" |] }

let private sampleInvestigatorIntent: InvestigatorIntent =
    { objective = "Find auth"
      background = "Where is login"
      questions = [| "Where is auth.handler?" |]
      entries = [| "src/" |] }

let coderPromptContainsObjective () =
    let p = coderPrompt sampleCoderIntent
    check "contains objective" (p.Contains "Add auth")
let coderPromptContainsDoNotTouch () =
    let p = coderPrompt sampleCoderIntent
    check "contains do_not_touch" (p.Contains "tests/")
let coderPromptContainsYaml () =
    let p = coderPrompt sampleCoderIntent
    check "contains yaml field" (p.Contains "objective:")
let coderPromptEmptyDoNotTouchOmitsField () =
    let intent = { sampleCoderIntent with doNotTouch = [||] }
    let p = coderPrompt intent
    check "omits do_not_touch when empty" (not (p.Contains "do_not_touch"))

let investigatorPromptContainsQuestions () =
    let p = investigatorPrompt sampleInvestigatorIntent
    check "contains questions" (p.Contains "Where is auth.handler?")
let investigatorPromptContainsYaml () =
    let p = investigatorPrompt sampleInvestigatorIntent
    check "contains yaml" (p.Contains "objective:")

let browserPromptContainsTask () =
    let p = browserPrompt "Search for docs"
    check "contains task" (p.Contains "Search for docs")
let browserPromptContainsStealth () =
    let p = browserPrompt "Search"
    check "contains stealth" (p.Contains "stealth-browser")

let meditatorPromptContainsFiles () =
    let sections = [ { file = "src/Auth.fs"; content = Some "let x = 1" } ]
    let p = meditatorPrompt sections "Analyze auth"
    check "contains file path" (p.Contains "src/Auth.fs")
    check "contains question" (p.Contains "Analyze auth")
    check "contains content" (p.Contains "let x = 1")

let meditatorPromptSkippedSection () =
    let sections = [ { file = "src/Auth.fs"; content = None } ]
    let p = meditatorPrompt sections "Analyze"
    check "contains skipped" (p.Contains meditatorSkippedSection)

let executorSummarizerPromptContainsFields () =
    let p = executorSummarizerPrompt "output" "stdout" "python" "print(1)" [] "short" "ro"
    check "contains language" (p.Contains "python")
    check "contains program" (p.Contains "print(1)")
    check "contains mode" (p.Contains "ro")

let websearchSummarizerPromptContains () =
    let p = websearchSummarizerPrompt "Q?" "results..."
    check "contains question" (p.Contains "Q?")
    check "contains raw_results" (p.Contains "raw_results")
    check "contains raw output" (p.Contains "results...")

let run () =
    coderPromptContainsObjective ()
    coderPromptContainsDoNotTouch ()
    coderPromptContainsYaml ()
    coderPromptEmptyDoNotTouchOmitsField ()
    investigatorPromptContainsQuestions ()
    investigatorPromptContainsYaml ()
    browserPromptContainsTask ()
    browserPromptContainsStealth ()
    meditatorPromptContainsFiles ()
    meditatorPromptSkippedSection ()
    executorSummarizerPromptContainsFields ()
    websearchSummarizerPromptContains ()
