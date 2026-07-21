module Wanxiangshu.Runtime.ReviewPrompts.Submission

open Wanxiangshu.Runtime.LoopMessages
open Wanxiangshu.Runtime.PromptHeader
open Wanxiangshu.Runtime.ReviewPrompts.Instructions

let doubleCheckChallenge =
    "Nope, let's re-evaluate: does it really fully satisfy the original task without cutting corners?"

let doubleCheckPrompt (task: string) : string =
    let taskLine =
        if task <> "" then
            [ yamlField originalTaskField task ]
        else
            []

    frontMatterPrompt
        ([ yamlField doubleCheckField doubleCheckChallenge ] @ taskLine)
        "If you insist on PERFECT, otherwise please use REVISE with detailed feedback."

let reviewerPrompt (task: string) (report: string) (affectedFiles: string list) : string =
    let taskLine =
        if task <> "" then
            [ yamlField originalTaskField task ]
        else
            []

    let filesLine =
        if affectedFiles.Length > 0 then
            [ yamlStringSeqField "affected_files" affectedFiles ]
        else
            []

    let body =
        if System.String.IsNullOrEmpty report then
            reviewInstructionsProse
        else
            reviewInstructionsProse + "\n\n# Worker Report\n\n" + report

    frontMatterPrompt (taskLine @ filesLine) body

let private reviewSubmissionFields
    (task: string)
    (report: string)
    (affectedFiles: string list)
    : FrontMatterField list =
    let taskLine =
        if task <> "" then
            [ yamlField originalTaskField task ]
        else
            []

    let filesLine =
        if affectedFiles.Length > 0 then
            [ yamlStringSeqField "affected_files" affectedFiles ]
        else
            []

    let reportLine =
        if System.String.IsNullOrEmpty report then
            []
        else
            [ yamlField "report" report ]

    taskLine @ filesLine @ reportLine

let reviewSubmissionVerdictPrompt (task: string) (report: string) (affectedFiles: string list) : string =
    frontMatterPrompt (reviewSubmissionFields task report affectedFiles) reviewSubmissionVerdictBody

let reviewSubmissionDoubleCheckPrompt (task: string) (report: string) (affectedFiles: string list) : string =
    let fields =
        [ yamlField doubleCheckField doubleCheckChallenge ]
        @ reviewSubmissionFields task report affectedFiles

    frontMatterPrompt fields reviewSubmissionVerdictBody

let returnReviewerVerdictSubmittedMessage: string =
    "Verdict submitted.\n\nPlease stop the session immediately."
