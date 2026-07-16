module Wanxiangshu.Tests.SubagentPromptsTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Runtime.SubagentPrompts

let private sampleCoderIntent: CoderIntent =
    { objective = "Add auth"
      background = "Need JWT"
      targets =
        [ { file = "src/Auth.fs"
            guide = "Add login"
            draft = None } ]
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
    let intent =
        { sampleCoderIntent with
            doNotTouch = [||] }

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

let meditatorPromptContainsQuestion () =
    let dummyEntry: Wanxiangshu.Kernel.Methodology.Schema.MethodologyEntry =
        { methodologyId = "test_methodology"
          shortDefinition = "test def"
          triggerWhen = "test trigger"
          noteDescription = "test note desc"
          meditatorRole = "test role"
          outputSections = [] }

    let p =
        Wanxiangshu.Kernel.Methodology.Schema.renderMeditatorIntent
            dummyEntry
            "Analyze auth"
            "JWT background"
            "note detail"

    check "contains question/intent" (p.Contains "Analyze auth")
    check "contains background" (p.Contains "JWT background")
    check "contains quiet room" (p.Contains "quiet room")

let executorSummarizerPromptContainsFields () =
    let p =
        executorSummarizerPrompt "output" "stdout" "python" "print(1)" [] "short" "ro"

    check "contains language" (p.Contains "python")
    check "contains program" (p.Contains "print(1)")
    check "contains mode" (p.Contains "ro")
    check "contains task section" (p.Contains "# Task")
    check "contains preserve directive" (p.Contains "Preserve errors")

let executorSummarizerPromptEmbedsWhatToSummarize () =
    let p =
        executorSummarizerPrompt "summarize exit codes and stderr only" "stdout" "python" "print(1)" [] "short" "ro"

    check "what_to_summarize embedded" (p.Contains "summarize exit codes and stderr only")
    check "contains task section" (p.Contains "# Task")

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
    meditatorPromptContainsQuestion ()
    executorSummarizerPromptContainsFields ()
    executorSummarizerPromptEmbedsWhatToSummarize ()
    websearchSummarizerPromptContains ()
