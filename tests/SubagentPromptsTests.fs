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

let private sampleInspectorIntent: InspectorIntent =
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
    check "contains objective field" (p.Contains "objective =")

let coderPromptEmptyDoNotTouchOmitsField () =
    let intent =
        { sampleCoderIntent with
            doNotTouch = [||] }

    let p = coderPrompt intent
    check "omits boundaries when empty" (not (p.Contains "boundaries"))

let inspectorPromptContainsQuestions () =
    let p = inspectorPrompt sampleInspectorIntent
    check "contains questions" (p.Contains "Where is auth.handler?")

let inspectorPromptContainsYaml () =
    let p = inspectorPrompt sampleInspectorIntent
    check "contains objective" (p.Contains "objective =")

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
        Wanxiangshu.Runtime.SubagentPrompts.renderMeditatorIntent
            dummyEntry
            "Analyze auth"
            "JWT background"
            "note detail"

    check "contains question/intent" (p.Contains "Analyze auth")
    check "contains background" (p.Contains "JWT background")
    check "contains agent_role" (p.Contains "agent_role =")

let executorSummarizerPromptContainsFields () =
    let p = executorSummarizerPrompt "output" "stdout" "python" "print(1)" [] "short"

    check "contains language" (p.Contains "python")
    check "contains program" (p.Contains "print(1)")
    check "contains agent_role" (p.Contains "agent_role =")
    check "contains preserve directive" (p.Contains "Preserve errors")

let executorSummarizerPromptEmbedsWhatToSummarize () =
    let p =
        executorSummarizerPrompt "summarize exit codes and stderr only" "stdout" "python" "print(1)" [] "short"

    check "what_to_summarize embedded" (p.Contains "summarize exit codes and stderr only")
    check "contains objective" (p.Contains "objective =")

let websearchSummarizerPromptContains () =
    let p = websearchSummarizerPrompt "Q?" "results..."
    check "contains question" (p.Contains "Q?")
    check "contains websearch_results" (p.Contains "websearch_results")
    check "contains raw output" (p.Contains "results...")

let run () =
    coderPromptContainsObjective ()
    coderPromptContainsDoNotTouch ()
    coderPromptContainsYaml ()
    coderPromptEmptyDoNotTouchOmitsField ()
    inspectorPromptContainsQuestions ()
    inspectorPromptContainsYaml ()
    browserPromptContainsTask ()
    browserPromptContainsStealth ()
    meditatorPromptContainsQuestion ()
    executorSummarizerPromptContainsFields ()
    executorSummarizerPromptEmbedsWhatToSummarize ()
    websearchSummarizerPromptContains ()
