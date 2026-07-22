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

/// Atomic submit_review contracts — not a free-form background dump.
let private loopSubmitRules: PromptRule list =
    [ PromptRule.Contract "submit_review.report: detailed description of what you did and why."
      PromptRule.Contract "submit_review.affectedFiles: every file modified or created."
      PromptRule.Contract
          "submit_review.wip: omit or true while incomplete; false only when the full task is done."
      PromptRule.Policy "Complete every task item — no shortcuts, reduced scope, or deferred work."
      PromptRule.Policy "A reviewer examines the submission; address REVISE feedback if returned." ]

let private reviewModeActive: PromptTarget =
    PromptTarget.EvidenceTarget("review_mode", "active")

let buildLoopMessage (task: string) (contextLines: string list) : string =
    let context =
        contextLines |> List.filter (fun l -> not (System.String.IsNullOrWhiteSpace l))

    let background =
        if List.isEmpty context then
            None
        else
            Some(String.concat "\n" context)

    let docView =
        { objective = task
          background = background
          agentRole = AgentRole.CodeReview
          targets = [ reviewModeActive ]
          boundaries = []
          rules = loopSubmitRules
          outcomes =
            [ { label = "submit_review"
                text = "Call submit_review when work is complete." } ] }

    match PromptDocument.create docView with
    | Ok doc -> PromptToml.render doc
    | Error errs -> failwithf "Failed to create buildLoopMessage doc: %A" errs

let buildLoopCommandTemplate (commandName: string) (ruleLines: string list) : string =
    let contextRules =
        ruleLines
        |> List.filter (fun l -> not (System.String.IsNullOrWhiteSpace l))
        |> List.map PromptRule.Policy

    let docView =
        { objective = commandName
          background = None
          agentRole = AgentRole.CodeReview
          targets = [ PromptTarget.EvidenceTarget("command", commandName) ]
          boundaries = []
          rules = contextRules
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
          background = None
          agentRole = AgentRole.CodeReview
          targets = [ PromptTarget.EvidenceTarget("review_mode", "cancelled") ]
          boundaries = []
          rules = []
          outcomes =
            [ { label = "cancelled"
                text = "With-Review Mode cancelled." } ] }

    match PromptDocument.create docView with
    | Ok doc -> PromptToml.render doc
    | Error errs -> failwithf "Failed to create loopCancelledMessage doc: %A" errs
