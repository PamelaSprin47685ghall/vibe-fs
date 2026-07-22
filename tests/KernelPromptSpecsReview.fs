module Wanxiangshu.Tests.KernelPromptSpecsReview

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.LoopMessages
open Wanxiangshu.Runtime.ReviewPrompts
open Wanxiangshu.Runtime.ReviewPrompts.Instructions
open Wanxiangshu.Kernel.ReviewSession
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Kernel.ReviewVerdict
open Wanxiangshu.Runtime.SubagentPrompts
open Wanxiangshu.Runtime.SubagentSummarizerPrompts
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality

let loopMessagesShared () =
    let task = "ship S1 refactor"

    let intro =
        "With-Review Mode is active. Complete the task above, then call submit_review with:"

    let kernelMsg = buildLoopMessage task [ intro ]
    check "loop message carries objective" (kernelMsg.Contains "objective =")
    check "loop message embeds task" (kernelMsg.Contains task)
    check "loop message embeds intro" (kernelMsg.Contains intro)
    check "loop message mentions submit_review" (kernelMsg.Contains "submit_review")
    check "loop message lists report field" (kernelMsg.Contains "report")
    check "loop message lists affectedFiles field" (kernelMsg.Contains "affectedFiles")
    check "loop message names reviewer" (kernelMsg.Contains "reviewer")

    let multilineTask = "ship S1 refactor\ninclude follow-up cleanup"
    let multilineMsg = buildLoopMessage multilineTask [ intro ]
    check "loop message multiline task" (multilineMsg.Contains "ship S1 refactor")

    let loopTemplate = withReviewCommandTemplate
    check "loop template carries objective" (loopTemplate.Contains "objective =")
    check "loop template carries task placeholder" (loopTemplate.Contains "$ARGUMENTS")
    check "loop template mentions submit_review" (loopTemplate.Contains "submit_review")
    check "loop template forbids finishing early" (loopTemplate.Contains "Do not end the conversation")

let reviewerVerdictPromptsShared () =
    let verdict = ReviewerVerdictPrompts.reviewerVerdictInstructions
    check "reviewer verdict mentions agent_report" (verdict.Contains "agent_report")
    check "reviewer verdict mentions PERFECT" (verdict.Contains "PERFECT")
    check "reviewer verdict mentions REVISE" (verdict.Contains "REVISE")
    check "reviewer verdict mentions feedback" (verdict.Contains "feedback")

let reviewResultFormattingShared () =
    let accepted = formatReviewResult (ReviewResult.Accepted [ "tighten tests" ])

    check "accepted text omits PERFECT" (not (accepted.Contains "PERFECT"))
    check "accepted text omits Review passed" (not (accepted.Contains "Review passed"))
    check "accepted has follow_through outcome" (accepted.Contains "follow_through")
    check "accepted has recommendation outcome" (accepted.Contains "recommendation_")
    check "accepted embeds reviewer opinion" (accepted.Contains "tighten tests")
    check "accepted signals review_mode ended" (accepted.Contains "review_mode" && accepted.Contains "ended")

    let needsRevision = formatReviewResult (ReviewResult.NeedsRevision [ "missing tests" ])
    check "needs_revision has verdict outcome" (needsRevision.Contains "needs_revision")
    check "needs_revision has feedback outcome" (needsRevision.Contains "feedback_")
    check "needs_revision embeds feedback text" (needsRevision.Contains "missing tests")

    let terminated = formatReviewResult ReviewResult.Terminated
    check "terminated has verdict outcome" (terminated.Contains "terminated")
    check "terminated keeps review_mode active" (terminated.Contains "review_mode" && terminated.Contains "active")

let domainErrorsShared () =
    let err1 = DomainError.ExecutorExecutableMissing "npm"
    let err2 = DomainError.ParseError("json", "missing bracket")
    let err3 = DomainError.ToolNotPermitted("coder", "bash")
    let err4 = DomainError.InvalidIntent("coder", "tdd", "unknown phase")
    let err5 = DomainError.UpstreamTimeout 30
    let err6 = DomainError.UpstreamRefused "rate limit"

    check
        "err1 is executable missing"
        (match err1 with
         | DomainError.ExecutorExecutableMissing "npm" -> true
         | _ -> false)

    check
        "err2 is parse error"
        (match err2 with
         | DomainError.ParseError("json", "missing bracket") -> true
         | _ -> false)

    check
        "err3 is tool not permitted"
        (match err3 with
         | DomainError.ToolNotPermitted("coder", "bash") -> true
         | _ -> false)

    check
        "err4 is invalid intent"
        (match err4 with
         | DomainError.InvalidIntent("coder", "tdd", "unknown phase") -> true
         | _ -> false)

    check
        "err5 is upstream timeout"
        (match err5 with
         | DomainError.UpstreamTimeout 30 -> true
         | _ -> false)

    check
        "err6 is upstream refused"
        (match err6 with
         | DomainError.UpstreamRefused "rate limit" -> true
         | _ -> false)

let reviewVerdictDecode () =
    equal "PERFECT decodes to Perfect" (Some Perfect) (parseVerdict "PERFECT")
    equal "REVISE decodes to Revise" (Some Revise) (parseVerdict "REVISE")
    equal "unknown token is not a verdict" None (parseVerdict "FOO")
    equal "lowercase perfect decodes" (Some Perfect) (parseVerdict "perfect")
    equal "mixed-case Revise decodes" (Some Revise) (parseVerdict "  Revise ")
    equal "string null is not a verdict" None (parseVerdict "null")
    equal "empty is not a verdict" None (parseVerdict "")
    equal "garbage is not a verdict" None (parseVerdict "maybe")

let reviewDecisionPolicy () =
    equal
        "revise finalizes as needs_revision with feedback"
        (Finalize(NeedsRevision [ "missing tests" ]))
        (decideReviewSubmission Revise "missing tests" false)

    // Policy layer still accepts empty feedback; boundary codec rejects REVISE+empty.
    equal
        "policy revise empty feedback finalizes (codec must reject earlier)"
        (Finalize(NeedsRevision []))
        (decideReviewSubmission Revise "" true)

    equal "perfect before double-check asks for re-evaluation" AskDoubleCheck (decideReviewSubmission Perfect "" false)

    equal
        "perfect after double-check finalizes as accepted"
        (Finalize(Accepted []))
        (decideReviewSubmission Perfect "" true)

let reviewMarkdownCodec () =
    check "format perfect is exactly PERFECT" (formatReviewVerdictMarkdown Perfect "" = "PERFECT")
    check "format perfect with feedback" ((formatReviewVerdictMarkdown Perfect "nice style").Contains "nice style")

    check
        "format perfect with feedback starts with PERFECT"
        ((formatReviewVerdictMarkdown Perfect "nice style").StartsWith "PERFECT")

    check "format revise embeds feedback" ((formatReviewVerdictMarkdown Revise "fix the leak").Contains "fix the leak")
    check "format revise starts with REVISE" ((formatReviewVerdictMarkdown Revise "fix the leak").StartsWith "REVISE")
    check "format revise empty feedback placeholder" ((formatReviewVerdictMarkdown Revise "").StartsWith "REVISE")
    equal "parse PERFECT markdown -> Accepted" (Accepted []) (parseReviewReportMarkdown "PERFECT")

    equal
        "parse PERFECT with feedback -> Accepted with feedback"
        (Accepted [ "nice work" ])
        (parseReviewReportMarkdown "PERFECT: nice work")

    equal
        "parse REVISE markdown -> NeedsRevision feedback"
        (NeedsRevision [ "fix the leak" ])
        (parseReviewReportMarkdown "REVISE: fix the leak")

    equal "parse unrecognized markdown -> Terminated" Terminated (parseReviewReportMarkdown "I think it looks fine")
    equal "parse empty markdown -> Terminated" Terminated (parseReviewReportMarkdown "")
    equal "round-trip perfect" (Accepted []) (parseReviewReportMarkdown (formatReviewVerdictMarkdown Perfect ""))

    equal
        "round-trip perfect with feedback"
        (Accepted [ "looks good" ])
        (parseReviewReportMarkdown (formatReviewVerdictMarkdown Perfect "looks good"))

    equal
        "round-trip revise non-empty"
        (NeedsRevision [ "needs work" ])
        (parseReviewReportMarkdown (formatReviewVerdictMarkdown Revise "needs work"))

let executorSummarizerNoExitStatus () =
    let evidence: Wanxiangshu.Kernel.Prompt.ExecutorOutputEvidence =
        { stdout = "raw"
          stderr = None
          exitStatus = "completed"
          exitCode = Some 0
          signal = None
          truncated = false }

    let prompt = executorSummarizerPrompt "" evidence "shell" "echo 1" [] Wanxiangshu.Kernel.Prompt.TimeoutKind.Short
    check "summarizer prompt contains objective" (prompt.Contains "Summarize the executor output")
