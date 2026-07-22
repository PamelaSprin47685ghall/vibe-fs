module Wanxiangshu.Tests.ReviewTestsPrompts

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.LoopMessages

let doubleCheckAnchorReplay () =
    let prompt = Wanxiangshu.Runtime.ReviewPrompts.doubleCheckPrompt "ship feature X"
    check "double-check prompt carries objective" (prompt.Contains "objective =")
    check "double-check prompt carries challenge" (prompt.Contains "re-evaluate")

let doubleCheckPromptFormat () =
    let prompt =
        Wanxiangshu.Runtime.ReviewPrompts.doubleCheckPrompt "build the login page"

    check "has objective" (prompt.Contains "objective =")
    check "embeds task" (prompt.Contains "build the login page")
    check "asks for re-submission" (prompt.Contains "REVISE with detailed feedback")

    let multiline =
        Wanxiangshu.Runtime.ReviewPrompts.doubleCheckPrompt "task with\nnewline and ### markdown"

    check "multiline contains markdown content" (multiline.Contains "task with")

let reviewerPromptFormat () =
    let prompt =
        Wanxiangshu.Runtime.ReviewPrompts.reviewerPrompt "ship S1" "changed A and B" [ "a.fs"; "b.fs" ]

    check "has objective" (prompt.Contains "objective =")
    check "embeds task in objective" (prompt.Contains "ship S1")
    check "embeds affected file a.fs" (prompt.Contains "a.fs")
    check "carries review criteria" (prompt.Contains "Evaluation Criteria" || prompt.Contains "language features")
    check "embeds report content" (prompt.Contains "changed A and B")
    check "no ugly Task header" (not (prompt.Contains "=== Task ==="))
    check "no ugly Change Report header" (not (prompt.Contains "=== Change Report ==="))
    let minimal = Wanxiangshu.Runtime.ReviewPrompts.reviewerPrompt "only task" "" []
    check "minimal prompt embeds task" (minimal.Contains "only task")
    let multilineTask = "Line one of task\nLine two with ### markdown\nLine three"
    let mp = Wanxiangshu.Runtime.ReviewPrompts.reviewerPrompt multilineTask "" []
    check "multiline task in prompt" (mp.Contains "Line one of task")

let muxReviewerVerdictPromptFormat () =
    let prompt =
        Wanxiangshu.Runtime.ReviewPrompts.reviewSubmissionVerdictPrompt "ship S1" "changed A and B" [ "a.fs"; "b.fs" ]

    check "mux prompt has objective" (prompt.Contains "objective =")
    check "mux prompt carries original task" (prompt.Contains "ship S1")
    check "mux prompt carries affected files" (prompt.Contains "a.fs")
    check "mux prompt carries report" (prompt.Contains "changed A and B")

    check
        "mux prompt reuses review criteria"
        (prompt.Contains "Evaluation Criteria" || prompt.Contains "language features")

    check "mux prompt names agent_report" (prompt.Contains "agent_report")
    check "mux prompt has no legacy divider" (not (prompt.Contains "==="))

let reviewInstructionsFrontMatter () =
    let instr = Wanxiangshu.Runtime.ReviewPrompts.reviewInstructions
    check "instructions are review prose" (instr.Contains "You are a code reviewer performing")
    check "instructions mention return_reviewer" (instr.Contains "return_reviewer")
