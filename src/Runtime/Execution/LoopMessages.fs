module Wanxiangshu.Runtime.LoopMessages

open Wanxiangshu.Runtime.PromptHeader

/// Structured front-matter field names and verdict values shared by producers —
/// slash-command activation, the submit_review rendering in `Prompts.formatReviewResult`,
/// and loop cancellation.
let taskField = "task"

/// Worker With-Review activation only. Reviewer prompts carry the parent's
/// requirement under this key so worker loops are cleanly segregated.
let originalTaskField = "original_task"
let verdictField = "verdict"
let verdictAccepted = "accepted"
let verdictNeedsRevision = "needs_revision"
let verdictTerminated = "terminated"
let verdictCancelled = "cancelled"
let commandField = "command"
let commandWithReview = "with-review"

/// Verdicts that END With-Review Mode. needs_revision/terminate keep it active (the work
/// continues), so they are deliberately excluded.
let isEndVerdict = Wanxiangshu.Kernel.Review.ReviewVerdictWire.isEndVerdict

let loopFooter =
    [ "- report: a detailed description of what you did and why"
      "- affectedFiles: list of every file you modified or created"
      "- wip (optional, defaults to true): omit or true while the task is not fully complete; false only when the full task is done"
      ""
      "You must fully complete every item in the task — no shortcuts, no reduced scope, no deferred work."
      "A reviewer will examine your submission. If accepted, you are done. If revision is requested, you will receive specific feedback to address." ]

let buildLoopMessage (task: string) (bodyLines: string list) : string =
    frontMatterPrompt
        [ yamlField commandField commandWithReview; yamlField taskField task ]
        (String.concat "\n" (bodyLines @ loopFooter))

let buildLoopCommandTemplate (commandName: string) (bodyLines: string list) : string =
    frontMatterPrompt [ yamlField commandField commandName ] (String.concat "\n" bodyLines)

/// Loop cancellation carries a `verdict: cancelled` front-matter anchor so a
/// restart replay recognizes it structurally, followed by the human-readable
/// line. Authored once here, consumed verbatim by both hosts' cancel paths.
let loopCancelledMessage: string =
    frontMatterPrompt [ yamlField verdictField verdictCancelled ] "With-Review Mode cancelled."

let doubleCheckField = "double-check"

let hasDoubleCheckAnchor (texts: string seq) : bool =
    texts
    |> Seq.exists (fun text -> not (isNull text) && text.Contains doubleCheckField)
