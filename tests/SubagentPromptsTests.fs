module Wanxiangshu.Tests.SubagentPromptsTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Runtime.SubagentPrompts
open Wanxiangshu.Runtime.SubagentSummarizerPrompts

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

let coderPromptContainsToml () =
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

let inspectorPromptContainsToml () =
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
        Wanxiangshu.Runtime.SubagentSummarizerPrompts.renderMeditatorIntent
            dummyEntry
            "Analyze auth"
            "JWT background"
            "note detail"

    check "contains question/intent" (p.Contains "Analyze auth")
    check "contains background" (p.Contains "JWT background")
    check "contains agent_role" (p.Contains "agent_role =")

let private sampleEvidence status exitCode truncated stdout : Wanxiangshu.Kernel.Prompt.ExecutorOutputEvidence =
    { stdout = stdout
      stderr = None
      exitStatus = status
      exitCode = exitCode
      signal = None
      truncated = truncated }

let executorSummarizerPromptContainsFields () =
    let evidence = sampleEvidence "exit_error" (Some 1) false "stdout"

    let p =
        executorSummarizerPrompt "output" evidence "python" "print(1)" [] Wanxiangshu.Kernel.Prompt.TimeoutKind.Short

    check "contains language" (p.Contains "python")
    check "contains program" (p.Contains "print(1)")
    check "contains agent_role" (p.Contains "agent_role =")
    check "contains preserve directive" (p.Contains "Preserve errors")
    check "contains executor_output kind" (p.Contains "executor_output")
    check "contains stdout field" (p.Contains "stdout")
    check "contains real exit_status" (p.Contains "exit_error")
    check "contains exit_code" (p.Contains "exit_code")
    check "no evidence bag label" (not (p.Contains "value = \"executor_output\""))
    check "not hard-coded completed when error" (not (p.Contains "exit_status = \"completed\""))

let executorSummarizerPromptEmbedsWhatToSummarize () =
    let evidence = sampleEvidence "completed" (Some 0) false "stdout"

    let p =
        executorSummarizerPrompt
            "summarize exit codes and stderr only"
            evidence
            "python"
            "print(1)"
            []
            Wanxiangshu.Kernel.Prompt.TimeoutKind.Short

    check "what_to_summarize embedded" (p.Contains "summarize exit codes and stderr only")
    check "contains objective" (p.Contains "objective =")


let run () =
    coderPromptContainsObjective ()
    coderPromptContainsDoNotTouch ()
    coderPromptContainsToml ()
    coderPromptEmptyDoNotTouchOmitsField ()
    inspectorPromptContainsQuestions ()
    inspectorPromptContainsToml ()
    browserPromptContainsTask ()
    browserPromptContainsStealth ()
    meditatorPromptContainsQuestion ()
    executorSummarizerPromptContainsFields ()
    executorSummarizerPromptEmbedsWhatToSummarize ()
