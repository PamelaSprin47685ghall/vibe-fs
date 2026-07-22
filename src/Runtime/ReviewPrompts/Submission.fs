module Wanxiangshu.Runtime.ReviewPrompts.Submission

open Wanxiangshu.Kernel.Prompt
open Wanxiangshu.Runtime.Prompt
open Wanxiangshu.Runtime.PromptFragments

let doubleCheckChallenge =
    "Nope, let's re-evaluate: does it really fully satisfy the original task without cutting corners?"

let doubleCheckPrompt (task: string) : string =
    let objText =
        if System.String.IsNullOrWhiteSpace task then
            "Re-evaluate the submission."
        else
            task

    let docView =
        { objective = objText
          background = Some doubleCheckChallenge
          agentRole = AgentRole.CodeReview
          targets = []
          boundaries = [ PromptBoundary.DoNotModify(BoundaryTarget.Directory ".") ]
          rules =
            [ PromptRule.Constraint readOnlyWorkspaceConstraint
              PromptRule.Policy "Re-evaluate: does it really fully satisfy the original task without cutting corners?"
              PromptRule.Contract "If you insist on PERFECT, otherwise please use REVISE with detailed feedback." ]
          outcomes =
            [ { label = "PERFECT"
                text = "Confirm acceptance after re-evaluation." }
              { label = "REVISE"
                text = "Request revision with detailed feedback." } ] }

    match PromptDocument.create docView with
    | Ok doc -> PromptToml.render doc
    | Error errs -> failwithf "Failed to create doubleCheckPrompt doc: %A" errs

let reviewerPrompt (task: string) (report: string) (affectedFiles: string list) : string =
    let objText =
        if System.String.IsNullOrWhiteSpace task then
            "Perform a code review of the submitted work."
        else
            task

    let mutable targetsList = []

    if not (System.String.IsNullOrWhiteSpace report) then
        targetsList <- targetsList @ [ PromptTarget.EvidenceTarget("worker_report", report) ]

    if not (List.isEmpty affectedFiles) then
        for f in affectedFiles do
            targetsList <- targetsList @ [ PromptTarget.FileReference f ]

    let docView =
        { objective = objText
          background = Some "You are a code reviewer performing a rigorous review of submitted work."
          agentRole = AgentRole.CodeReview
          targets = targetsList
          boundaries = [ PromptBoundary.DoNotModify(BoundaryTarget.Directory ".") ]
          rules =
            [ PromptRule.Constraint readOnlyWorkspaceConstraint
              PromptRule.Criterion reviewCriteria
              PromptRule.Policy
                  "Based on the original task, change report, and affected files, read and inspect actual file contents before making your judgment."
              PromptRule.Contract
                  "You MUST call return_reviewer before finishing. Do not end the conversation without submitting your verdict." ]
          outcomes =
            [ { label = "PERFECT"
                text = "Accept submission without required changes (or with minor suggestions)." }
              { label = "REVISE"
                text = "Reject submission and request revision with detailed, actionable feedback." } ] }

    match PromptDocument.create docView with
    | Ok doc -> PromptToml.render doc
    | Error errs -> failwithf "Failed to create reviewerPrompt doc: %A" errs

let reviewSubmissionVerdictPrompt (task: string) (report: string) (affectedFiles: string list) : string =
    let objText =
        if System.String.IsNullOrWhiteSpace task then
            "Perform a code review and submit verdict."
        else
            task

    let mutable targetsList = []

    if not (System.String.IsNullOrWhiteSpace report) then
        targetsList <- targetsList @ [ PromptTarget.EvidenceTarget("worker_report", report) ]

    if not (List.isEmpty affectedFiles) then
        for f in affectedFiles do
            targetsList <- targetsList @ [ PromptTarget.FileReference f ]

    let docView =
        { objective = objText
          background = Some "You are a code reviewer performing a rigorous review of submitted work."
          agentRole = AgentRole.CodeReview
          targets = targetsList
          boundaries = [ PromptBoundary.DoNotModify(BoundaryTarget.Directory ".") ]
          rules =
            [ PromptRule.Constraint readOnlyWorkspaceConstraint
              PromptRule.Criterion reviewCriteria
              PromptRule.Contract "Call the agent_report tool to submit your verdict." ]
          outcomes =
            [ { label = "PERFECT"
                text = "Accept submission." }
              { label = "REVISE"
                text = "Request revision with detailed feedback." } ] }

    match PromptDocument.create docView with
    | Ok doc -> PromptToml.render doc
    | Error errs -> failwithf "Failed to create reviewSubmissionVerdictPrompt doc: %A" errs

let reviewSubmissionDoubleCheckPrompt (task: string) (report: string) (affectedFiles: string list) : string =
    let objText =
        if System.String.IsNullOrWhiteSpace task then
            "Re-evaluate submission and submit verdict."
        else
            task

    let mutable targetsList = []

    if not (System.String.IsNullOrWhiteSpace report) then
        targetsList <- targetsList @ [ PromptTarget.EvidenceTarget("worker_report", report) ]

    if not (List.isEmpty affectedFiles) then
        for f in affectedFiles do
            targetsList <- targetsList @ [ PromptTarget.FileReference f ]

    let docView =
        { objective = objText
          background = Some doubleCheckChallenge
          agentRole = AgentRole.CodeReview
          targets = targetsList
          boundaries = [ PromptBoundary.DoNotModify(BoundaryTarget.Directory ".") ]
          rules =
            [ PromptRule.Constraint readOnlyWorkspaceConstraint
              PromptRule.Criterion reviewCriteria
              PromptRule.Policy "Re-evaluate: does it really fully satisfy the original task without cutting corners?"
              PromptRule.Contract
                  "Call agent_report with PERFECT if you insist on PERFECT, otherwise REVISE with detailed feedback." ]
          outcomes =
            [ { label = "PERFECT"
                text = "Confirm acceptance after re-evaluation." }
              { label = "REVISE"
                text = "Request revision with detailed feedback." } ] }

    match PromptDocument.create docView with
    | Ok doc -> PromptToml.render doc
    | Error errs -> failwithf "Failed to create reviewSubmissionDoubleCheckPrompt doc: %A" errs

let returnReviewerVerdictSubmittedMessage: string =
    "Verdict submitted.\n\nPlease stop the session immediately."
