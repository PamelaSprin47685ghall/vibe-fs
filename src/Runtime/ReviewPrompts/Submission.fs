module Wanxiangshu.Runtime.ReviewPrompts.Submission

open Wanxiangshu.Kernel.Prompt
open Wanxiangshu.Runtime.Prompt
open Wanxiangshu.Runtime.PromptFragments

let doubleCheckChallenge =
    "COMPLETENESS_RECHECK: fully satisfy the original task without cutting corners."

let private reviewBaseRules (extra: PromptRule list) : PromptRule list =
    readOnlyConstraints @ reviewCriteriaRules @ extra

/// Flatten worker report into summary + findings + file references (no opaque bag).
let private evidenceTargets (report: string) (affectedFiles: string list) : PromptTarget list =
    let parsed = Wanxiangshu.Runtime.SubagentReportParse.parseSubagentReportText report

    let summaryTargets =
        match parsed.summary with
        | Some s when s.Trim() <> "" -> [ PromptTarget.EvidenceTarget("summary", s.Trim()) ]
        | _ -> []

    let findingTargets =
        parsed.findings
        |> List.mapi (fun i f -> PromptTarget.EvidenceTarget(sprintf "finding_%d" (i + 1), f))

    let reportFileTargets =
        (parsed.relatedFiles @ affectedFiles)
        |> List.distinct
        |> List.map PromptTarget.FileReference

    let codeTargets =
        parsed.relatedCode
        |> List.mapi (fun i c -> PromptTarget.EvidenceTarget(sprintf "related_code_%d" (i + 1), c))

    summaryTargets @ findingTargets @ reportFileTargets @ codeTargets

let private perfectReviseOutcomes perfectText reviseText : PromptOutcome list =
    [ { label = "PERFECT"
        text = perfectText }
      { label = "REVISE"; text = reviseText } ]

let doubleCheckPrompt (task: string) : string =
    let objText =
        if System.String.IsNullOrWhiteSpace task then
            "Re-evaluate the submission."
        else
            task

    let docView =
        { objective = objText
          background = None
          agentRole = AgentRole.CodeReview
          targets = [ PromptTarget.EvidenceTarget("review_round", "double_check") ]
          boundaries = [ PromptBoundary.DoNotModify(BoundaryTarget.Directory ".") ]
          rules =
            readOnlyConstraints
            @ [ PromptRule.Policy doubleCheckChallenge
                PromptRule.Contract "You MUST call return_reviewer before finishing. Do not end without a verdict."
                PromptRule.Contract "PERFECT only if you still accept; otherwise REVISE with detailed feedback." ]
          outcomes =
            perfectReviseOutcomes "Confirm acceptance after re-evaluation." "Request revision with detailed feedback." }

    match PromptDocument.create docView with
    | Ok doc -> PromptToml.render doc
    | Error errs -> failwithf "Failed to create doubleCheckPrompt doc: %A" errs

let reviewerPrompt (task: string) (report: string) (affectedFiles: string list) : string =
    let objText =
        if System.String.IsNullOrWhiteSpace task then
            "Perform a code review of the submitted work."
        else
            task

    let docView =
        { objective = objText
          background = None
          agentRole = AgentRole.CodeReview
          targets = evidenceTargets report affectedFiles
          boundaries = [ PromptBoundary.DoNotModify(BoundaryTarget.Directory ".") ]
          rules =
            reviewBaseRules
                [ PromptRule.Policy
                      "Read the summary, findings, and affected files; inspect actual contents before judging."
                  PromptRule.Contract "You MUST call return_reviewer before finishing. Do not end without a verdict." ]
          outcomes =
            perfectReviseOutcomes
                "Accept submission without required changes (or with minor suggestions)."
                "Reject submission and request revision with detailed, actionable feedback." }

    match PromptDocument.create docView with
    | Ok doc -> PromptToml.render doc
    | Error errs -> failwithf "Failed to create reviewerPrompt doc: %A" errs

let reviewSubmissionVerdictPrompt (task: string) (report: string) (affectedFiles: string list) : string =
    let objText =
        if System.String.IsNullOrWhiteSpace task then
            "Perform a code review and submit verdict."
        else
            task

    let docView =
        { objective = objText
          background = None
          agentRole = AgentRole.CodeReview
          targets = evidenceTargets report affectedFiles
          boundaries = [ PromptBoundary.DoNotModify(BoundaryTarget.Directory ".") ]
          rules = reviewBaseRules [ PromptRule.Contract "Call agent_report to submit your verdict." ]
          outcomes = perfectReviseOutcomes "Accept submission." "Request revision with detailed feedback." }

    match PromptDocument.create docView with
    | Ok doc -> PromptToml.render doc
    | Error errs -> failwithf "Failed to create reviewSubmissionVerdictPrompt doc: %A" errs

let reviewSubmissionDoubleCheckPrompt (task: string) (report: string) (affectedFiles: string list) : string =
    let objText =
        if System.String.IsNullOrWhiteSpace task then
            "Re-evaluate submission and submit verdict."
        else
            task

    let docView =
        { objective = objText
          background = None
          agentRole = AgentRole.CodeReview
          targets =
            PromptTarget.EvidenceTarget("review_round", "double_check")
            :: evidenceTargets report affectedFiles
          boundaries = [ PromptBoundary.DoNotModify(BoundaryTarget.Directory ".") ]
          rules =
            reviewBaseRules
                [ PromptRule.Policy doubleCheckChallenge
                  PromptRule.Contract
                      "Call agent_report with PERFECT only if still accepting; otherwise REVISE with detailed feedback." ]
          outcomes =
            perfectReviseOutcomes "Confirm acceptance after re-evaluation." "Request revision with detailed feedback." }

    match PromptDocument.create docView with
    | Ok doc -> PromptToml.render doc
    | Error errs -> failwithf "Failed to create reviewSubmissionDoubleCheckPrompt doc: %A" errs

let returnReviewerVerdictSubmittedMessage: string =
    "Verdict submitted.\n\nPlease stop the session immediately."
