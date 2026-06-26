module Wanxiangshu.Tests.ReviewTestsPrompts

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.LoopMessages
open Wanxiangshu.Kernel.PromptFrontMatter

let doubleCheckAnchorReplay () =
    check "empty history -> no anchor" (not (hasDoubleCheckAnchor []))
    check "plain prose -> no anchor" (not (hasDoubleCheckAnchor [ "just a message"; "another" ]))
    let prompt = Wanxiangshu.Kernel.ReviewPrompts.doubleCheckPrompt "ship feature X"
    check "double-check prompt carries anchor" (hasDoubleCheckAnchor [ prompt ])
    check "anchor survives mixed history" (hasDoubleCheckAnchor [ "earlier msg"; prompt; "later msg" ])

let doubleCheckPromptFormat () =
    let prompt = Wanxiangshu.Kernel.ReviewPrompts.doubleCheckPrompt "build the login page"
    check "has front-matter fence" (prompt.Contains "---")
    check "has double-check field" (prompt.Contains "double-check:")
    check "embeds task" (prompt.Contains "build the login page")
    check "asks for re-submission" (prompt.Contains "REJECT with detailed feedback")
    let multiline = Wanxiangshu.Kernel.ReviewPrompts.doubleCheckPrompt "task with\nnewline and ### markdown"
    check "multiline original_task uses block field" (multiline.Contains "original_task: |")
    let parsed = parseFrontMatterScalars multiline
    equal "multiline original_task round-trips" (Some "task with\nnewline and ### markdown") (Map.tryFind "original_task" parsed)

let reviewerPromptFormat () =
    let prompt = Wanxiangshu.Kernel.ReviewPrompts.reviewerPrompt "ship S1" "changed A and B" [ "a.fs"; "b.fs" ]
    check "has front-matter fence" (prompt.Contains "---")
    check "embeds original_task in front matter" (prompt.Contains "original_task:" && prompt.Contains "ship S1")
    check "lists affected files in front-matter" (prompt.Contains "affected_files:")
    check "embeds affected file a.fs" (prompt.Contains "a.fs")
    check "carries review criteria" (prompt.Contains "# Evaluation Criteria")
    check "worker report is markdown body" (prompt.Contains "# Worker Report")
    check "embeds report content" (prompt.Contains "changed A and B")
    check "no ugly Task header" (not (prompt.Contains "=== Task ==="))
    check "no ugly Change Report header" (not (prompt.Contains "=== Change Report ==="))
    check "no change_report front-matter field" (not (prompt.Contains "change_report:"))
    let minimal = Wanxiangshu.Kernel.ReviewPrompts.reviewerPrompt "only task" "" []
    check "minimal prompt embeds task" (minimal.Contains "only task")
    check "minimal prompt has no worker report section" (not (minimal.Contains "# Worker Report"))
    check "minimal prompt omits affected_files when empty" (not (minimal.Contains "affected_files:"))
    let multilineTask = "Line one of task\nLine two with ### markdown\nLine three"
    let mp = Wanxiangshu.Kernel.ReviewPrompts.reviewerPrompt multilineTask "" []
    let parsed = parseFrontMatterScalars mp
    equal "multiline original_task round-trips through front-matter" (Some multilineTask) (Map.tryFind "original_task" parsed)

let muxReviewerVerdictPromptFormat () =
    let prompt = Wanxiangshu.Kernel.ReviewPrompts.reviewSubmissionVerdictPrompt "ship S1" "changed A and B" [ "a.fs"; "b.fs" ]
    check "mux prompt starts with front-matter" (prompt.StartsWith "---")
    check "mux prompt carries reviewer role" (prompt.Contains "role: reviewer")
    check "mux prompt has no call_id field" (not (prompt.Contains "call_id:"))
    check "mux prompt carries original_task" (prompt.Contains "original_task:" && prompt.Contains "ship S1")
    check "mux prompt carries affected_files" (prompt.Contains "affected_files:")
    check "mux prompt carries report" (prompt.Contains "report:" && prompt.Contains "changed A and B")
    check "mux prompt reuses review criteria" (prompt.Contains "# Evaluation Criteria")
    check "mux prompt names agent_report" (prompt.Contains "agent_report")
    check "mux prompt does not mention return_reviewer" (not (prompt.Contains "return_reviewer"))
    check "mux prompt has no legacy divider" (not (prompt.Contains "==="))

let muxPreReviewVerdictPromptFormat () =
    let prompt = Wanxiangshu.Kernel.ReviewPrompts.preReviewVerdictPrompt "clarify rollout"
    check "pre-review prompt starts with front-matter" (prompt.StartsWith "---")
    check "pre-review prompt carries reviewer role" (prompt.Contains "role: reviewer")
    check "pre-review prompt has no call_id field" (not (prompt.Contains "call_id:"))
    check "pre-review prompt carries original_task" (prompt.Contains "original_task:" && prompt.Contains "clarify rollout")
    check "pre-review prompt reuses review criteria" (prompt.Contains "# Evaluation Criteria")
    check "pre-review prompt names agent_report" (prompt.Contains "agent_report")
    check "pre-review prompt has no legacy divider" (not (prompt.Contains "==="))

let reviewInstructionsFrontMatter () =
    let instr = Wanxiangshu.Kernel.ReviewPrompts.reviewInstructions
    check "instructions wrapped in front-matter" (instr.StartsWith "---")
    check "instructions carry role" (instr.Contains "role: reviewer")
    check "instructions carry review criteria" (instr.Contains "# Evaluation Criteria")
    check "instructions mention return_reviewer" (instr.Contains "return_reviewer")