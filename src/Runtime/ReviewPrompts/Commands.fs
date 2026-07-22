module Wanxiangshu.Runtime.ReviewPrompts.Commands

open Wanxiangshu.Kernel.Prompt
open Wanxiangshu.Runtime.Prompt
open Wanxiangshu.Runtime.PromptFragments

let withReviewCommandTemplate: string =
    let docView =
        { objective = "$ARGUMENTS"
          background = Some "You are entering With-Review Mode. Complete the task recorded in objective."
          agentRole = AgentRole.CodeReview
          targets = []
          boundaries = []
          rules =
            [ PromptRule.Policy
                  "CRITICAL: You must fully satisfy every requirement in the task — no shortcuts, no partial implementations, no deferred work. If the task has multiple items, every single one must be addressed. Do not skip, trim, or reduce scope under any circumstance. The reviewer will verify completeness against the original task text."
              PromptRule.Criterion reviewCriteria
              PromptRule.Policy
                  "Defend proactively against reviewer rejection: keep the implementation natural, minimal, complete, and well-tested."
              PromptRule.Contract "Do not end the conversation without submit_review." ]
          outcomes =
            [ { label = "submit_review"
                text =
                  "Before finishing, call submit_review with report, affectedFiles, and wip (defaults to true until everything required is done)." } ] }

    match PromptDocument.create docView with
    | Ok doc -> PromptToml.render doc
    | Error errs -> failwithf "Failed to create withReviewCommandTemplate doc: %A" errs
