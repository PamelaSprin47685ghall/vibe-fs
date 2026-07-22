module Wanxiangshu.Runtime.LoopMessages

open Wanxiangshu.Kernel.Prompt
open Wanxiangshu.Runtime.Prompt

/// Structured field names and verdict values shared by producers —
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
    let bg = String.concat "\n" (bodyLines @ loopFooter)

    let docView =
        { objective = task
          background = Some bg
          agentRole = AgentRole.CodeReview
          targets = []
          boundaries = []
          rules = [ PromptRule.Contract "Complete every requirement in the task and submit review." ]
          outcomes =
            [ { label = "submit_review"
                text = "Call submit_review when work is complete." } ] }

    match PromptDocument.create docView with
    | Ok doc -> PromptToml.render doc
    | Error errs -> failwithf "Failed to create buildLoopMessage doc: %A" errs

let buildLoopCommandTemplate (commandName: string) (bodyLines: string list) : string =
    let bg = String.concat "\n" bodyLines

    let docView =
        { objective = commandName
          background = Some bg
          agentRole = AgentRole.CodeReview
          targets = []
          boundaries = []
          rules = []
          outcomes =
            [ { label = "activate"
                text = $"Activate {commandName} mode." } ] }

    match PromptDocument.create docView with
    | Ok doc -> PromptToml.render doc
    | Error errs -> failwithf "Failed to create buildLoopCommandTemplate doc: %A" errs

/// Loop cancellation carries a structured verdict in TOML outcomes.
let loopCancelledMessage: string =
    let docView =
        { objective = "Cancel With-Review Mode"
          background = Some "With-Review Mode cancelled."
          agentRole = AgentRole.CodeReview
          targets = []
          boundaries = []
          rules = []
          outcomes =
            [ { label = "cancelled"
                text = "With-Review Mode cancelled." } ] }

    match PromptDocument.create docView with
    | Ok doc -> PromptToml.render doc
    | Error errs -> failwithf "Failed to create loopCancelledMessage doc: %A" errs
