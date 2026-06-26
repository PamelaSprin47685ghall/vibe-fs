module VibeFs.Kernel.ReviewPrompts.Submission

open VibeFs.Kernel.LoopMessages
open VibeFs.Kernel.PromptFrontMatter
open VibeFs.Kernel.ReviewPrompts.Instructions

let doubleCheckChallenge =
    "Nope, let's re-evaluate: does it really fully satisfy the original task without cutting corners?"

let doubleCheckPrompt (task: string) : string =
    let taskLine = if task <> "" then [ yamlField originalTaskField task ] else []

    frontMatterPrompt
        ([ yamlField doubleCheckField doubleCheckChallenge ]
         @ taskLine)
        "If you insist on PASS, otherwise please REJECT with detailed feedback."

let private reviewerPromptFrontMatter (fields: string list) : string list =
    [ yamlField "role" "reviewer" ] @ fields

let reviewerPrompt (task: string) (report: string) (affectedFiles: string list) : string =
    let taskLine = if task <> "" then [ yamlField originalTaskField task ] else []

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

let private reviewSubmissionFields (task: string) (report: string) (affectedFiles: string list) : string list =
    let taskLine = if task <> "" then [ yamlField originalTaskField task ] else []

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

let reviewSubmissionVerdictPrompt
    (task: string)
    (report: string)
    (affectedFiles: string list)
    : string =
    frontMatterPrompt (reviewerPromptFrontMatter (reviewSubmissionFields task report affectedFiles)) reviewSubmissionVerdictBody

let reviewSubmissionDoubleCheckPrompt
    (task: string)
    (report: string)
    (affectedFiles: string list)
    : string =
    let fields =
        [ yamlField "role" "reviewer"; yamlField doubleCheckField doubleCheckChallenge ]
        @ reviewSubmissionFields task report affectedFiles

    frontMatterPrompt fields reviewSubmissionVerdictBody

let preReviewVerdictPrompt (task: string) : string =
    let taskLine = if task <> "" then [ yamlField originalTaskField task ] else []

    frontMatterPrompt (reviewerPromptFrontMatter taskLine) preReviewVerdictBody
