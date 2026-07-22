module Wanxiangshu.Runtime.ReviewPrompts.Instructions

open Wanxiangshu.Kernel.Prompt
open Wanxiangshu.Runtime.Prompt
open Wanxiangshu.Runtime.PromptFragments

let private reviewWorkspaceBoundary: PromptBoundary list =
    [ PromptBoundary.DoNotModify(BoundaryTarget.Directory ".") ]

let private perfectRevise: PromptOutcome list =
    [ { label = "PERFECT"
        text = "Accept submission without required changes (or with minor suggestions)." }
      { label = "REVISE"
        text = "Reject submission and request revision with detailed, actionable feedback." } ]

let private perfectReviseShort: PromptOutcome list =
    [ { label = "PERFECT"; text = "Accept submission." }
      { label = "REVISE"; text = "Request revision with detailed, actionable feedback." } ]

let private renderReviewDoc
    (objective: string)
    (targets: PromptTarget list)
    (rules: PromptRule list)
    (outcomes: PromptOutcome list)
    (label: string)
    : string =
    let docView =
        { objective = objective
          background = None
          agentRole = AgentRole.CodeReview
          targets = targets
          boundaries = reviewWorkspaceBoundary
          rules = rules
          outcomes = outcomes }

    match PromptDocument.create docView with
    | Ok doc -> PromptToml.render doc
    | Error errs -> failwithf "Failed to create %s doc: %A" label errs

let private withReviewRules (extra: PromptRule list) : PromptRule list =
    readOnlyConstraints @ reviewCriteriaRules @ extra

/// Canonical review instruction document (TOML PromptDocument, not prose).
let reviewInstructions =
    renderReviewDoc
        "Perform a rigorous code review of submitted work."
        []
        (withReviewRules
            [ PromptRule.Policy
                  "Inspect actual file contents against the original task; the task is authoritative over the self-reported change report."
              PromptRule.Contract "You MUST call return_reviewer before finishing. Do not end without a verdict." ])
        perfectRevise
        "reviewInstructions"

let reviewerVerdictPrologue (subject: string) =
    renderReviewDoc
        $"Evaluate {subject}."
        [ PromptTarget.EvidenceTarget("evaluation_subject", subject) ]
        (withReviewRules
            [ PromptRule.Contract "Call agent_report with verdict PERFECT if acceptable, REVISE otherwise." ])
        perfectReviseShort
        "reviewerVerdictPrologue"

let agentReportVerdictInstructions (acceptMeaning: string) =
    renderReviewDoc
        "Submit review verdict using the agent_report tool."
        []
        (readOnlyConstraints
         @ [ PromptRule.Contract $"Verdict MUST be PERFECT if {acceptMeaning}, REVISE otherwise." ])
        [ { label = "PERFECT"
            text = "Accept submission with optional feedback." }
          { label = "REVISE"
            text = "Request revision with detailed feedback." } ]
        "agentReportVerdictInstructions"

let reviewSubmissionVerdictDocument =
    renderReviewDoc
        "Perform a rigorous code review of submitted work and submit verdict using agent_report tool."
        []
        (withReviewRules
            [ PromptRule.Policy "Inspect file contents against the original task (authoritative)."
              PromptRule.Contract "Call agent_report with PERFECT if complete and correct, REVISE otherwise." ])
        perfectReviseShort
        "reviewSubmissionVerdictDocument"

let agentReportReviewInstructions =
    renderReviewDoc
        "Perform a rigorous code review of submitted work and report result via structured output."
        []
        (withReviewRules
            [ PromptRule.Policy "Inspect actual file contents against original task."
              PromptRule.Contract "Call agent_report with reportMarkdown starting with PERFECT or REVISE." ])
        perfectReviseShort
        "agentReportReviewInstructions"

let muxReviewerAgentReportDescription =
    "Submit a review verdict. Provide verdict and feedback; the wrapper forwards the verdict as the upstream agent_report markdown."

module ReviewerVerdictPrompts =
    let reviewerVerdictInstructions =
        reviewerVerdictPrologue "whether the reported changes satisfy the original task"

    let loopReviewVerdictInstructions =
        renderReviewDoc
            "Evaluate whether task description is clear and actionable enough to begin work."
            [ PromptTarget.EvidenceTarget("evaluation_subject", "task_clarity") ]
            [ PromptRule.Contract "Call agent_report with PERFECT if clear and actionable, REVISE otherwise." ]
            [ { label = "PERFECT"
                text = "Task is clear and actionable." }
              { label = "REVISE"
                text = "Request task clarification or revision." } ]
            "loopReviewVerdictInstructions"
