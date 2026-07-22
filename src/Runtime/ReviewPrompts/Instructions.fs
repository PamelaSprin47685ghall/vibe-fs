module Wanxiangshu.Runtime.ReviewPrompts.Instructions

open Wanxiangshu.Kernel.Prompt
open Wanxiangshu.Runtime.Prompt
open Wanxiangshu.Runtime.PromptFragments

let reviewInstructionsProse =
    let docView =
        { objective = "Perform a rigorous code review of submitted work."
          background = Some "You are a code reviewer performing a rigorous review of submitted work."
          agentRole = AgentRole.CodeReview
          targets = []
          boundaries = [ PromptBoundary.DoNotModify(BoundaryTarget.Directory ".") ]
          rules =
            [ PromptRule.Constraint readOnlyWorkspaceConstraint
              PromptRule.Criterion reviewCriteria
              PromptRule.Policy
                  "Based on the original task, change report, and affected files, read and inspect the actual file contents before making your judgment. The original task is the authoritative requirement — verify that the implementation satisfies it, not just that it matches the self-reported change report."
              PromptRule.Contract
                  "You MUST call return_reviewer before finishing. Do not end the conversation without submitting your verdict." ]
          outcomes =
            [ { label = "PERFECT"
                text = "Accept submission without required changes (or with minor suggestions)." }
              { label = "REVISE"
                text = "Reject submission and request revision with detailed, actionable feedback." } ] }

    match PromptDocument.create docView with
    | Ok doc -> PromptToml.render doc
    | Error errs -> failwithf "Failed to create reviewInstructionsProse doc: %A" errs

let reviewInstructions = reviewInstructionsProse

let reviewerVerdictPrologue (subject: string) =
    let docView =
        { objective = $"Evaluate {subject}."
          background = Some $"You are a reviewer evaluating {subject}."
          agentRole = AgentRole.CodeReview
          targets = []
          boundaries = [ PromptBoundary.DoNotModify(BoundaryTarget.Directory ".") ]
          rules =
            [ PromptRule.Constraint readOnlyWorkspaceConstraint
              PromptRule.Criterion reviewCriteria
              PromptRule.Contract
                  "Call the agent_report tool to submit your verdict. Use verdict PERFECT if acceptable, REVISE otherwise." ]
          outcomes =
            [ { label = "PERFECT"
                text = "Accept submission." }
              { label = "REVISE"
                text = "Request revision with detailed, actionable feedback." } ] }

    match PromptDocument.create docView with
    | Ok doc -> PromptToml.render doc
    | Error errs -> failwithf "Failed to create reviewerVerdictPrologue doc: %A" errs

let agentReportVerdictInstructions (acceptMeaning: string) =
    let docView =
        { objective = "Submit review verdict using the agent_report tool."
          background = Some "Call agent_report tool to submit your verdict."
          agentRole = AgentRole.CodeReview
          targets = []
          boundaries = [ PromptBoundary.DoNotModify(BoundaryTarget.Directory ".") ]
          rules =
            [ PromptRule.Constraint readOnlyWorkspaceConstraint
              PromptRule.Contract $"Verdict MUST be PERFECT if {acceptMeaning}, REVISE otherwise." ]
          outcomes =
            [ { label = "PERFECT"
                text = "Accept submission with optional feedback." }
              { label = "REVISE"
                text = "Request revision with detailed feedback." } ] }

    match PromptDocument.create docView with
    | Ok doc -> PromptToml.render doc
    | Error errs -> failwithf "Failed to create agentReportVerdictInstructions doc: %A" errs

let reviewSubmissionVerdictBody =
    let docView =
        { objective = "Perform a rigorous code review of submitted work and submit verdict using agent_report tool."
          background = Some "You are a code reviewer performing a rigorous review of submitted work."
          agentRole = AgentRole.CodeReview
          targets = []
          boundaries = [ PromptBoundary.DoNotModify(BoundaryTarget.Directory ".") ]
          rules =
            [ PromptRule.Constraint readOnlyWorkspaceConstraint
              PromptRule.Criterion reviewCriteria
              PromptRule.Policy
                  "Based on the original task, change report, and affected files, inspect file contents. The original task is authoritative."
              PromptRule.Contract
                  "Call agent_report with verdict PERFECT if current implementation is complete and correct, REVISE otherwise." ]
          outcomes =
            [ { label = "PERFECT"
                text = "Accept submission." }
              { label = "REVISE"
                text = "Request revision with detailed feedback." } ] }

    match PromptDocument.create docView with
    | Ok doc -> PromptToml.render doc
    | Error errs -> failwithf "Failed to create reviewSubmissionVerdictBody doc: %A" errs

let agentReportReviewInstructions =
    let docView =
        { objective = "Perform a rigorous code review of submitted work and report result via structured output."
          background = Some "You are a code reviewer performing a rigorous review of submitted work."
          agentRole = AgentRole.CodeReview
          targets = []
          boundaries = [ PromptBoundary.DoNotModify(BoundaryTarget.Directory ".") ]
          rules =
            [ PromptRule.Constraint readOnlyWorkspaceConstraint
              PromptRule.Criterion reviewCriteria
              PromptRule.Policy "Inspect actual file contents against original task."
              PromptRule.Contract "Call agent_report with reportMarkdown starting with PERFECT or REVISE." ]
          outcomes =
            [ { label = "PERFECT"
                text = "Accept submission." }
              { label = "REVISE"
                text = "Request revision with detailed feedback." } ] }

    match PromptDocument.create docView with
    | Ok doc -> PromptToml.render doc
    | Error errs -> failwithf "Failed to create agentReportReviewInstructions doc: %A" errs

let muxReviewerAgentReportDescription =
    "Submit a review verdict. Provide verdict and feedback; the wrapper forwards the verdict as the upstream agent_report markdown."

module ReviewerVerdictPrompts =
    let reviewerVerdictInstructions =
        reviewerVerdictPrologue "whether the reported changes satisfy the original task"

    let loopReviewVerdictInstructions =
        let docView =
            { objective = "Evaluate whether task description is clear and actionable enough to begin work."
              background = Some "You are a reviewer evaluating whether a task description is clear and actionable."
              agentRole = AgentRole.CodeReview
              targets = []
              boundaries = [ PromptBoundary.DoNotModify(BoundaryTarget.Directory ".") ]
              rules =
                [ PromptRule.Contract
                      "Call agent_report with verdict PERFECT if clear and actionable, REVISE otherwise." ]
              outcomes =
                [ { label = "PERFECT"
                    text = "Task is clear and actionable." }
                  { label = "REVISE"
                    text = "Request task clarification or revision." } ] }

        match PromptDocument.create docView with
        | Ok doc -> PromptToml.render doc
        | Error errs -> failwithf "Failed to create loopReviewVerdictInstructions doc: %A" errs
